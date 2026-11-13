using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.Exhaustiveness;

/// <summary>pattern の木にある全ての部分 pattern の、当たる部分値への経路を伴う巡回</summary>
/// <remarks>
/// 閉じた型の値を絞る部分 pattern は、その位置の型を入力の型に持つので、絞った後の型は経路に数えない。
/// 巡回は and と or の枝と、positional と property の部分 pattern をたどる。
/// not と list pattern の内側は、位置が当たる値の範囲を経路から決められないので、求めたときだけ Opaque として訪れる。
/// </remarks>
internal sealed class PatternWalk
{
    /// <summary>型名を解決する switch の位置の文脈</summary>
    private readonly TypeText _typeText;

    /// <summary>not と list pattern の内側も訪れるか</summary>
    private readonly bool _intoOpaque;

    /// <summary>訪れた pattern の受け取り</summary>
    private readonly Action<PatternVisit> _visit;

    /// <summary>巡回の設定による初期化</summary>
    /// <param name="typeText">型名を解決する switch の位置の文脈</param>
    /// <param name="intoOpaque">not と list pattern の内側も訪れる場合に true</param>
    /// <param name="visit">訪れた pattern の受け取り</param>
    private PatternWalk(TypeText typeText, bool intoOpaque, Action<PatternVisit> visit)
    {
        _typeText = typeText;
        _intoOpaque = intoOpaque;
        _visit = visit;
    }

    /// <summary>pattern とその部分 pattern の巡回</summary>
    /// <param name="pattern">switch の入力に当てた pattern</param>
    /// <param name="path">pattern が当たる部分値への経路</param>
    /// <param name="typeText">型名を解決する switch の位置の文脈</param>
    /// <param name="intoOpaque">not と list pattern の内側も訪れる場合に true</param>
    /// <param name="visit">訪れた pattern の受け取り</param>
    internal static void Run(
        IPatternOperation pattern,
        PatternPath path,
        TypeText typeText,
        bool intoOpaque,
        Action<PatternVisit> visit) =>
        new PatternWalk(typeText, intoOpaque, visit).Visit(pattern, path, true, false);

    /// <summary>pattern の訪問と、部分 pattern の巡回</summary>
    /// <param name="pattern">訪れる pattern</param>
    /// <param name="path">pattern が当たる部分値への経路</param>
    /// <param name="isPosition">部分値に当てた pattern の最上位なら true</param>
    /// <param name="opaque">not か list pattern の内部を通る経路なら true</param>
    private void Visit(IPatternOperation pattern, PatternPath path, bool isPosition, bool opaque)
    {
        _visit(new PatternVisit(path, pattern, isPosition, opaque));
        switch (pattern)
        {
            case IBinaryPatternOperation binary:
                Visit(binary.LeftPattern, path, false, opaque);
                Visit(binary.RightPattern, path, false, opaque);
                break;
            case INegatedPatternOperation negation when _intoOpaque:
                Visit(negation.Pattern, path, false, true);
                break;
            case IRecursivePatternOperation recursive:
                VisitRecursive(recursive, path, opaque);
                break;
            case IListPatternOperation list when _intoOpaque:
                VisitList(list, path);
                break;
            case ISlicePatternOperation { Pattern: { } slice } when _intoOpaque:
                Visit(slice, path, true, true);
                break;
        }
    }

    /// <summary>positional と property の部分 pattern の巡回</summary>
    /// <param name="recursive">調べる recursive pattern</param>
    /// <param name="path">pattern が当たる部分値への経路</param>
    /// <param name="opaque">not か list pattern の内部を通る経路なら true</param>
    private void VisitRecursive(IRecursivePatternOperation recursive, PatternPath path, bool opaque)
    {
        var narrowing = Narrowing(recursive);
        var elements = recursive.DeconstructionSubpatterns;
        for (var index = 0; index < elements.Length; index++)
            Visit(elements[index], path.Element(narrowing, index, elements.Length), true, opaque);

        foreach (var property in recursive.PropertySubpatterns)
        {
            if (MemberPath(property.Member, path, narrowing) is { } memberPath)
                Visit(property.Pattern, memberPath, true, opaque);
        }
    }

    /// <summary>list pattern の要素の巡回</summary>
    /// <param name="list">調べる list pattern</param>
    /// <param name="path">pattern が当たる部分値への経路</param>
    /// <remarks>要素の位置は list の長さと並びに依存するので、全ての要素を Opaque として訪れる</remarks>
    private void VisitList(IListPatternOperation list, PatternPath path)
    {
        var elements = list.Patterns;
        for (var index = 0; index < elements.Length; index++)
            Visit(elements[index], path.Element(null, index, elements.Length), true, true);
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
}
