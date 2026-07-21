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

/// <summary>包含 switch case 内の閉じた値の由来追跡</summary>
public sealed class ContainingSwitchCaseOriginTrackingTests
{
    /// <summary>外側の switch case で代入された閉じた由来の追跡</summary>
    [Test]
    public async Task Typmod001_tracks_closed_origins_assigned_earlier_in_a_containing_switch_case()
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
                internal static void Handle(Outcome outcome, int selector)
                {
                    switch (selector)
                    {
                        case 0:
                            object widened = outcome;
                            switch (widened)
                            {
                                case Outcome.A:
                                    break;
                                case Outcome.B:
                                    break;
                            }
                            break;
                    }
                }
            }
            """);

        var statements = diagnostics.Where(diagnostic => diagnostic.Id == "TYPMOD001").ToImmutableArray();
        await Assert.That(statements.Length).IsEqualTo(1);
    }

    /// <summary>外側の switch case で上書きされた閉じた由来の除外</summary>
    [Test]
    public async Task Typmod001_ignores_closed_origins_overwritten_in_a_containing_switch_case()
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
                internal static void Handle(Outcome outcome, int selector)
                {
                    switch (selector)
                    {
                        case 0:
                            object widened = outcome;
                            widened = new object();
                            switch (widened)
                            {
                                case object:
                                    break;
                            }
                            break;
                    }
                }
            }
            """);

        var statements = diagnostics.Where(diagnostic => diagnostic.Id == "TYPMOD001").ToImmutableArray();
        await Assert.That(statements.Length).IsEqualTo(0);
    }

}
