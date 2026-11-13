using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Diagnostics.ExplicitCase;

/// <summary>switch の一つの位置で、arm が挙げた case と、後から足す case を受け取ってはならない集まり</summary>
/// <remarks>
/// switch の入力と組の要素の位置は、どの arm も case を挙げなくても守る。
/// case の中身の位置は、どれかの arm がその位置の case を挙げて分岐するときだけ守る。
/// </remarks>
internal sealed class CasePosition
{
    /// <summary>pattern が当たる位置の型</summary>
    private readonly ITypeSymbol _inputType;

    /// <summary>位置の case を持つ閉じた型か union 型</summary>
    private readonly INamedTypeSymbol _root;

    /// <summary>位置の case が union 型の case 型か</summary>
    private readonly bool _isUnion;

    /// <summary>case と同じ型の範囲。閉じた型なら閉じた型そのもの、union 型なら case 型</summary>
    private readonly ImmutableArray<ITypeSymbol> _bounds;

    /// <summary>arm が型 pattern で調べた型</summary>
    private readonly List<ITypeSymbol> _tested = [];

    /// <summary>arm が enum の member を定数 pattern で挙げたか</summary>
    private bool _namesMember;

    /// <summary>位置の型と case の集まりによる初期化</summary>
    /// <param name="inputType">pattern が当たる位置の型</param>
    /// <param name="root">位置の case を持つ閉じた型か union 型</param>
    /// <param name="isUnion">位置の case が union 型の case 型なら true</param>
    /// <param name="isScrutinee">switch の入力か組の要素の位置なら true</param>
    private CasePosition(
        ITypeSymbol inputType,
        INamedTypeSymbol root,
        bool isUnion,
        bool isScrutinee
    )
    {
        _inputType = inputType;
        _root = root;
        _isUnion = isUnion;
        _bounds = isUnion ? UnionTypeSet.CaseTypes(root) : [root];
        IsScrutinee = isScrutinee;
    }

    /// <summary>switch の入力か組の要素の位置か</summary>
    internal bool IsScrutinee { get; }

    /// <summary>位置の case を持つ型の名前</summary>
    internal string Name => _root.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    /// <summary>将来の case の暗黙の受理を防ぐ保護状態</summary>
    /// <value>入力位置か明示的な case を持つ位置なら true</value>
    internal bool IsProtected => IsScrutinee || NamesCase;

    /// <summary>この位置での明示的な case 列挙の有無</summary>
    /// <value>いずれかの arm がこの位置の case を挙げていれば true</value>
    private bool NamesCase => _namesMember || _tested.Any(IsCaseName);

    /// <summary>位置の型が閉じた case を持つなら、その位置</summary>
    /// <param name="inputType">pattern が当たる位置の型。型のない式なら null</param>
    /// <param name="union">位置が union 型の Value なら、その union 型</param>
    /// <param name="isScrutinee">switch の入力か組の要素の位置なら true</param>
    /// <returns>閉じた class、閉じた enum、それらに制約された型引数、union 型の Value の位置。それ以外は null</returns>
    internal static CasePosition? At(
        ITypeSymbol? inputType,
        INamedTypeSymbol? union,
        bool isScrutinee
    )
    {
        // union 型の Value を参照する式は型を持つ
        if (union is not null)
            return new CasePosition(inputType!, union, true, isScrutinee);

        return
            ClosedTypeSet.EffectiveClosedType(TypeLineage.WithoutNullable(inputType)) is { } closed
            ? new CasePosition(inputType!, closed, false, isScrutinee)
            : null;
    }

    /// <summary>位置に当てた pattern が挙げた case の記録</summary>
    /// <param name="pattern">位置に当てた pattern</param>
    /// <remarks>not の内側と and と or の枝も、同じ位置の case を挙げるので辿る</remarks>
    internal void Note(IPatternOperation pattern)
    {
        switch (pattern)
        {
            case IBinaryPatternOperation binary:
                Note(binary.LeftPattern);
                Note(binary.RightPattern);
                break;
            case INegatedPatternOperation negated:
                Note(negated.Pattern);
                break;
            case IConstantPatternOperation constant:
                _namesMember |= constant.Value.ConstantValue.Value is not null;
                break;
            case ITypePatternOperation type:
                _tested.Add(type.MatchedType);
                break;
            case IDeclarationPatternOperation { MatchedType: { } matched }:
                _tested.Add(matched);
                break;
            case IRecursivePatternOperation recursive:
                _tested.Add(recursive.MatchedType);
                break;
        }
    }

    /// <summary>後から足す case を受け取ってはならない集まり</summary>
    /// <param name="peers">別経路で同じ値を取り出しうる位置</param>
    /// <returns>保護対象の位置で暗黙に受理してはならない case の集まり</returns>
    /// <remarks>共有する値の case 列挙を含む入れ子の閉じた型の保護</remarks>
    internal ImmutableArray<CaseFamily> Families(IEnumerable<CasePosition> peers)
    {
        CasePosition[] positions = [this, .. peers];
        var names = positions.SelectMany(position => position._tested).Where(IsCaseName).ToArray();
        var families = ImmutableArray.CreateBuilder<CaseFamily>();
        if (IsScrutinee || positions.Any(position => position.NamesCase))
            families.Add(
                _isUnion
                    ? CaseFamily.OfUnion(_root, _inputType)
                    : CaseFamily.OfClosedType(_root, _inputType)
            );

        families.AddRange(
            names
                .SelectMany(ClosedAncestors)
                .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
                .Where(closed => !TypeLineage.Same(closed, _root))
                .Select(closed => CaseFamily.OfClosedType(closed, _inputType))
        );
        return families.ToImmutable();
    }

    /// <summary>型が位置の case を挙げる型かの判定</summary>
    /// <param name="tested">arm が調べた型</param>
    /// <returns>位置の型そのものでなく、case の範囲の内側の型なら true</returns>
    private bool IsCaseName(ITypeSymbol tested) =>
        Within(tested)
        && !TypeLineage.Same(tested, _inputType)
        && (_isUnion || !TypeLineage.Same(tested, _root));

    /// <summary>型が case の範囲の内側かの判定</summary>
    /// <param name="type">判定する型</param>
    /// <returns>case と同じ型か、その派生なら true</returns>
    private bool Within(ITypeSymbol type) =>
        _bounds.Any(bound => TypeLineage.IsAtOrBelow(type, bound));

    /// <summary>case の範囲の内側にある、型の閉じた基底 class</summary>
    /// <param name="tested">arm が挙げた case の型</param>
    /// <returns>型から範囲の端まで辿った、閉じた基底 class</returns>
    private IEnumerable<INamedTypeSymbol> ClosedAncestors(ITypeSymbol tested)
    {
        for (
            var ancestor = tested.BaseType;
            ancestor is not null && Within(ancestor);
            ancestor = ancestor.BaseType
        )
        {
            if (ClosedTypeSet.IsClosedClass(ancestor))
                yield return ancestor;
        }
    }
}
