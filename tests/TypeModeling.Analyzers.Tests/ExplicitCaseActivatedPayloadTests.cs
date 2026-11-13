using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>ある arm が分岐して守った case の中身を、書かずに受け取る祖先の arm の拒否</summary>
public sealed class ExplicitCaseActivatedPayloadTests
{
    /// <summary>決済の状態を中身に持つ、閉じた record と、閉じた型を部分値に持つ閉じていない record の宣言</summary>
    private const string OutcomeDeclaration = """

        [Closed]
        public abstract record Outcome
        {
            private Outcome() { }
            public sealed record Done(PaymentState State) : Outcome;
            public sealed record Skipped : Outcome;
        }

        internal sealed record Envelope(Outcome Result);

        internal sealed record Receipt(PaymentState State, int Count);
        """;

    /// <summary>ある arm が分岐して守った case の中身を、組と property の祖先の広い arm が隠すときの拒否</summary>
    [Test]
    public async Task A_broad_ancestor_arm_hides_a_payload_that_another_arm_branched_on()
    {
        var tuple = await RunClosed(
            Payment()
                + OutcomeDeclaration
                + """

                internal static class Handler
                {
                    internal static int Describe(Outcome outcome, bool flag) => (outcome, flag) switch
                    {
                        (Outcome.Done(PaymentState.Pending), _) => 1,
                        (Outcome.Skipped, _) => 0,
                        _ => 2,
                    };
                }
                """
        );
        var property = await RunClosed(
            Payment()
                + OutcomeDeclaration
                + """

                internal static class Handler
                {
                    internal static int Describe(Envelope envelope) => envelope switch
                    {
                        { Result: Outcome.Done(PaymentState.Pending) } => 1,
                        { Result: Outcome.Skipped } => 0,
                        _ => 2,
                    };
                }
                """
        );

        await Assert.That(Flagged(tuple)).IsEquivalentTo(["_", "_"]);
        await Assert
            .That(ReportedMessages(tuple))
            .IsEquivalentTo(MessagesFor(tuple, "Outcome", "PaymentState"));
        await Assert.That(Flagged(property)).IsEquivalentTo(["_", "_"]);
        await Assert
            .That(ReportedMessages(property))
            .IsEquivalentTo(MessagesFor(property, "Outcome", "PaymentState"));
        await Assert.That(CompilerErrors(tuple)).IsEmpty();
        await Assert.That(CompilerErrors(property)).IsEmpty();
    }

    /// <summary>他の arm が分岐した中身を、書かずに受け取る case の pattern の拒否</summary>
    [Test]
    public async Task A_case_pattern_that_omits_an_activated_payload_is_rejected()
    {
        var diagnostics = await RunClosed(
            Payment()
                + OutcomeDeclaration
                + """

                internal static class Handler
                {
                    internal static int Type(Outcome outcome) => outcome switch
                    {
                        Outcome.Done(PaymentState.Pending) => 1,
                        Outcome.Done => 2,
                        Outcome.Skipped => 0,
                    };

                    internal static int Property(Outcome outcome) => outcome switch
                    {
                        Outcome.Done(PaymentState.Pending) => 1,
                        Outcome.Done { } => 2,
                        Outcome.Skipped => 0,
                    };

                    internal static int Declaration(Outcome outcome) => outcome switch
                    {
                        Outcome.Done(PaymentState.Pending) => 1,
                        Outcome.Done done => done.State.Label.Length,
                        Outcome.Skipped => 0,
                    };

                    internal static int Sibling(Receipt receipt) => receipt switch
                    {
                        { State: PaymentState.Pending } => 1,
                        { Count: 0 } => 2,
                        { State: PaymentState.Settled } => 3,
                        { State: PaymentState.Failed } => 4,
                    };

                    internal static int Nested(Envelope envelope) => envelope switch
                    {
                        { Result: Outcome.Done(PaymentState.Pending) } => 1,
                        { Result: Outcome.Done } => 2,
                        { Result: Outcome.Skipped } => 0,
                    };
                }
                """
        );

        await Assert
            .That(Flagged(diagnostics))
            .IsEquivalentTo([
                "Outcome.Done",
                "Outcome.Done { }",
                "Outcome.Done done",
                "{ Count: 0 }",
                "Outcome.Done",
            ]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>中身に分岐する arm がなければ受け取りを許し、分岐しても中身を書く arm、別の case の arm、guard の arm は許すこと</summary>
    [Test]
    public async Task A_case_pattern_is_accepted_where_the_payload_is_untouched_named_disjoint_or_guarded()
    {
        var diagnostics = await RunClosed(
            Payment()
                + OutcomeDeclaration
                + """

                internal static class Handler
                {
                    internal static int Untouched(Outcome outcome) => outcome switch
                    {
                        Outcome.Done => 1,
                        Outcome.Skipped => 0,
                    };

                    internal static int Bound(Outcome outcome) => outcome switch
                    {
                        Outcome.Done done => done.State.Label.Length,
                        Outcome.Skipped => 0,
                    };

                    internal static int Explicit(Outcome outcome) => outcome switch
                    {
                        Outcome.Done(PaymentState.Pending) => 1,
                        Outcome.Done(PaymentState.Settled) => 2,
                        Outcome.Done(PaymentState.Failed) => 3,
                        Outcome.Skipped => 0,
                    };

                    internal static int Conjunction(Outcome outcome) => outcome switch
                    {
                        Outcome.Done and { State: PaymentState.Pending } => 1,
                        Outcome.Done and { State: PaymentState.Settled or PaymentState.Failed } => 2,
                        Outcome.Skipped => 0,
                    };

                    internal static int Alternatives(Outcome outcome) => outcome switch
                    {
                        Outcome.Done(PaymentState.Pending) or Outcome.Skipped => 1,
                        Outcome.Done(PaymentState.Settled or PaymentState.Failed) => 2,
                    };

                    internal static int Guarded(Outcome outcome, bool flag) => outcome switch
                    {
                        Outcome.Done when flag => 9,
                        Outcome.Done(PaymentState.Pending) => 1,
                        Outcome.Done(PaymentState.Settled) => 2,
                        Outcome.Done(PaymentState.Failed) => 3,
                        Outcome.Skipped => 0,
                    };
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
