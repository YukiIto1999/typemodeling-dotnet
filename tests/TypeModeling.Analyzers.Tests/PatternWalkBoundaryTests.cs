using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExhaustivenessAssertions;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>部分 pattern の経路による診断境界の検証</summary>
public sealed class PatternWalkBoundaryTests
{
    /// <summary>slice の戻り値に当てる閉じた case の検証不能な分岐の拒否</summary>
    /// <returns>検証の完了</returns>
    [Test]
    public async Task A_named_case_on_a_closed_slice_result_is_rejected()
    {
        var diagnostics = await RunClosed(
            """
            using TypeModeling.Domain;

            namespace Probe;

            [Closed]
            public abstract record PaymentQueue
            {
                private PaymentQueue() { }
                public int Length => 1;
                public int this[int index] => index;
                public PaymentQueue Slice(int start, int length) => this;
                public sealed record Pending : PaymentQueue;
            }

            internal static class Handler
            {
                internal static int Describe(PaymentQueue queue) => queue switch
                {
                    [.. PaymentQueue.Pending] => 1,
                    _ => 0,
                };
            }
            """
        );

        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
        await Assert.That(Flagged(diagnostics)).Contains("PaymentQueue.Pending");
    }

    /// <summary>入れ子の property に当てた閉じた case の明示と fallback の区別</summary>
    /// <returns>検証の完了</returns>
    [Test]
    public async Task Nested_properties_keep_named_cases_safe_and_reject_their_fallback()
    {
        var diagnostics = await RunClosed(
            Payment()
                + """

                internal sealed record Payload(PaymentState State);
                internal sealed record Envelope(Payload Payload);

                internal static class Handler
                {
                    internal static int Describe(Envelope envelope) => envelope switch
                    {
                        { Payload: { State: PaymentState.Pending } } => 1,
                        { Payload: { State: var state } } => state.Label.Length,
                    };
                }
                """
        );

        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["var state"]);
    }

    /// <summary>not の内側にある property の検証不能な分岐の拒否</summary>
    /// <returns>検証の完了</returns>
    [Test]
    public async Task Negated_properties_keep_their_descendants_opaque()
    {
        var diagnostics = await RunClosed(
            Payment()
                + """

                internal sealed record Payload(PaymentState State);
                internal sealed record Envelope(Payload Payload);

                internal static class Handler
                {
                    internal static int Describe(Envelope envelope) => envelope switch
                    {
                        not { Payload: { State: PaymentState.Pending } } => 1,
                        { Payload: { State: PaymentState.Pending } } => 2,
                    };
                }
                """
        );

        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["PaymentState.Pending"]);
    }

    /// <summary>list の内部を仮想の組の経路に変換しない閉じた型の網羅判定</summary>
    /// <returns>検証の完了</returns>
    [Test]
    public Task An_opaque_list_payload_does_not_invalidate_closed_root_exhaustiveness() =>
        AssertExhaustive(
            Payment()
                + """

                [Closed]
                public abstract record Delivery
                {
                    private Delivery() { }
                    public sealed record Batch(PaymentState[] States) : Delivery;
                    public sealed record Single : Delivery;
                }

                internal static class Handler
                {
                    internal static int Describe(Delivery delivery) => delivery switch
                    {
                        Delivery.Batch { States: [PaymentState.Pending, ..] } => 1,
                        Delivery.Batch => 2,
                        Delivery.Single => 3,
                    };
                }
                """
        );
}
