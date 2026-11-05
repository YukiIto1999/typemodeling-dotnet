using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Exhaustiveness;

/// <summary>switch の arm の pattern が型を調べる部分値の位置の収集</summary>
internal sealed class PositionCollector
{
    /// <summary>型名を解決する switch の位置の文脈</summary>
    private readonly TypeText _typeText;

    /// <summary>集めた位置。重複は足す pattern の段階で除く</summary>
    private readonly List<PatternPosition> _positions = [];

    /// <summary>型名の文脈による初期化</summary>
    /// <param name="typeText">型名を解決する switch の位置の文脈</param>
    private PositionCollector(TypeText typeText) => _typeText = typeText;

    /// <summary>switch 式の入力と全ての arm から集めた位置</summary>
    /// <param name="switchExpression">位置を集める switch 式</param>
    /// <param name="typeText">型名を解決する switch の位置の文脈</param>
    /// <returns>pattern が型を調べる部分値の位置</returns>
    internal static ImmutableArray<PatternPosition> Collect(ISwitchExpressionOperation switchExpression, TypeText typeText)
    {
        var collector = new PositionCollector(typeText);
        collector.Add(PatternPath.Root, switchExpression.Value.Type, UnionOfValue(switchExpression.Value));
        if (switchExpression.Value is ITupleOperation tuple)
        {
            for (var index = 0; index < tuple.Elements.Length; index++)
            {
                var element = tuple.Elements[index];
                collector.Add(PatternPath.Root.Element(null, index, tuple.Elements.Length), element.Type, UnionOfValue(element));
            }
        }
        foreach (var arm in switchExpression.Arms)
            collector.Visit(arm.Pattern, PatternPath.Root);

        return [.. collector._positions];
    }

    /// <summary>pattern とその部分 pattern の位置の収集</summary>
    /// <param name="pattern">調べる pattern</param>
    /// <param name="path">pattern が当たる部分値への経路</param>
    /// <remarks>
    /// 閉じた型の値を絞る部分 pattern は、その位置の型を入力の型に持つので、絞った後の型は位置に数えない。
    /// not の内側だけに現れる位置は他の arm が調べないので、そこに存在しない値を足しても網羅の判定は変わらない。
    /// </remarks>
    private void Visit(IPatternOperation pattern, PatternPath path)
    {
        Add(path, pattern.InputType);
        switch (pattern)
        {
            case IBinaryPatternOperation binary:
                Visit(binary.LeftPattern, path);
                Visit(binary.RightPattern, path);
                break;
            case IRecursivePatternOperation recursive:
                VisitRecursive(recursive, path);
                break;
        }
    }

    /// <summary>positional と property の部分 pattern の位置の収集</summary>
    /// <param name="recursive">調べる recursive pattern</param>
    /// <param name="path">pattern が当たる部分値への経路</param>
    private void VisitRecursive(IRecursivePatternOperation recursive, PatternPath path)
    {
        var narrowing = Narrowing(recursive);
        var elements = recursive.DeconstructionSubpatterns;
        for (var index = 0; index < elements.Length; index++)
            Visit(elements[index], path.Element(narrowing, index, elements.Length));

        foreach (var property in recursive.PropertySubpatterns)
        {
            if (MemberPath(property.Member, path, narrowing) is { } memberPath)
                Visit(property.Pattern, memberPath);
        }
    }

    /// <summary>recursive pattern に書く型</summary>
    /// <param name="recursive">型を書く recursive pattern</param>
    /// <returns>入力の型を絞る型の名前。絞らないなら null</returns>
    private string? Narrowing(IRecursivePatternOperation recursive)
    {
        var matched = recursive.MatchedType;
        if (matched.IsTupleType || SymbolEqualityComparer.Default.Equals(matched, recursive.InputType))
            return null;

        return _typeText.Render(matched);
    }

    /// <summary>property pattern の member を足した経路</summary>
    /// <param name="member">property pattern の member の参照</param>
    /// <param name="path">property pattern が当たる部分値への経路</param>
    /// <param name="narrowing">property pattern に書く型</param>
    /// <returns>member の先の部分値への経路。member を解決できなければ null</returns>
    /// <remarks>compiler は <c>{ A.B: p }</c> を <c>{ A: { B: p } }</c> の入れ子として表すので、member は一段ずつ現れる</remarks>
    private static PatternPath? MemberPath(IOperation member, PatternPath path, string? narrowing) =>
        member is IMemberReferenceOperation reference ? path.Property(narrowing, reference.Member.Name) : null;

    /// <summary>union 型の Value を参照する operation の union 型</summary>
    /// <param name="operation">参照を調べる operation</param>
    /// <returns>union 型か、その union member provider の Value の参照なら、その union 型。それ以外は null</returns>
    private static INamedTypeSymbol? UnionOfValue(IOperation operation)
    {
        if (operation is not IPropertyReferenceOperation { Property: { ContainingType: { } owner } property })
            return null;

        var union = UnionTypeSet.IsUnion(owner) ? owner : owner.ContainingType;
        return UnionTypeSet.IsUnion(union) && SymbolEqualityComparer.Default.Equals(UnionTypeSet.ValueProperty(union), property)
            ? union
            : null;
    }

    /// <summary>位置の登録</summary>
    /// <param name="path">部分値への経路</param>
    /// <param name="type">その位置で pattern が受け取る型。型のない式なら null</param>
    /// <param name="union">union 型の Value の位置なら、その union 型</param>
    private void Add(PatternPath path, ITypeSymbol? type, INamedTypeSymbol? union = null) =>
        _positions.Add(new PatternPosition(path, type, union));
}
