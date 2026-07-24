using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Diagnostics.ClosedUnionSwitchStatement;

/// <summary>local 到達値解析の単一経路状態</summary>
internal sealed class ReachingLocalFlowState
{
    /// <summary>到達値解析状態の構築</summary>
    /// <param name="values">経路上の到達値</param>
    /// <param name="canContinue">経路が後続へ到達する判定</param>
    internal ReachingLocalFlowState(List<IOperation> values, bool canContinue)
    {
        Values = values;
        CanContinue = canContinue;
    }

    /// <summary>経路上の到達値</summary>
    internal List<IOperation> Values { get; }

    /// <summary>経路が後続へ到達する判定</summary>
    internal bool CanContinue { get; }

    /// <summary>値集合を複製した到達状態</summary>
    /// <returns>独立した値集合を持つ状態</returns>
    internal ReachingLocalFlowState Copy() => new([.. Values], CanContinue);

    /// <summary>二つの到達経路の合流</summary>
    /// <param name="first">第一の到達経路</param>
    /// <param name="second">第二の到達経路</param>
    /// <returns>継続可能な経路の値集合</returns>
    internal static ReachingLocalFlowState Merge(
        ReachingLocalFlowState first,
        ReachingLocalFlowState second)
    {
        if (!first.CanContinue)
            return second;
        if (!second.CanContinue)
            return first;

        var values = new List<IOperation>(first.Values);
        foreach (var candidate in second.Values.Where(candidate =>
                     !values.Exists(existing =>
                         existing.Syntax.SyntaxTree == candidate.Syntax.SyntaxTree &&
                         existing.Syntax.Span == candidate.Syntax.Span)))
        {
            values.Add(candidate);
        }

        return new ReachingLocalFlowState(values, canContinue: true);
    }

    /// <summary>固定点判定のための到達状態同値性</summary>
    /// <param name="first">第一の到達状態</param>
    /// <param name="second">第二の到達状態</param>
    /// <returns>到達状態が同値の場合に true</returns>
    internal static bool Equivalent(
        ReachingLocalFlowState first,
        ReachingLocalFlowState second)
    {
        if (first.CanContinue != second.CanContinue || first.Values.Count != second.Values.Count)
            return false;

        return first.Values.All(candidate =>
            second.Values.Exists(existing =>
                existing.Syntax.SyntaxTree == candidate.Syntax.SyntaxTree &&
                existing.Syntax.Span == candidate.Syntax.Span));
    }
}
