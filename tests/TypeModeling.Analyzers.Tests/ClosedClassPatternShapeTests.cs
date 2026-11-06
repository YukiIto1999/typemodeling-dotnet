using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExhaustivenessAssertions;

namespace TypeModeling.Analyzers.Tests;

/// <summary>閉じた class の網羅を、C# 15 と同じく pattern の形を問わず判定すること</summary>
public sealed class ClosedClassPatternShapeTests
{
    /// <summary>入れ子の派生を持つ閉じた record の宣言</summary>
    private const string PaymentState = """
        using TypeModeling.Domain;

        namespace Probe;

        [Closed]
        public abstract record PaymentState
        {
            private PaymentState() { }
            public sealed record Pending : PaymentState;
            public sealed record Settled(System.DateTimeOffset At, bool Refunded) : PaymentState;
        }
        """;

    /// <summary>型 pattern で全ての派生を扱う switch 式の網羅</summary>
    [Test]
    public Task Type_patterns_over_every_subtype_are_exhaustive() => AssertExhaustive(PaymentState + """
        internal static class Handler
        {
            internal static int Describe(PaymentState state) => state switch
            {
                PaymentState.Pending => 1,
                PaymentState.Settled settled => 2,
            };
        }
        """);

    /// <summary>位置 pattern で全ての派生を扱う switch 式の網羅</summary>
    [Test]
    public Task Positional_patterns_over_every_subtype_are_exhaustive() => AssertExhaustive(PaymentState + """
        internal static class Handler
        {
            internal static int Describe(PaymentState state) => state switch
            {
                PaymentState.Pending => 1,
                PaymentState.Settled(var at, var refunded) => 2,
            };
        }
        """);

    /// <summary>property pattern の値の場合分けで全てを扱う switch 式の網羅</summary>
    [Test]
    public Task Property_patterns_that_split_a_subtype_completely_are_exhaustive() => AssertExhaustive(PaymentState + """
        internal static class Handler
        {
            internal static int Describe(PaymentState state) => state switch
            {
                PaymentState.Pending => 1,
                PaymentState.Settled { Refunded: true } => 2,
                PaymentState.Settled { Refunded: false } => 3,
            };
        }
        """);

    /// <summary>派生を欠く switch 式の CS8509 の維持</summary>
    [Test]
    public Task A_missing_subtype_keeps_the_switch_not_exhaustive() => AssertNotExhaustive(PaymentState + """
        internal static class Handler
        {
            internal static int Describe(PaymentState state) => state switch
            {
                PaymentState.Settled => 2,
            };
        }
        """);

    /// <summary>派生の値の一部を欠く switch 式の CS8509 の維持</summary>
    [Test]
    public Task A_missing_property_case_keeps_the_switch_not_exhaustive() => AssertNotExhaustive(PaymentState + """
        internal static class Handler
        {
            internal static int Describe(PaymentState state) => state switch
            {
                PaymentState.Pending => 1,
                PaymentState.Settled { Refunded: true } => 2,
            };
        }
        """);

    /// <summary>when 付きの arm だけが扱う派生の CS8846 の維持と、when のない arm を足した網羅</summary>
    [Test]
    public async Task When_clauses_count_only_when_an_unguarded_arm_covers_the_rest()
    {
        await AssertNotExhaustive(PaymentState + """
            internal static class Handler
            {
                internal static int Describe(PaymentState state, bool flag) => state switch
                {
                    PaymentState.Pending when flag => 1,
                    PaymentState.Settled => 2,
                };
            }
            """);
        await AssertExhaustive(PaymentState + """
            internal static class Handler
            {
                internal static int Describe(PaymentState state, bool flag) => state switch
                {
                    PaymentState.Pending when flag => 1,
                    PaymentState.Pending => 3,
                    PaymentState.Settled => 2,
                };
            }
            """);
    }

    /// <summary>二つの閉じた値の組の全ての組合せを扱う switch 式の網羅</summary>
    [Test]
    public Task Tuples_of_closed_values_are_exhaustive_when_every_combination_is_covered() => AssertExhaustive(PaymentState + """
        internal static class Handler
        {
            internal static int Describe(PaymentState left, PaymentState right) => (left, right) switch
            {
                (PaymentState.Pending, _) => 1,
                (PaymentState.Settled, PaymentState.Pending) => 2,
                (PaymentState.Settled, PaymentState.Settled) => 3,
            };
        }
        """);

    /// <summary>property に入れ子の閉じた値の全てを扱う switch 式の網羅</summary>
    [Test]
    public Task Closed_values_nested_in_properties_are_exhaustive() => AssertExhaustive(PaymentState + """
        internal sealed record Order(PaymentState State);

        internal static class Handler
        {
            internal static int Describe(Order order) => order switch
            {
                { State: PaymentState.Pending } => 1,
                { State: PaymentState.Settled } => 2,
            };
        }
        """);
}
