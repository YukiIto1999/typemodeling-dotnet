using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.ClosedUnion;

/// <summary>制御移動による到達状態更新</summary>
internal static class ReachingLocalControlTransferAnalysis
{
    /// <summary>制御移動による到達状態の更新</summary>
    /// <param name="analysis">local 到達値解析</param>
    /// <param name="operation">解析対象の operation</param>
    /// <param name="incoming">operation 直前の状態</param>
    /// <param name="result">operation 通過後の状態</param>
    /// <returns>制御移動の場合に true</returns>
    internal static bool TryAnalyze(
        ReachingLocalValueAnalysis analysis,
        IOperation operation,
        ReachingLocalFlowState incoming,
        out ReachingLocalFlowState result)
    {
        if (operation is IThrowOperation &&
            !operation.Syntax.Span.Contains(analysis.BeforePosition))
        {
            analysis.CollectThrow(incoming.Copy());
            result = new ReachingLocalFlowState([], canContinue: false);
            return true;
        }

        if (operation is IReturnOperation &&
            !operation.Syntax.Span.Contains(analysis.BeforePosition))
        {
            result = new ReachingLocalFlowState([], canContinue: false);
            return true;
        }

        result = incoming;
        return false;
    }
}
