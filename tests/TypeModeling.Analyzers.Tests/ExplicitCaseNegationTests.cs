using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>閉じた型の未来の case に対する否定 pattern の受理範囲</summary>
public sealed class ExplicitCaseNegationTests
{
    /// <summary>位置 pattern の否定が未来の payload を受け取る場合の拒否</summary>
    /// <returns>診断の検査完了</returns>
    [Test]
    public async Task Negated_deconstruction_can_accept_a_future_case_with_a_different_payload()
    {
        var diagnostics = await RunClosed(
            """
            using TypeModeling.Domain;

            [Closed]
            public abstract record State(int Code)
            {
                public sealed record Ready() : State(0);
                public sealed record Failed() : State(1);
            }

            internal static class Handler
            {
                internal static int Describe(State state) => state switch
                {
                    not State(0) => 1,
                    State.Ready => 2,
                };
            }
            """
        );

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["not State(0)"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>閉じた型の全ての非 null 値を除く否定 pattern の許可</summary>
    /// <param name="nullPattern">非 null の全ての case を除く pattern</param>
    /// <returns>診断の検査完了</returns>
    [Test]
    [Arguments("not { }")]
    [Arguments("not System.IEquatable<PaymentState>")]
    public async Task Negating_a_test_shared_by_every_case_only_accepts_null(string nullPattern)
    {
        var diagnostics = await RunClosed(
            Payment()
                + $$"""

                internal static class Handler
                {
                    internal static int Describe(PaymentState? state) => state switch
                    {
                        {{nullPattern}} => 0,
                        PaymentState.Pending => 1,
                        PaymentState.Settled => 2,
                        PaymentState.Failed => 3,
                    };
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
