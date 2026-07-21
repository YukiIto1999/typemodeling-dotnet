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

/// <summary>when guard 内の closed variant 識別判定</summary>
public sealed class ClosedVariantWhenGuardTests
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

        internal sealed record Value(int Foo);

        internal readonly struct Matcher
        {
            public static bool operator ==(Matcher left, Status right) => false;

            public static bool operator !=(Matcher left, Status right) => true;

            public override bool Equals(object? value) => value is Matcher;

            public override int GetHashCode() => 0;
        }

        internal static class Handler
        {
            internal static void TypePattern(object value)
            {
                switch (value)
                {
                    case var candidate when candidate is Outcome.A:
                        break;
                }
            }

            internal static void Equality(object value)
            {
                switch (value)
                {
                    case var candidate when candidate == (object)Status.Open:
                        break;
                }
            }

            internal static void ConstantPattern(object value)
            {
                switch (value)
                {
                    case var candidate when candidate is Status.Open:
                        break;
                }
            }

            internal static void Inequality(object value)
            {
                switch (value)
                {
                    case var candidate when candidate != (object)Status.Closed:
                        break;
                }
            }

            internal static void PropertyComparison(object value)
            {
                switch (value)
                {
                    case Value candidate when candidate.Foo > 0:
                        break;
                }
            }

            internal static void ArithmeticEquality(object value)
            {
                switch (value)
                {
                    case var candidate when candidate == (object)((int)Status.Open + 100):
                        break;
                }
            }


            internal static void OverloadedInequality(object value)
            {
                switch (value)
                {
                    case Matcher candidate when candidate != Status.Open:
                        break;
                }
            }
        }
        """;

    /// <summary>when guard 内だけで判別される closed variant の検出</summary>
    [Test]
    public async Task Typmod001_anchors_closed_variant_discrimination_in_when_guards_only()
    {
        var diagnostics = await RunSuppressor(ProbeSource);

        var statements = diagnostics.Where(diagnostic => diagnostic.Id == "TYPMOD001").ToImmutableArray();
        var methods = statements.Select(diagnostic => diagnostic.Location.SourceTree!
            .GetRoot()
            .FindToken(diagnostic.Location.SourceSpan.Start)
            .Parent!
            .AncestorsAndSelf()
            .OfType<MethodDeclarationSyntax>()
            .Single()
            .Identifier.ValueText);
        await Assert.That(methods).IsEquivalentTo([
            "TypePattern",
            "Equality",
            "ConstantPattern",
            "Inequality",
        ]);
    }

}
