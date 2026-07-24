using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ClosedTypeDiagnosticFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>governing expression に依存しない closed variant の検出</summary>
public sealed class ClosedVariantCasePatternOriginTests
{
    private const string ProbeSource = """
        using TypeModeling.Domain;

        namespace Probe;

        [ClosedUnion]
        internal abstract record Outcome
        {
            internal sealed record A : Outcome;
            internal sealed record B : Outcome;
        }

        [ClosedUnion]
        internal enum Status
        {
            Open,
            Closed,
        }

        internal static class Handler
        {
            private static object BoxedField = new Outcome.A();

            private static object BoxedProperty => new Outcome.B();

            internal static void FromHelper(Outcome outcome)
            {
                switch (Box(outcome))
                {
                    case Outcome.A:
                        break;
                    case Outcome.B:
                        break;
                }
            }

            internal static void FromField()
            {
                switch (BoxedField)
                {
                    case Outcome.A:
                        break;
                    case Outcome.B:
                        break;
                }
            }

            internal static void FromProperty()
            {
                switch (BoxedProperty)
                {
                    case Outcome.A:
                        break;
                    case Outcome.B:
                        break;
                }
            }

            internal static void FromParameter(object boxed)
            {
                switch (boxed)
                {
                    case Outcome.A:
                        break;
                    case Outcome.B:
                        break;
                }
            }

            internal static void FromCoalesceAssignment(Outcome outcome)
            {
                object? boxed = null;
                boxed ??= outcome;
                switch (boxed)
                {
                    case Outcome.A:
                        break;
                    case Outcome.B:
                        break;
                }
            }

            internal static void FromNullableEnum(Status? status)
            {
                switch (status)
                {
                    case Status.Open:
                        break;
                    case Status.Closed:
                        break;
                }
            }

            internal static int Expression(Outcome outcome) => outcome switch
            {
                Outcome.A => 1,
                Outcome.B => 2,
            };

            private static object Box(Outcome outcome) => outcome;
        }
        """;

    /// <summary>governing expression の由来に依存しない closed variant の検出</summary>
    [Test]
    public async Task Typmod001_anchors_closed_variants_in_case_patterns_regardless_of_governing_origin()
    {
        var diagnostics = await RunSuppressor(ProbeSource);

        var statements = diagnostics.Where(diagnostic => diagnostic.Id == "TYPMOD001").ToImmutableArray();
        await Assert.That(statements.Length).IsEqualTo(6);
    }

}
