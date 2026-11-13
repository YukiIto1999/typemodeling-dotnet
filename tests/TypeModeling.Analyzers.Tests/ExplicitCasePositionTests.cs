using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>switch 入力と case 内部の位置ごとの閉じた型の保護</summary>
public sealed partial class ExplicitCasePositionTests
{
    /// <summary>決済の状態を中身に持つ閉じた record の宣言</summary>
    private const string OutcomeDeclaration = """

        [Closed]
        public abstract record Outcome
        {
            private Outcome() { }
            public sealed record Done(PaymentState State) : Outcome;
            public sealed record Skipped : Outcome;
        }
        """;

    /// <summary>case を明示した位置だけに適用する中身の閉じた型の保護</summary>
    /// <returns>診断対象と compiler error の比較完了</returns>
    [Test]
    public async Task A_payload_position_is_protected_only_where_a_case_is_named()
    {
        var diagnostics = await RunClosed(
            Payment()
                + OutcomeDeclaration
                + """


                internal static class Handler
                {
                    internal static int Untouched(Outcome outcome) => outcome switch
                    {
                        Outcome.Done(var state) => state.Label.Length,
                        Outcome.Skipped => 0,
                    };

                    internal static int Branched(Outcome outcome) => outcome switch
                    {
                        Outcome.Done(PaymentState.Pending) => 1,
                        Outcome.Done(var other) => other.Label.Length,
                        Outcome.Skipped => 0,
                    };

                    internal static int Explicit(Outcome outcome) => outcome switch
                    {
                        Outcome.Done(PaymentState.Pending) => 1,
                        Outcome.Done(PaymentState.Settled) => 2,
                        Outcome.Done(PaymentState.Failed) => 3,
                        Outcome.Skipped => 0,
                    };
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["var other"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>式と戻り値と入れ子の組の各要素にある閉じた型の保護</summary>
    /// <returns>診断対象と compiler error の比較完了</returns>
    [Test]
    public async Task Every_closed_element_of_a_tuple_input_is_protected()
    {
        var diagnostics = await RunClosed(
            Payment()
                + """

                internal static class Handler
                {
                    internal static int Complete(PaymentState left, PaymentState right) => (left, right) switch
                    {
                        (PaymentState.Pending, PaymentState.Pending or PaymentState.Settled or PaymentState.Failed) => 1,
                        (PaymentState.Settled or PaymentState.Failed, PaymentState.Pending or PaymentState.Settled or PaymentState.Failed) => 2,
                    };

                    internal static int Partial(PaymentState left, PaymentState right) => (left, right) switch
                    {
                        (PaymentState.Pending, _) => 1,
                        (PaymentState.Settled, PaymentState.Failed) => 2,
                        (var other, PaymentState.Pending) => 3,
                    };

                    internal static (PaymentState, bool) Current() => (new PaymentState.Pending(), true);

                    internal static int Returned() => Current() switch
                    {
                        (_, true) => 1,
                        (_, false) => 2,
                    };

                    internal static int Nested(PaymentState left, PaymentState right, int number) => ((left, right), number) switch
                    {
                        ((PaymentState.Pending, _), 1) => 1,
                        ((var other, PaymentState.Pending), _) => 2,
                        _ => 3,
                    };
                }
                """
        );

        await Assert
            .That(Flagged(diagnostics))
            .IsEquivalentTo(["_", "var other", "_", "_", "_", "var other", "_"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>開いた型の switch に残る fallback の許可</summary>
    /// <returns>診断対象と compiler error の比較完了</returns>
    [Test]
    public async Task Open_types_keep_their_fallback_arms()
    {
        var diagnostics = await RunClosed(
            """
            namespace Probe;

            public abstract record Open;
            public sealed record Leaf : Open;

            public enum Plain { None, Some }

            internal static class Handler
            {
                internal static int Records(Open value) => value switch { Leaf => 1, _ => 0 };

                internal static int Objects(object value) => value switch { int => 1, string => 2, _ => 0 };

                internal static int Enums(Plain value) => value switch { Plain.None => 1, _ => 0 };

                internal static int Statement(Open value)
                {
                    switch (value)
                    {
                        case Leaf: return 1;
                        default: return 0;
                    }
                }
            }
            """
        );

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>閉じた class を制約とする型引数の catch-all の拒否</summary>
    /// <returns>診断対象と compiler error の比較完了</returns>
    [Test]
    public async Task A_type_parameter_constrained_to_a_closed_class_is_protected()
    {
        var diagnostics = await RunClosed(
            Payment()
                + """

                internal static class Handler
                {
                    internal static int Describe<TState>(TState state) where TState : PaymentState => state switch
                    {
                        PaymentState.Pending => 1,
                        TState other => other.Label.Length,
                    };
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["TState other"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
