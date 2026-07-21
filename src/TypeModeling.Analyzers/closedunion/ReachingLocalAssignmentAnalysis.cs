using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.ClosedUnion;

/// <summary>local 代入による到達状態更新</summary>
internal static class ReachingLocalAssignmentAnalysis
{
    /// <summary>単一代入による到達状態の更新</summary>
    /// <param name="analysis">local 到達値解析</param>
    /// <param name="operation">解析対象の operation</param>
    /// <param name="incoming">operation 直前の状態</param>
    /// <param name="result">operation 通過後の状態</param>
    /// <returns>追跡対象への代入の場合に true</returns>
    internal static bool TryAnalyze(
        ReachingLocalValueAnalysis analysis,
        IOperation operation,
        ReachingLocalFlowState incoming,
        out ReachingLocalFlowState result)
    {
        if (operation is IVariableDeclaratorOperation declarator &&
            SymbolEqualityComparer.Default.Equals(declarator.Symbol, analysis.Target))
        {
            result = declarator.Initializer?.Value is { } initializer
                ? new ReachingLocalFlowState([initializer], incoming.CanContinue)
                : incoming;
            return true;
        }

        if (operation is ISimpleAssignmentOperation assignment &&
            ReferencesTargetLocal(analysis.Target, assignment.Target))
        {
            result = new ReachingLocalFlowState([assignment.Value], incoming.CanContinue);
            return true;
        }

        result = incoming;
        return false;
    }

    /// <summary>代入先が追跡対象 local を参照するかの判定</summary>
    /// <param name="target">追跡対象の local</param>
    /// <param name="operation">代入先の operation</param>
    /// <returns>追跡対象 local の場合に true</returns>
    private static bool ReferencesTargetLocal(ILocalSymbol target, IOperation operation)
    {
        while (operation is IConversionOperation or IParenthesizedOperation)
        {
            operation = operation switch
            {
                IConversionOperation conversion => conversion.Operand,
                IParenthesizedOperation parenthesized => parenthesized.Operand,
                _ => operation,
            };
        }

        return operation is ILocalReferenceOperation local &&
            SymbolEqualityComparer.Default.Equals(local.Local, target);
    }
}
