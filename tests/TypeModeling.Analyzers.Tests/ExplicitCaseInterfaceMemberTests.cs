using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>interface の property と、それを実装する型の property を、同じ値を返すと確かめられるときだけ一つの位置とすること</summary>
public sealed class ExplicitCaseInterfaceMemberTests
{
    /// <summary>interface と、その property を暗黙に実装する sealed record、別の値を返す public property と明示実装を持つ sealed record、派生が再実装できる record の宣言</summary>
    private const string Declarations = """

        internal interface IStamped
        {
            PaymentState State { get; }
        }

        internal sealed record Stamp : IStamped
        {
            public PaymentState State { get; init; } = new PaymentState.Pending();
        }

        internal sealed record Disguised : IStamped
        {
            public PaymentState State { get; init; } = new PaymentState.Pending();

            PaymentState IStamped.State => new PaymentState.Failed("disguised");
        }

        internal record Loose : IStamped
        {
            public PaymentState State { get; init; } = new PaymentState.Pending();
        }

        internal sealed record Rebound : Loose, IStamped
        {
            PaymentState IStamped.State => new PaymentState.Failed("rebound");
        }
        """;

    /// <summary>interface の property と、それを暗黙に実装する sealed な型の property を、書く順によらず一つの位置として許可すること</summary>
    [Test]
    public async Task An_interface_property_and_its_implementation_in_a_sealed_type_are_one_position()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int ImplementationFirst(IStamped stamped) => stamped switch
                {
                    Stamp { State: PaymentState.Pending } => 1,
                    { State: PaymentState.Settled or PaymentState.Failed } => 2,
                    { State: PaymentState.Pending } => 3,
                };

                internal static int InterfaceFirst(IStamped stamped) => stamped switch
                {
                    { State: PaymentState.Settled or PaymentState.Failed } => 2,
                    Stamp { State: PaymentState.Pending } => 1,
                    { State: PaymentState.Pending } => 3,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>public property が別の値を返す型の interface 明示実装は、public property と別の位置として、interface で書く arm の拒否を残すこと</summary>
    [Test]
    public async Task An_explicit_implementation_is_not_merged_with_a_different_public_property()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int Describe(IStamped stamped) => stamped switch
                {
                    Disguised { State: PaymentState.Pending } => 1,
                    { State: var any } => any.Label.Length,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["{ State: var any }"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>派生が interface を再実装して別の値を返せる型の property は、interface の property と別の位置として、両方で書く arm の拒否を残すこと</summary>
    [Test]
    public async Task An_implementation_that_a_derived_type_can_re_implement_is_not_merged()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int Describe(IStamped stamped) => stamped switch
                {
                    Loose { State: PaymentState.Pending } => 1,
                    { State: PaymentState.Settled or PaymentState.Failed } => 2,
                    { State: PaymentState.Pending } => 3,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(
            ["Loose { State: PaymentState.Pending }", "{ State: PaymentState.Settled or PaymentState.Failed }", "{ State: PaymentState.Pending }"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
