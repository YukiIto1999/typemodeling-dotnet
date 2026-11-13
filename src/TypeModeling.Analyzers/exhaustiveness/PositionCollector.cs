using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Exhaustiveness;

/// <summary>switch の arm の pattern が型を調べる部分値の位置の収集</summary>
internal static class PositionCollector
{
    /// <summary>switch 式の入力と全ての arm から集めた位置</summary>
    /// <param name="switchExpression">位置を集める switch 式</param>
    /// <param name="typeText">型名を解決する switch の位置の文脈</param>
    /// <returns>pattern が型を調べる部分値の位置。重複は足す pattern の段階で除く</returns>
    /// <remarks>
    /// 閉じた型の値を絞る部分 pattern は、その位置の型を入力の型に持つので、絞った後の型は位置に数えない。
    /// not の内側だけに現れる位置は他の arm が調べないので、そこに存在しない値を足しても網羅の判定は変わらない。
    /// </remarks>
    internal static ImmutableArray<PatternPosition> Collect(ISwitchExpressionOperation switchExpression, TypeText typeText)
    {
        var positions = new List<PatternPosition>
        {
            new(PatternPath.Root, switchExpression.Value.Type, UnionOfValue(switchExpression.Value)),
        };
        if (switchExpression.Value is ITupleOperation tuple)
        {
            for (var index = 0; index < tuple.Elements.Length; index++)
            {
                var element = tuple.Elements[index];
                positions.Add(new(PatternPath.Root.Element(null, index, tuple.Elements.Length), element.Type, UnionOfValue(element)));
            }
        }

        foreach (var arm in switchExpression.Arms)
        {
            PatternWalk.Run(
                arm.Pattern,
                PatternPath.Root,
                typeText,
                intoOpaque: false,
                visit => positions.Add(new PatternPosition(visit.Path, visit.Pattern.InputType)));
        }

        return [.. positions];
    }

    /// <summary>union 型の Value を参照する operation の union 型</summary>
    /// <param name="operation">参照を調べる operation。式が分からなければ null</param>
    /// <returns>union 型か、その union member provider の Value の参照なら、その union 型。それ以外は null</returns>
    internal static INamedTypeSymbol? UnionOfValue(IOperation? operation)
    {
        if (operation is not IPropertyReferenceOperation { Property: { ContainingType: { } owner } property })
            return null;

        var union = UnionTypeSet.IsUnion(owner) ? owner : owner.ContainingType;
        return UnionTypeSet.IsUnion(union) && SymbolEqualityComparer.Default.Equals(UnionTypeSet.ValueProperty(union), property)
            ? union
            : null;
    }
}
