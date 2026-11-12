using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>Protection activated by case branching inside payloads</summary>
public sealed class ExplicitCaseActivationTests
{
    /// <summary>Closed outcome carrying an otherwise untouched payment state</summary>
    private const string OutcomeDeclaration = """

        [Closed]
        public abstract record Outcome
        {
            private Outcome() { }
            public sealed record Done(PaymentState State) : Outcome;
            public sealed record Skipped : Outcome;
        }
        """;

    /// <summary>Payload protection activated by each supported case naming pattern</summary>
    /// <param name="pattern">Pattern naming a payment case inside the payload</param>
    /// <param name="acceptsFuture">Whether the naming pattern also accepts future cases</param>
    /// <returns>Completion of the payload protection assertions</returns>
    [Test]
    [Arguments("PaymentState.Pending or null", false)]
    [Arguments("null or PaymentState.Pending", false)]
    [Arguments("not PaymentState.Pending", true)]
    [Arguments("PaymentState.Pending pending", false)]
    [Arguments("PaymentState.Settled { Refunded: true }", false)]
    public async Task Naming_a_payload_case_protects_that_position(string pattern, bool acceptsFuture)
    {
        var diagnostics = await RunClosed(Payment() + OutcomeDeclaration + $$"""

            internal static class Handler
            {
                internal static int Describe(Outcome outcome) => outcome switch
                {
                    Outcome.Done({{pattern}}) => 1,
                    Outcome.Done(var other) => other.Label.Length,
                    Outcome.Skipped => 0,
                };
            }
            """);
        string[] flagged = acceptsFuture ? [pattern, "var other"] : ["var other"];

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(flagged);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>Protection of an enum payload after a named member branch</summary>
    /// <returns>Completion of the enum payload protection assertions</returns>
    [Test]
    public async Task Naming_an_enum_payload_member_protects_that_position()
    {
        var diagnostics = await RunClosed("""
            using TypeModeling.Domain;

            namespace Probe;

            [Closed]
            public enum Color { Red, Green, Blue }

            [Closed]
            public abstract record Outcome
            {
                private Outcome() { }
                public sealed record Painted(Color Color) : Outcome;
                public sealed record Skipped : Outcome;
            }

            internal static class Handler
            {
                internal static int Describe(Outcome outcome) => outcome switch
                {
                    Outcome.Painted(Color.Red) => 1,
                    Outcome.Painted(var other) => (int)other,
                    Outcome.Skipped => 0,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["var other"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>Broad payload binders without case branching</summary>
    /// <returns>Completion of the untouched payload binder assertions</returns>
    [Test]
    public async Task An_object_payload_binder_does_not_activate_protection()
    {
        var diagnostics = await RunClosed(Payment() + OutcomeDeclaration + """

            internal static class Handler
            {
                internal static int Describe(Outcome outcome) => outcome switch
                {
                    Outcome.Done(object state) => state.GetHashCode(),
                    Outcome.Skipped => 0,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
