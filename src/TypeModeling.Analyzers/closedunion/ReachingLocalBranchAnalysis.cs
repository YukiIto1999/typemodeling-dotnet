using System.Collections.Generic;
using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.ClosedUnion;

/// <summary>分岐構造における local 到達状態解析</summary>
internal static class ReachingLocalBranchAnalysis
{
    /// <summary>条件分岐通過後の到達状態</summary>
    /// <param name="analysis">local 到達値解析</param>
    /// <param name="conditional">解析対象の条件分岐</param>
    /// <param name="incoming">条件分岐直前の状態</param>
    /// <returns>条件分岐通過後の状態</returns>
    internal static ReachingLocalFlowState AnalyzeConditional(
        ReachingLocalValueAnalysis analysis,
        IConditionalOperation conditional,
        ReachingLocalFlowState incoming)
    {
        if (conditional.WhenTrue.Syntax.Span.Contains(analysis.BeforePosition))
            return analysis.AnalyzeOperation(conditional.WhenTrue, incoming);
        if (conditional.WhenFalse?.Syntax.Span.Contains(analysis.BeforePosition) == true)
            return analysis.AnalyzeOperation(conditional.WhenFalse, incoming);

        var whenTrue = analysis.AnalyzeOperation(conditional.WhenTrue, incoming.Copy());
        var whenFalse = conditional.WhenFalse is { } alternative
            ? analysis.AnalyzeOperation(alternative, incoming.Copy())
            : incoming.Copy();
        return ReachingLocalFlowState.Merge(whenTrue, whenFalse);
    }

    /// <summary>switch 通過後の到達状態</summary>
    /// <param name="analysis">local 到達値解析</param>
    /// <param name="switchOperation">解析対象の switch</param>
    /// <param name="incoming">switch 直前の状態</param>
    /// <returns>switch 通過後の状態</returns>
    internal static ReachingLocalFlowState AnalyzeSwitch(
        ReachingLocalValueAnalysis analysis,
        ISwitchOperation switchOperation,
        ReachingLocalFlowState incoming)
    {
        if (switchOperation.Syntax.Span.Contains(analysis.BeforePosition))
            return AnalyzeContainingSwitch(analysis, switchOperation, incoming);

        var result = new ReachingLocalFlowState([], canContinue: false);
        var hasDefault = false;
        foreach (var switchCase in switchOperation.Cases)
        {
            foreach (var clause in switchCase.Clauses)
                hasDefault |= clause is IDefaultCaseClauseOperation;
            result = ReachingLocalFlowState.Merge(
                result,
                analysis.AnalyzeSequence(switchCase.Body, incoming.Copy()));
        }

        return hasDefault ? result : ReachingLocalFlowState.Merge(result, incoming);
    }

    /// <summary>解析位置を含む switch 内の到達状態</summary>
    /// <param name="analysis">local 到達値解析</param>
    /// <param name="switchOperation">解析対象の switch</param>
    /// <param name="incoming">switch 直前の状態</param>
    /// <returns>解析位置直前の状態</returns>
    private static ReachingLocalFlowState AnalyzeContainingSwitch(
        ReachingLocalValueAnalysis analysis,
        ISwitchOperation switchOperation,
        ReachingLocalFlowState incoming)
    {
        foreach (var switchCase in switchOperation.Cases)
        {
            var caseState = incoming.Copy();
            foreach (var bodyOperation in switchCase.Body)
            {
                if (bodyOperation.Syntax.Span.Contains(analysis.BeforePosition))
                    return analysis.AnalyzeOperation(bodyOperation, caseState);
                if (bodyOperation.Syntax.SpanStart >= analysis.BeforePosition ||
                    !caseState.CanContinue)
                {
                    break;
                }
                caseState = analysis.AnalyzeOperation(bodyOperation, caseState);
            }
        }

        return incoming;
    }

    /// <summary>try statement 通過後の到達状態</summary>
    /// <param name="analysis">local 到達値解析</param>
    /// <param name="tryOperation">解析対象の try statement</param>
    /// <param name="incoming">try statement 直前の状態</param>
    /// <returns>try statement 通過後の状態</returns>
    internal static ReachingLocalFlowState AnalyzeTry(
        ReachingLocalValueAnalysis analysis,
        ITryOperation tryOperation,
        ReachingLocalFlowState incoming)
    {
        var thrown = new List<ReachingLocalFlowState>();
        analysis.PushThrowCollector(thrown);
        var bodyResult = analysis.AnalyzeOperation(tryOperation.Body, incoming.Copy());
        analysis.PopThrowCollector();

        var result = bodyResult;
        if (!tryOperation.Catches.IsEmpty)
        {
            var catchInput = incoming.Copy();
            foreach (var thrownState in thrown)
                catchInput = ReachingLocalFlowState.Merge(catchInput, thrownState);
            foreach (var catchClause in tryOperation.Catches)
            {
                result = ReachingLocalFlowState.Merge(
                    result,
                    analysis.AnalyzeOperation(catchClause.Handler, catchInput.Copy()));
            }
        }

        return tryOperation.Finally is { } finallyBlock
            ? analysis.AnalyzeOperation(finallyBlock, result)
            : result;
    }
}
