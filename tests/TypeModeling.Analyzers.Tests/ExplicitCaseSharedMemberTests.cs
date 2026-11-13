using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>基底の型が持つ member を、重なる型で書く pattern は一つの位置とし、重ならない派生の型で書く pattern は混ぜないこと</summary>
public sealed class ExplicitCaseSharedMemberTests
{
    /// <summary>決済の状態を共有の property に持つ閉じていない基底 record と、重ならない二つの派生の宣言</summary>
    private const string Declarations = """

        internal abstract record Document
        {
            public PaymentState State { get; init; } = new PaymentState.Pending();
        }

        internal sealed record Letter : Document;

        internal sealed record Crate : Document;
        """;

    /// <summary>派生の型と基底の型で同じ property を書く arm は、守る case を全て挙げれば書く順によらず許可すること</summary>
    [Test]
    public async Task A_member_written_through_a_derived_and_a_base_type_is_one_position_in_either_order()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int DerivedFirst(Document document) => document switch
                {
                    Letter { State: PaymentState.Pending } => 1,
                    { State: PaymentState.Settled or PaymentState.Failed } => 2,
                    { State: PaymentState.Pending } => 3,
                };

                internal static int BaseFirst(Document document) => document switch
                {
                    { State: PaymentState.Settled or PaymentState.Failed } => 2,
                    Letter { State: PaymentState.Pending } => 1,
                    { State: PaymentState.Pending } => 3,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>派生の型が case を挙げた member を、基底の型で書く catch-all の arm が受け取るときの、書いた位置での拒否と guard の免除</summary>
    [Test]
    public async Task A_catch_all_through_a_base_type_is_rejected_where_a_derived_type_names_the_cases()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int Describe(Document document) => document switch
                {
                    Letter { State: PaymentState.Pending } => 1,
                    { State: var any } => any.Label.Length,
                };

                internal static int Guarded(Document document, bool flag) => document switch
                {
                    Letter { State: PaymentState.Pending } => 1,
                    { State: var any } when flag => any.Label.Length,
                    { State: PaymentState.Settled or PaymentState.Failed } => 2,
                    { State: PaymentState.Pending } => 3,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["var any"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>重ならない派生の型で書く同じ member は、別の位置として守る範囲を混ぜないこと</summary>
    [Test]
    public async Task A_member_written_through_unrelated_derived_types_stays_two_positions()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int NamedThenBound(Document document) => document switch
                {
                    Letter { State: PaymentState.Pending } => 1,
                    Letter { State: PaymentState.Settled or PaymentState.Failed } => 2,
                    Crate { State: var any } => any.Label.Length,
                };

                internal static int BoundThenNamed(Document document) => document switch
                {
                    Crate { State: PaymentState.Pending } => 1,
                    Letter { State: var any } => any.Label.Length,
                    Crate { State: PaymentState.Settled or PaymentState.Failed } => 2,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
