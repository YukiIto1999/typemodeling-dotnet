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

/// <summary>[ClosedUnion] enum switch 式の網羅性診断境界</summary>
public sealed class ClosedUnionEnumSwitchSuppressionTests
{
    /// <summary>全 named member を扱う switch 式の CS8524 抑止</summary>
    [Test]
    public async Task Cs8524_is_suppressed_when_the_switch_covers_every_named_member()
    {
        var diagnostics = await RunSuppressor("""
            using TypeModeling.Domain;

            namespace Probe;

            [ClosedUnion]
            internal enum Status
            {
                Open,
                Closed,
            }

            internal static class Handler
            {
                internal static int Describe(Status status) => status switch
                {
                    Status.Open => 1,
                    Status.Closed => 2,
                };
            }
            """);

        var cs8524 = diagnostics.Where(d => d.Id == "CS8524").ToImmutableArray();
        await Assert.That(cs8524.Length).IsEqualTo(1);
        await Assert.That(cs8524[0].IsSuppressed).IsTrue();
    }

    /// <summary>or pattern で全 named member を扱う switch 式の CS8524 抑止</summary>
    [Test]
    public async Task Cs8524_is_suppressed_when_or_patterns_cover_every_named_member()
    {
        var diagnostics = await RunSuppressor("""
            using TypeModeling.Domain;

            namespace Probe;

            [ClosedUnion]
            internal enum Status
            {
                Open,
                Closed,
                Pending,
            }

            internal static class Handler
            {
                internal static int Describe(Status status) => status switch
                {
                    Status.Open or Status.Pending => 1,
                    Status.Closed => 2,
                };
            }
            """);

        var cs8524 = diagnostics.Where(d => d.Id == "CS8524").ToImmutableArray();
        await Assert.That(cs8524.Length).IsEqualTo(1);
        await Assert.That(cs8524[0].IsSuppressed).IsTrue();
    }

    /// <summary>括弧付き or pattern で全 named member を扱う switch 式の CS8524 抑止</summary>
    [Test]
    public async Task Cs8524_is_suppressed_when_parenthesized_or_patterns_cover_every_named_member()
    {
        var diagnostics = await RunSuppressor("""
            using TypeModeling.Domain;

            namespace Probe;

            [ClosedUnion]
            internal enum Status
            {
                Open,
                Closed,
                Pending,
            }

            internal static class Handler
            {
                internal static int Describe(Status status) => status switch
                {
                    (Status.Open or Status.Pending) => 1,
                    Status.Closed => 2,
                };
            }
            """);

        var cs8524 = diagnostics.Where(d => d.Id == "CS8524").ToImmutableArray();
        await Assert.That(cs8524.Length).IsEqualTo(1);
        await Assert.That(cs8524[0].IsSuppressed).IsTrue();
    }

    /// <summary>named member を欠く switch 式の CS8509 維持</summary>
    [Test]
    public async Task Cs8509_is_not_suppressed_when_the_switch_omits_a_named_member()
    {
        var diagnostics = await RunSuppressor("""
            using TypeModeling.Domain;

            namespace Probe;

            [ClosedUnion]
            internal enum Status
            {
                Open,
                Closed,
                Pending,
            }

            internal static class Handler
            {
                internal static int Describe(Status status) => status switch
                {
                    Status.Open => 1,
                    Status.Closed => 2,
                };
            }
            """);

        var cs8509 = diagnostics.Where(d => d.Id == "CS8509").ToImmutableArray();
        await Assert.That(cs8509.Length).IsEqualTo(1);
        await Assert.That(cs8509[0].IsSuppressed).IsFalse();
    }

}
