using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>組の要素を、位置と要素名と ItemN のどの書き方でも同じ位置として守ること</summary>
public sealed class ExplicitCaseTupleAliasTests
{
    /// <summary>組を property に持つ record の宣言</summary>
    private const string Declarations = """

        internal sealed record Holder((PaymentState State, int Count) Pair);
        """;

    /// <summary>入力の組の要素を、位置と要素名と ItemN で書き分ける arm は、守る要素を全て書けば許可すること</summary>
    [Test]
    public async Task A_tuple_input_element_written_by_position_name_or_item_is_one_position()
    {
        var diagnostics = await RunClosed(Payment() + """

            internal static class Handler
            {
                internal static int ByName((PaymentState State, int Count) pair) => pair switch
                {
                    { State: PaymentState.Pending } => 1,
                    { Item1: PaymentState.Settled or PaymentState.Failed } => 2,
                };

                internal static int PositionThenName((PaymentState State, int Count) pair) => pair switch
                {
                    (PaymentState.Pending, _) => 1,
                    { State: PaymentState.Settled or PaymentState.Failed } => 2,
                };

                internal static int ItemThenPosition((PaymentState State, int Count) pair) => pair switch
                {
                    { Item1: PaymentState.Pending } => 1,
                    (PaymentState.Settled or PaymentState.Failed, _) => 2,
                };

                internal static int BothElements((PaymentState First, PaymentState Second) pair) => pair switch
                {
                    { First: PaymentState.Pending, Item2: PaymentState.Pending or PaymentState.Settled or PaymentState.Failed } => 1,
                    { Item1: PaymentState.Settled or PaymentState.Failed, Second: PaymentState.Pending or PaymentState.Settled or PaymentState.Failed } => 2,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>property が持つ組の要素を、位置と要素名と ItemN で書き分ける arm の許可</summary>
    [Test]
    public async Task A_tuple_element_reached_through_a_property_is_one_position_in_every_form()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int PositionThenName(Holder holder) => holder switch
                {
                    { Pair: (PaymentState.Pending, _) } => 1,
                    { Pair: { State: PaymentState.Settled or PaymentState.Failed } } => 2,
                };

                internal static int ItemThenPosition(Holder holder) => holder switch
                {
                    { Pair: { Item1: PaymentState.Pending } } => 1,
                    { Pair: (PaymentState.Settled or PaymentState.Failed, _) } => 2,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>同じ要素への別の書き方の catch-all は、書いた位置で拒否すること</summary>
    [Test]
    public async Task A_catch_all_through_a_tuple_element_alias_is_rejected_where_it_is_written()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int ItemAfterPosition((PaymentState State, int Count) pair) => pair switch
                {
                    (PaymentState.Pending, _) => 1,
                    { Item1: var other } => other.Label.Length,
                };

                internal static int NameAfterItem((PaymentState State, int Count) pair) => pair switch
                {
                    { Item1: PaymentState.Pending } => 1,
                    { State: var other } => other.Label.Length,
                };

                internal static int ThroughProperty(Holder holder) => holder switch
                {
                    { Pair: (PaymentState.Pending, _) } => 1,
                    { Pair: { Item1: var other } } => other.Label.Length,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["var other", "var other", "var other"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
