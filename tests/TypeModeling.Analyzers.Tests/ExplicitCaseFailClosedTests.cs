using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>not と list pattern の内側の閉じた型の位置を、後から足す case を受け取らないと確かめられないものとして拒否すること</summary>
public sealed class ExplicitCaseFailClosedTests
{
    /// <summary>not の内側にある位置は、分岐する位置なら検証できないので挙げた派生も含めて拒否し、分岐しない位置は許すこと</summary>
    [Test]
    public async Task A_branched_position_beneath_not_cannot_be_verified_and_is_rejected()
    {
        var diagnostics = await RunClosed(
            Payment()
                + """

                public sealed record Holder(PaymentState State);

                internal static class Handler
                {
                    internal static int Negated(Holder holder) => holder switch
                    {
                        not Holder(PaymentState.Pending) => 1,
                        Holder(PaymentState.Pending) => 2,
                    };

                    internal static int Untouched(Holder holder, int number) => (holder, number) switch
                    {
                        (not Holder(_), 1) => 1,
                        _ => 2,
                    };
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["PaymentState.Pending"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>list pattern の要素の閉じた型は、分岐する位置なら拒否し、閉じた型を含まない list は許すこと</summary>
    [Test]
    public async Task A_branched_list_element_cannot_be_verified_and_is_rejected()
    {
        var diagnostics = await RunClosed(
            Payment()
                + """

                internal static class Handler
                {
                    internal static int Items(PaymentState[] items) => items switch
                    {
                        [PaymentState.Pending, ..] => 1,
                        [var first, ..] => first.Label.Length,
                        _ => 0,
                    };

                    internal static int Numbers(int[] items) => items switch
                    {
                        [1, ..] => 1,
                        [var first, ..] => first,
                        _ => 0,
                    };

                    internal static int Slices(PaymentState[] items) => items switch
                    {
                        [.. [PaymentState.Pending]] => 1,
                        [.. [var only]] => only.Label.Length,
                        _ => 0,
                    };
                }
                """
        );

        await Assert
            .That(Flagged(diagnostics))
            .IsEquivalentTo([
                "PaymentState.Pending",
                "var first",
                "PaymentState.Pending",
                "var only",
            ]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>閉じた型の位置に当てた list pattern を、case を挙げていないものとして拒否すること</summary>
    [Test]
    public async Task A_list_pattern_on_a_closed_position_is_rejected()
    {
        var diagnostics = await RunClosed(
            """
            using TypeModeling.Domain;

            namespace Probe;

            [Closed]
            public abstract record Bag
            {
                private Bag() { }
                public int Count => 0;
                public int this[int index] => index;
                public sealed record Empty : Bag;
            }

            internal static class Handler
            {
                internal static int Size(Bag bag) => bag switch
                {
                    Bag.Empty => 0,
                    [..] => 1,
                };
            }
            """
        );

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["[..]"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
