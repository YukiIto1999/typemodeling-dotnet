using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Diagnostics.ExplicitCase;

/// <summary>位置から部分値を取り出す段</summary>
/// <param name="Provenance">値の取り出しを識別する symbol か組の要素を表す null</param>
/// <param name="Index">引数・要素の位置か member の段を表す 0</param>
/// <param name="Scope">段が当たる値の型か入力位置を表す null</param>
/// <remarks>symbol で裏付けた値の共有に限定した位置の同一視</remarks>
internal readonly record struct PositionStep(ISymbol? Provenance, int Index, ITypeSymbol? Scope)
{
    /// <summary>組の要素の段</summary>
    /// <param name="index">要素の位置</param>
    /// <param name="tuple">要素を持つ組の型</param>
    /// <returns>組の要素の段</returns>
    internal static PositionStep TupleElement(int index, ITypeSymbol tuple) => new(null, index, tuple);

    /// <summary>部分値を取り出す段を持つ pattern の特定</summary>
    /// <param name="pattern">部分値に当てた pattern の最上位</param>
    /// <returns>所有する pattern と段の組か arm の最上位を表す null</returns>
    internal static (IPatternOperation Owner, PositionStep Step)? Of(IPatternOperation pattern) => pattern.Parent switch
    {
        IRecursivePatternOperation recursive =>
            (recursive, Positional(recursive, recursive.DeconstructionSubpatterns.IndexOf(pattern))),
        IPropertySubpatternOperation { Parent: IRecursivePatternOperation recursive } property
            when Property(recursive, property) is { } step => (recursive, step),
        IListPatternOperation list => (list, new PositionStep(list.InputType, list.Patterns.IndexOf(pattern), list.InputType)),
        _ => null,
    };

    /// <summary>recursive pattern が部分 pattern で書く全ての段</summary>
    /// <param name="recursive">調べる recursive pattern</param>
    /// <returns>明示された各部分値を取り出す段</returns>
    internal static IEnumerable<PositionStep> Written(IRecursivePatternOperation recursive) =>
        recursive.DeconstructionSubpatterns.Select((_, index) => Positional(recursive, index))
            .Concat(recursive.PropertySubpatterns.Select(property => Property(recursive, property)).OfType<PositionStep>());

    /// <summary>位置の木で同じ節になる段かの判定</summary>
    /// <param name="other">比べる段</param>
    /// <returns>同じ出どころの値を同じ型の範囲で取り出すなら true</returns>
    /// <remarks>要素名を問わず位置だけで識別する組の要素</remarks>
    internal bool IsSameAs(PositionStep other) =>
        Index == other.Index
        && SymbolEqualityComparer.Default.Equals(Provenance, other.Provenance)
        && (Provenance is null || SymbolEqualityComparer.Default.Equals(Scope, other.Scope));

    /// <summary>同じ値を取り出しうる段かの判定</summary>
    /// <param name="other">比べる段</param>
    /// <param name="conversions">型の重なりを判定する変換</param>
    /// <returns>同じ member か実装先の member を重なる型の値から取り出しうるなら true</returns>
    /// <remarks>型の重なりと member の同一性を別の根拠で判定する構成</remarks>
    internal bool Overlaps(PositionStep other, TypeConversions conversions)
    {
        if (Index != other.Index)
            return false;

        if (Provenance is null || other.Provenance is null)
            return Provenance is null && other.Provenance is null;

        return (SymbolEqualityComparer.Default.Equals(Provenance, other.Provenance) || IsImplementedBy(other) || other.IsImplementedBy(this))
            && MayShareValue(Scope, other.Scope, conversions);
    }

    /// <summary>interface member と確定した実装先の照合</summary>
    /// <param name="concrete">実装する member を読む段</param>
    /// <returns>再実装できない型の実装先と同じ仮想 slot なら true</returns>
    /// <remarks>同名の public member とは別に識別する明示的な interface 実装</remarks>
    private bool IsImplementedBy(PositionStep concrete)
    {
        if (Provenance is not { ContainingType.TypeKind: TypeKind.Interface } member || member is not (IPropertySymbol or IMethodSymbol))
            return false;

        if (concrete.Provenance is not { } actual || concrete.Scope is not { } scope || !CannotBeReimplemented(scope))
            return false;

        return scope.FindImplementationForInterfaceMember(member) is { } implementation
            && SymbolEqualityComparer.Default.Equals(Slot(implementation), Slot(actual));
    }

    /// <summary>派生による interface の再実装の不可能性の判定</summary>
    /// <param name="type">判定する型</param>
    /// <returns>sealed な型か値型なら true</returns>
    private static bool CannotBeReimplemented(ITypeSymbol type) =>
        type.TypeKind != TypeKind.TypeParameter && (type.IsSealed || type.IsValueType);

    /// <summary>property pattern の一つの member の段</summary>
    /// <param name="recursive">property pattern を持つ recursive pattern</param>
    /// <param name="subpattern">一つの member に当てた部分 pattern</param>
    /// <returns>組の要素か member に対応する段か解決不能を表す null</returns>
    private static PositionStep? Property(IRecursivePatternOperation recursive, IPropertySubpatternOperation subpattern)
    {
        if (subpattern.Member is not IMemberReferenceOperation { Member: { } member })
            return null;

        var scope = recursive.MatchedType;
        return TupleElementIndex(scope, member) is { } index
            ? TupleElement(index, scope)
            : new PositionStep(Slot(member), 0, scope);
    }

    /// <summary>positional pattern の一つの要素の段</summary>
    /// <param name="recursive">positional pattern を持つ recursive pattern</param>
    /// <param name="index">要素の位置</param>
    /// <returns>組の要素か自動生成 Deconstruct の property か独立した引数を表す段</returns>
    private static PositionStep Positional(IRecursivePatternOperation recursive, int index)
    {
        var scope = recursive.MatchedType;
        if (scope.IsTupleType)
            return TupleElement(index, scope);

        var deconstruct = recursive.DeconstructSymbol;
        return RecordProperty(deconstruct, index) is { } property
            ? new PositionStep(Slot(property), 0, scope)
            : new PositionStep(Slot(deconstruct ?? scope), index, scope);
    }

    /// <summary>組の型の field が取り出す要素の位置</summary>
    /// <param name="scope">pattern が当たる値の型</param>
    /// <param name="member">property pattern が読む member</param>
    /// <returns>組の要素の位置か該当しない member を表す null</returns>
    /// <remarks>要素名と ItemN を同一視する既定 field との対応</remarks>
    private static int? TupleElementIndex(ITypeSymbol scope, ISymbol member)
    {
        if (scope is not INamedTypeSymbol { IsTupleType: true } tuple || member is not IFieldSymbol field)
            return null;

        var elements = tuple.TupleElements;
        for (var index = 0; index < elements.Length; index++)
        {
            if (Denotes(elements[index], field))
                return index;
        }

        return null;
    }

    /// <summary>組の field が指す要素の同一性の判定</summary>
    /// <param name="element">組の型の要素の field</param>
    /// <param name="field">property pattern が読む field</param>
    /// <returns>同じ要素の既定 field に対応するなら true</returns>
    private static bool Denotes(IFieldSymbol element, IFieldSymbol field) =>
        SymbolEqualityComparer.Default.Equals(element, field)
        || (element.CorrespondingTupleField is { } defaultField
            && (SymbolEqualityComparer.Default.Equals(defaultField, field)
                || SymbolEqualityComparer.Default.Equals(defaultField, field.CorrespondingTupleField)));

    /// <summary>member が読む仮想 slot の出どころ</summary>
    /// <param name="member">property か Deconstruct か型</param>
    /// <returns>最初に宣言された仮想 slot か仮想 slot を持たない元の member</returns>
    private static ISymbol Slot(ISymbol member) => member switch
    {
        IPropertySymbol { OverriddenProperty: { } overridden } => Slot(overridden),
        IMethodSymbol { OverriddenMethod: { } overridden } => Slot(overridden),
        _ => member,
    };

    /// <summary>二つの型が同じ値の型になりうるかの判定</summary>
    /// <param name="left">比べる型か入力位置を表す null</param>
    /// <param name="right">比べる型か入力位置を表す null</param>
    /// <param name="conversions">型の重なりを判定する変換</param>
    /// <returns>型を持たない位置か重なりうる型なら true</returns>
    private static bool MayShareValue(ITypeSymbol? left, ITypeSymbol? right, TypeConversions conversions) =>
        left is null || right is null || conversions.MayShare(left, right);

    /// <summary>compiler が作った record の Deconstruct の引数が返す property</summary>
    /// <param name="deconstruct">呼び出す Deconstruct か未使用を表す null</param>
    /// <param name="index">引数の位置</param>
    /// <returns>ソース内の自動生成 Deconstruct の引数に対応する property か null</returns>
    /// <remarks>自動生成と手書きを区別できない参照 assembly の Deconstruct の除外</remarks>
    private static IPropertySymbol? RecordProperty(ISymbol? deconstruct, int index)
    {
        if (deconstruct is not IMethodSymbol { IsImplicitlyDeclared: true, ContainingType: { IsRecord: true } owner } method
            || index >= method.Parameters.Length)
        {
            return null;
        }

        var parameter = method.Parameters[index];
        return Lineage(owner)
            .SelectMany(type => type.GetMembers(parameter.Name))
            .OfType<IPropertySymbol>()
            .FirstOrDefault(property => !property.IsStatic && SymbolEqualityComparer.Default.Equals(property.Type, parameter.Type));
    }

    /// <summary>型とその基底 class</summary>
    /// <param name="type">起点の型</param>
    /// <returns>型から基底の class へ辿った型</returns>
    private static IEnumerable<INamedTypeSymbol> Lineage(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            yield return current;
    }
}
