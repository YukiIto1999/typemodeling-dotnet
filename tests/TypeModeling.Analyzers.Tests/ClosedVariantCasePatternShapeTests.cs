using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ClosedTypeDiagnosticFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>case pattern の closed variant 検出</summary>
public sealed class ClosedVariantCasePatternShapeTests
{
    private const string NestedPatternProbeSource = """
        using System.Text.Json.Serialization;
        using TypeModeling.Domain;

        namespace Probe;

        [ClosedUnion]
        internal abstract record Outcome
        {
            internal sealed record A : Outcome;
            internal sealed record B : Outcome;
        }

        internal sealed record Envelope(object Value);

        [JsonPolymorphic]
        [JsonDerivedType(typeof(JsonA), "a")]
        [JsonDerivedType(typeof(JsonB), "b")]
        internal interface IJsonOutcome;

        internal sealed record JsonA : IJsonOutcome;
        internal sealed record JsonB : IJsonOutcome;

        [ClosedUnion]
        internal enum Status
        {
            Open,
            Closed,
        }

        internal static class Handler
        {
            internal static void NestedProperty(Envelope envelope)
            {
                switch (envelope)
                {
                    case Envelope { Value: Outcome.A }:
                        break;
                }
            }

            internal static void JsonInterface(object value)
            {
                switch (value)
                {
                    case JsonA:
                        break;
                }
            }

            internal static void ConvertedEnum(Status status)
            {
                switch ((int)status)
                {
                    case (int)Status.Open:
                        break;
                }
            }
        }
        """;

    /// <summary>or pattern 内の closed variant の検出</summary>
    [Test]
    public async Task Typmod001_anchors_closed_variants_nested_in_or_patterns()
    {
        var diagnostics = await RunSuppressor("""
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
                internal static void Union(object value)
                {
                    switch (value)
                    {
                        case Outcome.A or Outcome.B:
                            break;
                    }
                }

                internal static void Enum(Status? status)
                {
                    switch (status)
                    {
                        case Status.Open or Status.Closed:
                            break;
                    }
                }
            }
            """);

        var statements = diagnostics.Where(diagnostic => diagnostic.Id == "TYPMOD001").ToImmutableArray();
        await Assert.That(statements.Length).IsEqualTo(2);
    }

    /// <summary>JSON interface と変換済み enum の closed variant 検出</summary>
    [Test]
    public async Task Typmod001_anchors_nested_json_interface_and_converted_enum_variants()
    {
        var diagnostics = await RunSuppressor(NestedPatternProbeSource);

        var statements = diagnostics.Where(diagnostic => diagnostic.Id == "TYPMOD001").ToImmutableArray();
        await Assert.That(statements.Length).IsEqualTo(3);
    }

}
