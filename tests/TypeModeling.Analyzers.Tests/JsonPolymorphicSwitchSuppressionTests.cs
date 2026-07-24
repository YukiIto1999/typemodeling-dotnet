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

/// <summary>JSON polymorphic switch 式の網羅性診断境界</summary>
public sealed class JsonPolymorphicSwitchSuppressionTests
{
    /// <summary>全 derived type を扱う JSON polymorphic switch 式の CS8509 抑止</summary>
    [Test]
    public async Task Cs8509_is_suppressed_when_json_polymorphic_switch_covers_every_derived_type()
    {
        var diagnostics = await RunSuppressor("""
            using System.Text.Json.Serialization;

            namespace Probe;

            [JsonPolymorphic]
            [JsonDerivedType(typeof(A), "a")]
            [JsonDerivedType(typeof(B), "b")]
            internal abstract record Outcome;

            internal sealed record A : Outcome;

            internal sealed record B : Outcome;

            internal static class Handler
            {
                internal static void Describe(Outcome outcome)
                {
                    var values = new System.Collections.Generic.List<int>();
                    values.Add(outcome switch
                    {
                        A => 1,
                        B => 2,
                    });
                }
            }
            """);

        var cs8509 = diagnostics.Where(diagnostic => diagnostic.Id == "CS8509").ToImmutableArray();
        await Assert.That(cs8509.Length).IsEqualTo(1);
        await Assert.That(cs8509[0].IsSuppressed).IsTrue();
    }

    /// <summary>derived type を欠く JSON polymorphic switch 式の CS8509 維持</summary>
    [Test]
    public async Task Cs8509_is_not_suppressed_when_json_polymorphic_switch_omits_a_derived_type()
    {
        var diagnostics = await RunSuppressor("""
            using System.Text.Json.Serialization;

            namespace Probe;

            [JsonPolymorphic]
            [JsonDerivedType(typeof(A), "a")]
            [JsonDerivedType(typeof(B), "b")]
            internal abstract record Outcome;

            internal sealed record A : Outcome;

            internal sealed record B : Outcome;

            internal static class Handler
            {
                internal static int Describe(Outcome outcome) => outcome switch
                {
                    A => 1,
                };
            }
            """);

        var cs8509 = diagnostics.Where(diagnostic => diagnostic.Id == "CS8509").ToImmutableArray();
        await Assert.That(cs8509.Length).IsEqualTo(1);
        await Assert.That(cs8509[0].IsSuppressed).IsFalse();
    }

}
