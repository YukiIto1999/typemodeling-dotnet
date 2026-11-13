using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>case を挙げて分岐した位置の下を、型を調べるだけの case が受け取るかを、型の重なりを確かめられる範囲で判定すること</summary>
public sealed class ExplicitCaseTypeReachTests
{
    /// <summary>interface、interface を実装しない sealed でない class とそれに interface を足す派生、interface を実装しない sealed な class と値型の宣言</summary>
    private const string Declarations = """

        internal interface IStamped
        {
            PaymentState State { get; }
        }

        internal record Loose
        {
            public PaymentState State { get; init; } = new PaymentState.Pending();
        }

        internal sealed record Joined : Loose, IStamped;

        internal sealed record Stamp
        {
            public PaymentState State { get; init; } = new PaymentState.Pending();
        }

        internal readonly record struct Tag(PaymentState State);
        """;

    /// <summary>interface の property で分岐した後、interface を実装しない sealed でない class の case が、派生の値の中身を隠すときの拒否</summary>
    [Test]
    public async Task A_class_that_a_derived_type_can_join_to_the_interface_is_rejected_where_it_hides_the_payload()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int ClassAfterInterface(object value)
                {
                    var result = 0;
                    switch (value)
                    {
                        case IStamped { State: PaymentState.Pending }: result = 1; break;
                        case Loose: result = 2; break;
                    }

                    return result;
                }
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["Loose"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>派生が足せる class と interface、interface と別の interface のように、同じ値になりうる型の case の拒否</summary>
    [Test]
    public async Task A_type_that_can_intersect_the_branched_scope_is_rejected()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int InterfaceAfterClass(object value)
                {
                    var result = 0;
                    switch (value)
                    {
                        case Loose { State: PaymentState.Pending }: result = 1; break;
                        case System.IDisposable: result = 2; break;
                    }

                    return result;
                }

                internal static int InterfaceAfterInterface(object value)
                {
                    var result = 0;
                    switch (value)
                    {
                        case IStamped { State: PaymentState.Pending }: result = 1; break;
                        case System.IDisposable: result = 2; break;
                    }

                    return result;
                }
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["System.IDisposable", "System.IDisposable"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>sealed な class と値型が実装しない interface の case は、分岐した位置の値と重ならないので許可すること</summary>
    [Test]
    public async Task An_interface_that_a_sealed_scope_does_not_implement_is_accepted()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int SealedClass(object value)
                {
                    var result = 0;
                    switch (value)
                    {
                        case Stamp { State: PaymentState.Pending }: result = 1; break;
                        case System.IDisposable: result = 2; break;
                        case IStamped: result = 3; break;
                    }

                    return result;
                }

                internal static int ValueType(object value)
                {
                    var result = 0;
                    switch (value)
                    {
                        case Tag { State: PaymentState.Pending }: result = 1; break;
                        case System.IDisposable: result = 2; break;
                    }

                    return result;
                }
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>配列型は決まった interface だけを実装し派生も作れないので、実装しない interface の位置で分岐した後の配列の case を許可すること</summary>
    [Test]
    public async Task An_array_type_is_accepted_after_an_interface_it_cannot_implement()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int Arrays(object value)
                {
                    var result = 0;
                    switch (value)
                    {
                        case IStamped { State: PaymentState.Pending }: result = 1; break;
                        case Loose[]: result = 2; break;
                        case int[,]: result = 3; break;
                    }

                    return result;
                }
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
