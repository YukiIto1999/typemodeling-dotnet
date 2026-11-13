using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>同じ値を指す positional と property の位置を一つとし、別の値を指す位置は混ぜないこと</summary>
public sealed class ExplicitCaseMemberIdentityTests
{
    /// <summary>record の positional parameter、Deconstruct が別の値を返す class、閉じた型を部分値に持つ record の宣言</summary>
    private const string Declarations = """

        [Closed]
        public abstract record Outcome
        {
            private Outcome() { }
            public sealed record Done(PaymentState State) : Outcome;
            public sealed record Skipped : Outcome;
        }

        internal sealed record Receipt(PaymentState State, int Count);

        internal sealed class Ledger
        {
            public PaymentState State { get; init; } = new PaymentState.Pending();
            public PaymentState Previous { get; init; } = new PaymentState.Pending();
            public void Deconstruct(out PaymentState state) => state = Previous;
        }
        """;

    /// <summary>record の positional parameter と同名の property は、どちらの書き方が先でも同じ位置として守ること</summary>
    [Test]
    public async Task A_record_slot_and_its_property_are_one_position_in_either_order()
    {
        var diagnostics = await RunClosed(
            Payment()
                + Declarations
                + """

                internal static class Handler
                {
                    internal static int PropertyAfterSlot(Outcome outcome) => outcome switch
                    {
                        Outcome.Done(PaymentState.Pending) => 1,
                        Outcome.Done { State: var state } => state.Label.Length,
                        Outcome.Skipped => 0,
                    };

                    internal static int SlotAfterProperty(Outcome outcome) => outcome switch
                    {
                        Outcome.Done { State: PaymentState.Pending } => 1,
                        Outcome.Done(var state) => state.Label.Length,
                        Outcome.Skipped => 0,
                    };
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["var state", "var state"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>別の member だけを書く pattern が、他の arm が分岐した守る中身を書かずに受け取るときの拒否</summary>
    [Test]
    public async Task An_unrelated_written_member_does_not_hide_an_omitted_active_payload()
    {
        var diagnostics = await RunClosed(
            Payment()
                + Declarations
                + """

                internal static class Handler
                {
                    internal static int PropertyOmitsSlot(Receipt receipt) => receipt switch
                    {
                        Receipt(PaymentState.Pending, _) => 1,
                        { Count: 0 } => 2,
                        Receipt(PaymentState.Settled, _) => 3,
                        Receipt(PaymentState.Failed, _) => 4,
                    };

                    internal static int SlotWritesPropertyBroadly(Receipt receipt) => receipt switch
                    {
                        { State: PaymentState.Pending } => 1,
                        Receipt(_, 0) => 2,
                        { State: PaymentState.Settled } => 3,
                        { State: PaymentState.Failed } => 4,
                    };
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["{ Count: 0 }", "_"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>自分で書いた Deconstruct が返す値は、同名の property と混ぜず、守る位置を書かない pattern を拒否すること</summary>
    [Test]
    public async Task A_custom_deconstruct_slot_is_never_merged_with_a_similarly_named_property()
    {
        var diagnostics = await RunClosed(
            Payment()
                + Declarations
                + """

                internal static class Handler
                {
                    internal static int PropertyOmitsSlot(Ledger ledger) => ledger switch
                    {
                        Ledger(PaymentState.Pending) => 1,
                        { State: var current } => current.Label.Length,
                    };

                    internal static int SlotOmitsProperty(Ledger ledger) => ledger switch
                    {
                        { State: PaymentState.Pending } => 1,
                        Ledger(var shown) => shown.Label.Length,
                    };

                    internal static int Untouched(Ledger ledger) => ledger switch
                    {
                        Ledger(var shown) => shown.Label.Length,
                    };
                }
                """
        );

        await Assert
            .That(Flagged(diagnostics))
            .IsEquivalentTo(["{ State: var current }", "Ledger(var shown)"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>両方の書き方で case を挙げる arm、分岐しない位置、guard の arm、別の case の arm の許可</summary>
    [Test]
    public async Task Mixed_forms_are_accepted_where_every_active_position_is_written_or_exempt()
    {
        var diagnostics = await RunClosed(
            Payment()
                + Declarations
                + """

                internal static class Handler
                {
                    internal static int SlotThenProperty(Outcome outcome) => outcome switch
                    {
                        Outcome.Done(PaymentState.Pending) => 1,
                        Outcome.Done { State: PaymentState.Settled or PaymentState.Failed } => 2,
                        Outcome.Skipped => 0,
                    };

                    internal static int PropertyThenSlot(Outcome outcome) => outcome switch
                    {
                        Outcome.Done { State: PaymentState.Pending } => 1,
                        Outcome.Done(PaymentState.Settled or PaymentState.Failed) => 2,
                        Outcome.Skipped => 0,
                    };

                    internal static int UntouchedProperty(Outcome outcome) => outcome switch
                    {
                        Outcome.Done { State: var state } => state.Label.Length,
                        Outcome.Skipped => 0,
                    };

                    internal static int UntouchedMember(Receipt receipt) => receipt switch
                    {
                        { Count: 0 } => 1,
                        { Count: var count } => count,
                    };

                    internal static int Guarded(Outcome outcome, bool flag) => outcome switch
                    {
                        Outcome.Done(PaymentState.Pending) => 1,
                        Outcome.Done { State: var state } when flag => state.Label.Length,
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
