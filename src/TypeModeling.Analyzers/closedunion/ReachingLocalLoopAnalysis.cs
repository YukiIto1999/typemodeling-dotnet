using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.ClosedUnion;

/// <summary>loop 固定点における local 到達状態解析</summary>
internal static class ReachingLocalLoopAnalysis
{
    /// <summary>for loop 通過後の到達状態</summary>
    /// <param name="analysis">local 到達値解析</param>
    /// <param name="forLoop">解析対象の for loop</param>
    /// <param name="incoming">for loop 直前の状態</param>
    /// <returns>for loop 通過後の状態</returns>
    internal static ReachingLocalFlowState AnalyzeFor(
        ReachingLocalValueAnalysis analysis,
        IForLoopOperation forLoop,
        ReachingLocalFlowState incoming)
    {
        var loopInput = analysis.AnalyzeSequence(forLoop.Before, incoming);
        if (forLoop.Condition is { } condition)
            loopInput = analysis.AnalyzeOperation(condition, loopInput);
        return AnalyzeBody(
            analysis,
            forLoop.Body,
            loopInput,
            forLoop.AtLoopBottom,
            zeroIterationsAllowed: true);
    }

    /// <summary>一般 loop 通過後の到達状態</summary>
    /// <param name="analysis">local 到達値解析</param>
    /// <param name="loop">解析対象の loop</param>
    /// <param name="incoming">loop 直前の状態</param>
    /// <returns>loop 通過後の状態</returns>
    internal static ReachingLocalFlowState AnalyzeLoop(
        ReachingLocalValueAnalysis analysis,
        ILoopOperation loop,
        ReachingLocalFlowState incoming)
    {
        var zeroIterationsAllowed = loop is not IWhileLoopOperation { ConditionIsTop: false };
        return AnalyzeBody(analysis, loop.Body, incoming, [], zeroIterationsAllowed);
    }

    /// <summary>loop body の固定点解析</summary>
    /// <param name="analysis">local 到達値解析</param>
    /// <param name="body">解析対象の loop body</param>
    /// <param name="incoming">loop 入口の状態</param>
    /// <param name="atLoopBottom">反復末尾の operation 列</param>
    /// <param name="zeroIterationsAllowed">ゼロ回反復を許す判定</param>
    /// <returns>参照位置へ到達する状態</returns>
    private static ReachingLocalFlowState AnalyzeBody(
        ReachingLocalValueAnalysis analysis,
        IOperation body,
        ReachingLocalFlowState incoming,
        ImmutableArray<IOperation> atLoopBottom,
        bool zeroIterationsAllowed)
    {
        if (!body.Syntax.Span.Contains(analysis.BeforePosition))
        {
            var afterIteration = analysis.AnalyzeOperation(body, incoming.Copy());
            afterIteration = analysis.AnalyzeSequence(atLoopBottom, afterIteration);
            return zeroIterationsAllowed
                ? ReachingLocalFlowState.Merge(incoming, afterIteration)
                : afterIteration;
        }

        var loopEntry = incoming.Copy();
        while (true)
        {
            var fullAnalysis = new ReachingLocalValueAnalysis(analysis.Target, int.MaxValue);
            var afterIteration = fullAnalysis.AnalyzeOperation(body, loopEntry.Copy());
            afterIteration = fullAnalysis.AnalyzeSequence(atLoopBottom, afterIteration);
            var nextEntry = zeroIterationsAllowed
                ? ReachingLocalFlowState.Merge(incoming, afterIteration)
                : afterIteration;
            if (ReachingLocalFlowState.Equivalent(loopEntry, nextEntry))
                break;
            loopEntry = nextEntry;
        }

        return analysis.AnalyzeOperation(body, loopEntry);
    }
}
