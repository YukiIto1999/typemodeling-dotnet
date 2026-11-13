using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>extended property pattern と入れ子の property pattern を、同じ member の連なりとして同じ位置にすること</summary>
public sealed class ExplicitCaseExtendedPropertyTests
{
    /// <summary>決済の状態を入れ子の property に持つ record の宣言</summary>
    private const string Declarations = """

        internal sealed record Receipt(PaymentState State, int Count);

        internal sealed record Envelope(Receipt Inner);

        internal sealed record Holder(Envelope Outer);
        """;

    /// <summary>extended property pattern と入れ子の property pattern を書き分ける arm は、守る case を全て挙げれば順序によらず許可すること</summary>
    [Test]
    public async Task An_extended_and_a_nested_property_pattern_name_one_position()
    {
        var diagnostics = await RunClosed(
            Payment()
                + Declarations
                + """

                internal static class Handler
                {
                    internal static int ExtendedFirst(Envelope envelope) => envelope switch
                    {
                        { Inner.State: PaymentState.Pending } => 1,
                        { Inner: { State: PaymentState.Settled or PaymentState.Failed } } => 2,
                    };

                    internal static int NestedFirst(Envelope envelope) => envelope switch
                    {
                        { Inner: { State: PaymentState.Pending } } => 1,
                        { Inner.State: PaymentState.Settled or PaymentState.Failed } => 2,
                    };

                    internal static int Deep(Holder holder) => holder switch
                    {
                        { Outer.Inner.State: PaymentState.Pending } => 1,
                        { Outer: { Inner: { State: PaymentState.Settled or PaymentState.Failed } } } => 2,
                    };
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>入れ子の property pattern が case を挙げた位置への extended property pattern の catch-all は、書いた位置で拒否すること</summary>
    [Test]
    public async Task A_catch_all_through_an_extended_property_pattern_is_rejected_where_it_is_written()
    {
        var diagnostics = await RunClosed(
            Payment()
                + Declarations
                + """

                internal static class Handler
                {
                    internal static int Shallow(Envelope envelope) => envelope switch
                    {
                        { Inner: { State: PaymentState.Pending } } => 1,
                        { Inner.State: var any } => any.Label.Length,
                    };

                    internal static int Deep(Holder holder) => holder switch
                    {
                        { Outer: { Inner: { State: PaymentState.Pending } } } => 1,
                        { Outer.Inner.State: var any } => any.Label.Length,
                    };
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["var any", "var any"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
