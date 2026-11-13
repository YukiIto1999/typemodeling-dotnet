using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>組の入力の全体に当てる arm が、組の閉じた要素を書かずに受け取ることの拒否</summary>
public sealed class ExplicitCaseTupleRootTests
{
    /// <summary>派生に中身を持たせる、閉じた record の宣言</summary>
    private const string OutcomeDeclaration = """

        [Closed]
        public abstract record Outcome
        {
            private Outcome() { }
            public sealed record Done(PaymentState State) : Outcome;
            public sealed record Skipped : Outcome;
        }
        """;

    /// <summary>組の入力の全体に当てる discard と束縛が、組の閉じた要素を隠すときの拒否</summary>
    [Test]
    public async Task A_broad_arm_over_a_tuple_input_is_rejected_where_it_hides_a_protected_element()
    {
        var diagnostics = await RunClosed(Payment() + """

            internal static class Handler
            {
                internal static int Discard(PaymentState state, bool flag) => (state, flag) switch { _ => 0 };

                internal static int Bound(PaymentState state, bool flag) => (state, flag) switch { var pair => pair.Item2 ? 1 : 0 };

                internal static int After(PaymentState state, bool flag) => (state, flag) switch
                {
                    (PaymentState.Pending, _) => 1,
                    var rest => rest.Item2 ? 2 : 3,
                };

                internal static int Statement(PaymentState state, bool flag)
                {
                    var result = 0;
                    switch ((state, flag))
                    {
                        case var pair: result = pair.Item2 ? 1 : 0; break;
                    }

                    return result;
                }
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["_", "var pair", "var rest", "var pair"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>守る値を下に持たない広い arm、guard の arm、守る要素を全て挙げた組の許可</summary>
    [Test]
    public async Task A_broad_arm_is_accepted_where_no_protected_value_lies_beneath_it()
    {
        var diagnostics = await RunClosed(Payment() + OutcomeDeclaration + """

            internal static class Handler
            {
                internal static int Numbers(int number, bool flag) => (number, flag) switch { _ => 0 };

                internal static int Guarded(PaymentState state, bool flag) => (state, flag) switch
                {
                    _ when flag => 9,
                    (PaymentState.Pending, _) => 1,
                    (PaymentState.Settled, _) => 2,
                    (PaymentState.Failed, _) => 3,
                };

                internal static int Complete(PaymentState state, bool flag) => (state, flag) switch
                {
                    (PaymentState.Pending, _) => 1,
                    (PaymentState.Settled or PaymentState.Failed, true) => 2,
                    (PaymentState.Settled or PaymentState.Failed, false) => 3,
                };

                internal static int Unactivated(Outcome outcome, bool flag) => (outcome, flag) switch
                {
                    (Outcome.Done, true) => 1,
                    (Outcome.Done, false) => 2,
                    (Outcome.Skipped, _) => 0,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>入れ子の組に当てる discard が隠す、異なる閉じた型を一つずつ、同じ型は重複なく示すこと</summary>
    [Test]
    public async Task A_broad_arm_over_a_nested_tuple_identifies_each_distinct_family_once()
    {
        var diagnostics = await RunClosed(Payment() + """

            [Closed]
            public enum Delivery { Pending, Complete }

            internal static class Handler
            {
                internal static int Describe(PaymentState first, PaymentState second, Delivery delivery) =>
                    ((first, second), delivery) switch { _ => 0 };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["_", "_"]);
        await Assert.That(ReportedMessages(diagnostics)).IsEquivalentTo(MessagesFor(diagnostics, "PaymentState", "Delivery"));
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
