using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>型の一致だけを調べる case による保護対象の payload の隠蔽の診断</summary>
public sealed class ExplicitCaseTypeReachTests
{
    /// <summary>class と interface の実装関係を区別する宣言の組</summary>
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

    /// <summary>派生型で interface を実装できる class の case による payload の隠蔽の拒否</summary>
    /// <returns>派生型を介した重なりの検証の完了</returns>
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

    /// <summary>同じ値を持ちうる異なる型の case による payload の隠蔽の拒否</summary>
    /// <returns>実装を追加できる型の重なりの検証の完了</returns>
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

    /// <summary>sealed な型と値型が実装しない interface の case の許可</summary>
    /// <returns>実装関係を持たない型の分離の検証の完了</returns>
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

    /// <summary>実装しない interface と配列の型の分離</summary>
    /// <returns>配列が持ちえない interface との非交差の検証の完了</returns>
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
