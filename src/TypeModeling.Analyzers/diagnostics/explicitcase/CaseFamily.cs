using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Facts;
using static TypeModeling.Analyzers.Diagnostics.ExplicitCase.AcceptanceAlgebra;

namespace TypeModeling.Analyzers.Diagnostics.ExplicitCase;

/// <summary>後から足す case を受け取る pattern の判定の対象になる、閉じた case の集まり</summary>
/// <remarks>
/// 閉じた class は後から足す直接の派生を、閉じた enum は後から足す member を、union は後から足す case 型を、名前のない case として持つ。
/// pattern がその case を受け取るかは、compiler の網羅の判定と同じく when を数えず、pattern の形だけで判定する。
/// </remarks>
internal sealed class CaseFamily
{
    /// <summary>この集まりを表す閉じた型か union 型</summary>
    private readonly INamedTypeSymbol _root;

    /// <summary>pattern が当たる位置の型</summary>
    private readonly ITypeSymbol _inputType;

    /// <summary>union 型の case 型。閉じた class と閉じた enum なら未設定</summary>
    private readonly ImmutableArray<ITypeSymbol> _unionCases;

    /// <summary>集まりの型による初期化</summary>
    /// <param name="root">この集まりを表す閉じた型か union 型</param>
    /// <param name="inputType">pattern が当たる位置の型</param>
    /// <param name="unionCases">union 型の case 型。閉じた型なら未設定</param>
    private CaseFamily(
        INamedTypeSymbol root,
        ITypeSymbol inputType,
        ImmutableArray<ITypeSymbol> unionCases
    )
    {
        _root = root;
        _inputType = inputType;
        _unionCases = unionCases;
    }

    /// <summary>診断に書く、集まりを表す型の名前</summary>
    internal string Name => _root.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    /// <summary>union 型の集まりか</summary>
    private bool IsUnion => !_unionCases.IsDefault;

    /// <summary>閉じた class か閉じた enum の集まり</summary>
    /// <param name="closed">閉じた class か閉じた enum</param>
    /// <param name="inputType">pattern が当たる位置の型</param>
    /// <returns>後から足す派生か member の集まり</returns>
    internal static CaseFamily OfClosedType(INamedTypeSymbol closed, ITypeSymbol inputType) =>
        new(closed, inputType, default);

    /// <summary>union 型の集まり</summary>
    /// <param name="union">union 型</param>
    /// <param name="inputType">pattern が当たる位置の型</param>
    /// <returns>後から足す case 型の集まり</returns>
    internal static CaseFamily OfUnion(INamedTypeSymbol union, ITypeSymbol inputType) =>
        new(union, inputType, UnionTypeSet.CaseTypes(union));

    /// <summary>pattern が、後から足す case の値を受け取るかの判定</summary>
    /// <param name="pattern">判定する pattern</param>
    /// <returns>受け取る度合い</returns>
    /// <remarks>relational pattern と list pattern は、case を一つずつ挙げないので受け取りうるとみなす</remarks>
    internal Acceptance Accepts(IPatternOperation pattern) =>
        pattern switch
        {
            IDiscardPatternOperation => Acceptance.Always,
            IDeclarationPatternOperation declaration => TypeTest(declaration.MatchedType),
            ITypePatternOperation type => TypeTest(type.MatchedType),
            IConstantPatternOperation => Acceptance.Never,
            INegatedPatternOperation negated => Not(Accepts(negated.Pattern)),
            IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } binary => Or(
                Accepts(binary.LeftPattern),
                Accepts(binary.RightPattern)
            ),
            IBinaryPatternOperation binary => And(
                Accepts(binary.LeftPattern),
                Accepts(binary.RightPattern)
            ),
            IRecursivePatternOperation recursive => Recursive(recursive),
            _ => Acceptance.Maybe,
        };

    /// <summary>recursive pattern が、後から足す case の値を受け取るかの判定</summary>
    /// <param name="recursive">判定する recursive pattern</param>
    /// <returns>型の判定と、部分 pattern を合わせた度合い</returns>
    /// <remarks>部分 pattern は後から足す case の値の中身を決められないので、部分 pattern を持てば受け取りうる止まりになる</remarks>
    private Acceptance Recursive(IRecursivePatternOperation recursive)
    {
        var type = TypeTest(recursive.MatchedType);
        var hasSubpatterns =
            !recursive.DeconstructionSubpatterns.IsEmpty || !recursive.PropertySubpatterns.IsEmpty;
        return hasSubpatterns ? And(type, Acceptance.Maybe) : type;
    }

    /// <summary>型を調べる pattern が、後から足す case の値を受け取るかの判定</summary>
    /// <param name="matched">pattern が調べる型。var のように書かないなら null</param>
    /// <returns>受け取る度合い</returns>
    private Acceptance TypeTest(ITypeSymbol? matched)
    {
        if (matched is null || TypeLineage.Same(matched, _inputType))
            return Acceptance.Always;

        return IsUnion ? UnionTypeTest(matched) : ClosedTypeTest(matched);
    }

    /// <summary>閉じた型の位置で、型を調べる pattern が受け取るかの判定</summary>
    /// <param name="matched">pattern が調べる型</param>
    /// <returns>閉じた型より広い型なら Always、閉じた型の派生か、入れ子の閉じた型から見た別の派生の class なら Never、閉じた型が実装しない interface なら Maybe</returns>
    private Acceptance ClosedTypeTest(ITypeSymbol matched)
    {
        if (TypeLineage.IsAtOrBelow(_root, matched))
            return Acceptance.Always;

        return matched.TypeKind == TypeKind.Interface ? Acceptance.Maybe : Acceptance.Never;
    }

    /// <summary>union 型の位置で、型を調べる pattern が受け取るかの判定</summary>
    /// <param name="matched">pattern が調べる型</param>
    /// <returns>case 型かその派生なら Never、それ以外は Maybe</returns>
    private Acceptance UnionTypeTest(ITypeSymbol matched) =>
        _unionCases.Any(caseType => TypeLineage.IsAtOrBelow(matched, caseType))
            ? Acceptance.Never
            : Acceptance.Maybe;
}
