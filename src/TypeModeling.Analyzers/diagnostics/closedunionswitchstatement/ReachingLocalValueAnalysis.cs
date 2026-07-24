using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.Diagnostics.ClosedUnionSwitchStatement;

/// <summary>参照位置より前の local 到達値解析</summary>
internal sealed class ReachingLocalValueAnalysis
{
    /// <summary>追跡対象の local</summary>
    private readonly ILocalSymbol _target;

    /// <summary>解析を打ち切る参照位置</summary>
    private readonly int _beforePosition;

    /// <summary>try body から catch へ届く throw 状態</summary>
    private readonly Stack<List<ReachingLocalFlowState>> _throwCollectors = new();

    /// <summary>local 到達値解析の構築</summary>
    /// <param name="target">追跡対象の local</param>
    /// <param name="beforePosition">解析を打ち切る参照位置</param>
    internal ReachingLocalValueAnalysis(ILocalSymbol target, int beforePosition)
    {
        _target = target;
        _beforePosition = beforePosition;
    }

    /// <summary>追跡対象の local</summary>
    internal ILocalSymbol Target => _target;

    /// <summary>解析を打ち切る参照位置</summary>
    internal int BeforePosition => _beforePosition;

    /// <summary>制御フロー全体から求めた到達値</summary>
    /// <param name="controlFlowGraph">解析対象の制御フロー</param>
    /// <returns>参照位置へ到達可能な値</returns>
    internal List<IOperation> Analyze(ControlFlowGraph controlFlowGraph) =>
        AnalyzeOperation(
            controlFlowGraph.OriginalOperation,
            new ReachingLocalFlowState([], canContinue: true)).Values;

    /// <summary>operation 通過後の到達状態</summary>
    /// <param name="operation">解析対象の operation</param>
    /// <param name="incoming">operation 直前の状態</param>
    /// <returns>operation 通過後の状態</returns>
    internal ReachingLocalFlowState AnalyzeOperation(
        IOperation operation,
        ReachingLocalFlowState incoming)
    {
        if (operation.Syntax.SpanStart >= _beforePosition)
            return incoming;

        if (operation is IAnonymousFunctionOperation anonymous)
        {
            return anonymous.Syntax.Span.Contains(_beforePosition)
                ? AnalyzeOperation(anonymous.Body, incoming)
                : incoming;
        }

        if (operation is ILocalFunctionOperation localFunction)
        {
            return localFunction.Syntax.Span.Contains(_beforePosition) &&
                localFunction.Body is { } body
                    ? AnalyzeOperation(body, incoming)
                    : incoming;
        }

        if (ReachingLocalAssignmentAnalysis.TryAnalyze(this, operation, incoming, out var result))
            return result;
        if (ReachingLocalControlTransferAnalysis.TryAnalyze(
            this,
            operation,
            incoming,
            out result))
        {
            return result;
        }

        if (operation is IConditionalOperation conditional)
            return ReachingLocalBranchAnalysis.AnalyzeConditional(this, conditional, incoming);
        if (operation is ISwitchOperation switchOperation)
            return ReachingLocalBranchAnalysis.AnalyzeSwitch(this, switchOperation, incoming);
        if (operation is ITryOperation tryOperation)
            return ReachingLocalBranchAnalysis.AnalyzeTry(this, tryOperation, incoming);
        if (operation is IForLoopOperation forLoop)
            return ReachingLocalLoopAnalysis.AnalyzeFor(this, forLoop, incoming);
        if (operation is ILoopOperation loop)
            return ReachingLocalLoopAnalysis.AnalyzeLoop(this, loop, incoming);

        var state = incoming;
        foreach (var child in operation.ChildOperations)
        {
            if (child.Syntax.SpanStart >= _beforePosition || !state.CanContinue)
                continue;
            state = AnalyzeOperation(child, state);
        }

        return state;
    }

    /// <summary>operation 列通過後の到達状態</summary>
    /// <param name="operations">解析対象の operation 列</param>
    /// <param name="incoming">operation 列直前の状態</param>
    /// <returns>operation 列通過後の状態</returns>
    internal ReachingLocalFlowState AnalyzeSequence(
        ImmutableArray<IOperation> operations,
        ReachingLocalFlowState incoming)
    {
        var state = incoming;
        foreach (var operation in operations)
        {
            if (!state.CanContinue || operation.Syntax.SpanStart >= _beforePosition)
                break;
            state = AnalyzeOperation(operation, state);
        }

        return state;
    }

    /// <summary>try body の throw 状態収集開始</summary>
    /// <param name="collector">throw 状態の追加先</param>
    internal void PushThrowCollector(List<ReachingLocalFlowState> collector) =>
        _throwCollectors.Push(collector);

    /// <summary>try body の throw 状態収集終了</summary>
    internal void PopThrowCollector() => _throwCollectors.Pop();

    /// <summary>現在の try body における throw 状態の記録</summary>
    /// <param name="state">throw 直前の状態</param>
    internal void CollectThrow(ReachingLocalFlowState state)
    {
        if (_throwCollectors.Count > 0)
            _throwCollectors.Peek().Add(state);
    }
}
