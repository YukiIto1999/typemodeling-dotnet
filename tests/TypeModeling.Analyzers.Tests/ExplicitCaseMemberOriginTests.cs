using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>同じ宣言を指す member は一つの位置とし、隠した別の宣言の member は混ぜないこと</summary>
public sealed class ExplicitCaseMemberOriginTests
{
    /// <summary>派生が new で隠す property、派生が override する property、派生の positional parameter が基底の property を指す record の宣言</summary>
    private const string Declarations = """

        internal abstract record Memo
        {
            public PaymentState State { get; init; } = new PaymentState.Pending();
        }

        internal sealed record Reminder : Memo
        {
            public new PaymentState State { get; init; } = new PaymentState.Pending();
        }

        internal abstract record Slip
        {
            public abstract PaymentState State { get; }
        }

        internal sealed record Cheque : Slip
        {
            public override PaymentState State => new PaymentState.Pending();
        }

        internal record Account(PaymentState State);

        internal sealed record Savings(PaymentState State, decimal Rate) : Account(State);
        """;

    /// <summary>派生が new で隠した property は、基底の property と別の位置として、基底の型で書く arm の拒否を残すこと</summary>
    [Test]
    public async Task A_hidden_member_is_not_merged_with_the_member_it_hides()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int Describe(Memo memo) => memo switch
                {
                    Reminder { State: PaymentState.Pending } => 1,
                    { State: var any } => any.Label.Length,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["{ State: var any }"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>override した property と基底の property を、同じ仮想 slot の一つの位置として許可すること</summary>
    [Test]
    public async Task An_overriding_member_and_the_member_it_overrides_are_one_position()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int Describe(Slip slip) => slip switch
                {
                    Cheque { State: PaymentState.Pending } => 1,
                    { State: PaymentState.Settled or PaymentState.Failed } => 2,
                    { State: PaymentState.Pending } => 3,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>派生 record の positional parameter が指す基底の property を、基底の型の property pattern と一つの位置として許可すること</summary>
    [Test]
    public async Task A_derived_record_slot_and_the_inherited_property_are_one_position()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int Describe(Account account) => account switch
                {
                    Savings(PaymentState.Pending, _) => 1,
                    { State: PaymentState.Settled or PaymentState.Failed } => 2,
                    { State: PaymentState.Pending } => 3,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
