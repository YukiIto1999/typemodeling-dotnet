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

/// <summary>閉じた値の由来に対する制御フロー追跡</summary>
public sealed class ClosedOriginDataFlowTests
{
    /// <summary>loop carried な閉じた由来の固定点追跡</summary>
    [Test]
    public async Task Typmod001_tracks_loop_carried_closed_origins_to_a_fixed_point()
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

            internal static class Handler
            {
                internal static void Handle(Outcome outcome, bool repeat)
                {
                    object widened = "ordinary";
                    while (repeat)
                    {
                        switch (widened)
                        {
                            case Outcome.A:
                                break;
                            case Outcome.B:
                                break;
                        }

                        widened = outcome;
                    }
                }
            }
            """);

        var statements = diagnostics.Where(diagnostic => diagnostic.Id == "TYPMOD001").ToImmutableArray();
        await Assert.That(statements.Length).IsEqualTo(1);
    }

    /// <summary>上書きまたは変換された閉じた値の除外</summary>
    [Test]
    public async Task Typmod001_ignores_overwritten_and_transformed_closed_values()
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

            internal static class Handler
            {
                internal static void Handle(Outcome outcome)
                {
                    object overwritten = outcome;
                    overwritten = new object();
                    switch (overwritten)
                    {
                        case object:
                            break;
                    }

                    object transformed = Transform(outcome);
                    switch (transformed)
                    {
                        case object:
                            break;
                    }
                }

                private static object Transform(Outcome _) => new object();
            }
            """);

        var statements = diagnostics.Where(diagnostic => diagnostic.Id == "TYPMOD001").ToImmutableArray();
        await Assert.That(statements.Length).IsEqualTo(0);
    }

}
