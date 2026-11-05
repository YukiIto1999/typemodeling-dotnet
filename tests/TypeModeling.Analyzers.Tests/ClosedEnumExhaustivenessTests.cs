using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExhaustivenessAssertions;

namespace TypeModeling.Analyzers.Tests;

/// <summary>閉じた enum の網羅を、C# 16 の closed enum と同じく名前付きの member で判定すること</summary>
public sealed class ClosedEnumExhaustivenessTests
{
    /// <summary>三つの名前付き member を持つ閉じた enum の宣言</summary>
    private const string Status = """
        using TypeModeling.Domain;

        namespace Probe;

        [Closed]
        internal enum Status
        {
            Open,
            Closed,
            Pending,
        }
        """;

    /// <summary>全ての名前付き member を扱う switch 式の網羅</summary>
    [Test]
    public Task Every_named_member_makes_the_switch_exhaustive() => AssertExhaustive(Status + """
        internal static class Handler
        {
            internal static int Describe(Status status) => status switch
            {
                Status.Open => 1,
                Status.Closed => 2,
                Status.Pending => 3,
            };
        }
        """);

    /// <summary>or pattern と括弧で全ての名前付き member を扱う switch 式の網羅</summary>
    [Test]
    public Task Or_patterns_over_every_named_member_make_the_switch_exhaustive() => AssertExhaustive(Status + """
        internal static class Handler
        {
            internal static int Describe(Status status) => status switch
            {
                (Status.Open or Status.Pending) => 1,
                Status.Closed => 2,
            };
        }
        """);

    /// <summary>名前付き member を欠く switch 式の CS8509 の維持</summary>
    [Test]
    public Task A_missing_named_member_keeps_the_switch_not_exhaustive() => AssertNotExhaustive(Status + """
        internal static class Handler
        {
            internal static int Describe(Status status) => status switch
            {
                Status.Open => 1,
                Status.Closed => 2,
            };
        }
        """);

    /// <summary>閉じた enum と bool の組の全ての組合せを扱う switch 式の網羅</summary>
    [Test]
    public Task Closed_enums_inside_tuples_are_exhaustive() => AssertExhaustive(Status + """
        internal static class Handler
        {
            internal static int Describe(Status status, bool urgent) => (status, urgent) switch
            {
                (Status.Open, _) => 1,
                (Status.Closed or Status.Pending, true) => 2,
                (Status.Closed or Status.Pending, false) => 3,
            };
        }
        """);
}
