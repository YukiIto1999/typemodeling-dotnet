using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Diagnostics.ClosedUnionSwitchStatement;

/// <summary>switch 値へ到達する閉じた値の由来追跡</summary>
internal static class ClosedOriginFinder
{
    /// <summary>operation の閉じた値の由来</summary>
    /// <param name="operation">追跡対象の operation</param>
    /// <param name="controlFlowGraph">到達値解析の制御フロー</param>
    /// <returns>到達可能な閉集合</returns>
    internal static ITypeSymbol? Find(
        IOperation operation,
        ControlFlowGraph controlFlowGraph) =>
        Find(operation, controlFlowGraph, new HashSet<LocalUse>(LocalUseComparer.Instance));

    /// <summary>訪問済み local use を引き継ぐ由来追跡</summary>
    /// <param name="operation">追跡対象の operation</param>
    /// <param name="controlFlowGraph">到達値解析の制御フロー</param>
    /// <param name="visited">訪問済み local use</param>
    /// <returns>到達可能な閉集合</returns>
    private static ITypeSymbol? Find(
        IOperation operation,
        ControlFlowGraph controlFlowGraph,
        HashSet<LocalUse> visited)
    {
        while (operation is IParenthesizedOperation parenthesized)
            operation = parenthesized.Operand;

        if (ClosedTypeSet.Canonical(operation.Type) is { } direct)
            return direct;

        if (operation is IConversionOperation conversion &&
            ValuePreservingWidening.Is(conversion))
        {
            return Find(conversion.Operand, controlFlowGraph, visited);
        }

        if (operation is ILocalReferenceOperation local)
            return FindLocalOrigin(local, controlFlowGraph, visited);

        if (operation is IConditionalOperation conditional)
        {
            return Find(conditional.WhenTrue, controlFlowGraph, visited) ??
                (conditional.WhenFalse is { } whenFalse
                    ? Find(whenFalse, controlFlowGraph, visited)
                    : null);
        }

        if (operation is ICoalesceOperation coalesce)
        {
            return Find(coalesce.Value, controlFlowGraph, visited) ??
                Find(coalesce.WhenNull, controlFlowGraph, visited);
        }

        if (operation is ISwitchExpressionOperation switchExpression)
        {
            foreach (var arm in switchExpression.Arms)
            {
                if (Find(
                    arm.Value,
                    controlFlowGraph,
                    new HashSet<LocalUse>(visited, LocalUseComparer.Instance)) is { } origin)
                {
                    return origin;
                }
            }
        }

        return null;
    }

    /// <summary>local reference へ到達する閉じた値の由来</summary>
    /// <param name="local">追跡対象の local reference</param>
    /// <param name="controlFlowGraph">到達値解析の制御フロー</param>
    /// <param name="visited">訪問済み local use</param>
    /// <returns>到達可能な閉集合</returns>
    private static ITypeSymbol? FindLocalOrigin(
        ILocalReferenceOperation local,
        ControlFlowGraph controlFlowGraph,
        HashSet<LocalUse> visited)
    {
        var use = new LocalUse(local.Local, local.Syntax.SpanStart);
        if (!visited.Add(use))
            return null;

        var reachingValues = new ReachingLocalValueAnalysis(
            local.Local,
            local.Syntax.SpanStart).Analyze(controlFlowGraph);
        foreach (var value in reachingValues)
        {
            var nextVisited = new HashSet<LocalUse>(visited, LocalUseComparer.Instance);
            if (Find(value, controlFlowGraph, nextVisited) is { } origin)
                return origin;
        }

        return null;
    }

    /// <summary>参照位置を含む local use</summary>
    /// <param name="Local">参照対象の local</param>
    /// <param name="Position">参照位置</param>
    private readonly record struct LocalUse(ILocalSymbol Local, int Position);

    /// <summary>symbol identity に基づく local use 比較</summary>
    private sealed class LocalUseComparer : IEqualityComparer<LocalUse>
    {
        /// <summary>共有 comparer</summary>
        internal static LocalUseComparer Instance { get; } = new();

        /// <summary>local use の同一性判定</summary>
        /// <param name="x">左辺の local use</param>
        /// <param name="y">右辺の local use</param>
        /// <returns>同一 use の場合に true</returns>
        public bool Equals(LocalUse x, LocalUse y) =>
            x.Position == y.Position && SymbolEqualityComparer.Default.Equals(x.Local, y.Local);

        /// <summary>local use の hash code</summary>
        /// <param name="obj">hash 対象の local use</param>
        /// <returns>symbol identity を含む hash code</returns>
        public int GetHashCode(LocalUse obj) =>
            unchecked((SymbolEqualityComparer.Default.GetHashCode(obj.Local) * 397) ^ obj.Position);
    }
}
