using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>Activation boundaries for nested case families</summary>
public sealed class ExplicitCaseHierarchyActivationTests
{
    /// <summary>Protection of a closed hierarchy used as a union case</summary>
    /// <returns>Completion of the nested union family assertions</returns>
    [Test]
    public async Task Naming_a_subtype_of_a_closed_union_case_protects_its_family()
    {
        var diagnostics = await RunClosed("""
            namespace System.Runtime.CompilerServices
            {
                [System.AttributeUsage(System.AttributeTargets.Struct)]
                public sealed class UnionAttribute : System.Attribute;
            }

            namespace Probe
            {
                [TypeModeling.Domain.Closed]
                public abstract record PaymentState
                {
                    private PaymentState() { }
                    public sealed record Pending : PaymentState;
                    public sealed record Settled : PaymentState;
                }

                [System.Runtime.CompilerServices.Union]
                public readonly struct Outcome
                {
                    public Outcome(PaymentState value) => Value = value;
                    public Outcome(string value) => Value = value;
                    public object Value { get; }
                }

                internal static class Handler
                {
                    internal static int Describe(Outcome outcome) => outcome.Value switch
                    {
                        PaymentState.Pending => 1,
                        PaymentState => 2,
                        string => 3,
                    };
                }
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["PaymentState"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>Open descendants remaining whole cases of their closed ancestor</summary>
    /// <returns>Completion of the open branch activation assertions</returns>
    [Test]
    public async Task Naming_an_open_branch_subtype_does_not_close_that_branch()
    {
        var diagnostics = await RunClosed("""
            using TypeModeling.Domain;

            namespace Probe;

            [Closed]
            public abstract record Shape
            {
                private Shape() { }
                public sealed record Circle : Shape;
                public abstract record Rect : Shape;
            }

            public sealed record Square : Shape.Rect;
            public sealed record Oblong : Shape.Rect;

            internal static class Handler
            {
                internal static int Describe(Shape shape) => shape switch
                {
                    Square => 1,
                    Shape.Rect => 2,
                    Shape.Circle => 3,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
