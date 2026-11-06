using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ClosedTypeDiagnosticFixture;
using static TypeModeling.Analyzers.Tests.ExhaustivenessAssertions;

namespace TypeModeling.Analyzers.Tests;

/// <summary>入力の部分値にある閉じた型の網羅を、C# 15 と同じく部分値の取り出し方を問わず判定すること</summary>
public sealed class PatternPositionTests
{
    /// <summary>閉じた型を部分値に持つ型の宣言</summary>
    private const string Declarations = """
        using TypeModeling.Domain;

        namespace Probe;

        [Closed]
        public abstract record PaymentState
        {
            private PaymentState() { }
            public sealed record Pending : PaymentState;
            public sealed record Settled : PaymentState;
        }

        public sealed record Order(PaymentState State, bool Flag)
        {
            public (PaymentState First, bool Second) Both => (State, Flag);
            public Order? Next { get; init; }
        }

        public sealed record Pair(PaymentState First, PaymentState Second);

        [Closed]
        public abstract record Envelope
        {
            private Envelope() { }
            public sealed record Paid(PaymentState State) : Envelope;
            public sealed record Unpaid : Envelope;
        }

        [Closed]
        public abstract record Token
        {
            private Token() { }
            public sealed record Plain : Token;
            private sealed record Secret : Token;
        }

        public enum Light
        {
            Red,
            Green,
        }
        """;

    /// <summary>部分値の取り出し方ごとの、全ての派生を扱う switch 式の網羅</summary>
    /// <param name="input">switch 式の入力の型と名前</param>
    /// <param name="arms">switch 式の arm</param>
    [Test]
    [Arguments("Pair pair", "(PaymentState.Pending, _) => 1, (PaymentState.Settled, _) => 2")]
    [Arguments("Order order", "{ Both: (PaymentState.Pending, _) } => 1, { Both: (PaymentState.Settled, _) } => 2")]
    [Arguments("Order order", "{ State: PaymentState.Pending or PaymentState.Settled } or { Flag: true } => 1")]
    [Arguments("Order order", "{ Flag: true } or { State: PaymentState.Pending or PaymentState.Settled } => 1")]
    [Arguments("Order order", "{ Next.State: PaymentState.Pending } => 1, { Next.State: PaymentState.Settled } => 2, { Next: null } => 3")]
    [Arguments("Envelope envelope", "Envelope.Paid { State: PaymentState.Pending } => 1, Envelope.Paid { State: PaymentState.Settled } => 2, Envelope.Unpaid => 3")]
    public Task Closed_values_reached_through_any_pattern_shape_are_exhaustive(string input, string arms) =>
        AssertExhaustive(Declarations + $$"""
            internal static class Handler
            {
                internal static int Describe({{input}}) => {{input.Split(' ')[1]}} switch { {{arms}} };
            }
            """);

    /// <summary>名前で書けない匿名型の部分値の網羅</summary>
    [Test]
    public Task Closed_values_inside_anonymous_types_are_exhaustive() => AssertExhaustive(Declarations + """
        internal static class Handler
        {
            internal static int Describe(PaymentState state) => new { State = state } switch
            {
                { State: PaymentState.Pending } => 1,
                { State: PaymentState.Settled } => 2,
            };
        }
        """);

    /// <summary>arm の値の中の網羅していない switch 式と、外側の switch 式の網羅の区別</summary>
    [Test]
    public async Task An_inner_switch_that_is_not_exhaustive_leaves_the_outer_switch_exhaustive()
    {
        var diagnostics = await Run(Declarations + """
            internal static class Handler
            {
                internal static int Describe(PaymentState state, int code) => state switch
                {
                    PaymentState.Pending => code switch { 1 => 1 },
                    PaymentState.Settled => 2,
                };
            }
            """);

        await Assert.That(Suppressed(diagnostics, "CS8509").Length).IsEqualTo(1);
        await Assert.That(Active(diagnostics, "CS8509").Length).IsEqualTo(1);
    }

    /// <summary>存在しない member の property pattern を持つ switch 式の、例外を出さない解析</summary>
    [Test]
    public async Task Patterns_on_missing_members_are_analyzed_without_exceptions()
    {
        var diagnostics = await Run(Declarations + """
            internal static class Handler
            {
                internal static int Describe(Order order) => order switch
                {
                    { Missing: PaymentState.Pending } => 1,
                    { State: PaymentState.Settled } => 2,
                };
            }
            """);

        await Assert.That(Active(diagnostics, "CS0117")).IsNotEmpty();
        await Assert.That(Active(diagnostics, "AD0001")).IsEmpty();
    }

    /// <summary>閉じた型を考えなくても到達できない arm への、compiler の診断だけの報告</summary>
    [Test]
    public async Task Arms_the_compiler_already_rejects_get_no_closed_type_diagnostic()
    {
        var diagnostics = await Run(Declarations + """
            internal static class Handler
            {
                internal static int Describe(PaymentState state) => state switch
                {
                    PaymentState.Pending => 1,
                    PaymentState.Pending => 2,
                    PaymentState.Settled => 3,
                };
            }
            """);

        await Assert.That(Active(diagnostics, "CS8510")).IsNotEmpty();
        await Assert.That(Active(diagnostics, "TYPMOD006")).IsEmpty();
    }

    /// <summary>他の位置の閉じた型を尽くしても、閉じていない enum の名前のない値で網羅にならないこと</summary>
    [Test]
    public Task Open_enums_beside_closed_values_keep_the_switch_not_exhaustive() => AssertNotExhaustive(Declarations + """
        internal static class Handler
        {
            internal static int Describe(PaymentState state, Light light) => (state, light) switch
            {
                (PaymentState.Pending, Light.Red or Light.Green) => 1,
                (PaymentState.Settled, Light.Red or Light.Green) => 2,
            };
        }
        """);

    /// <summary>名前で参照できない派生を持つ閉じた型を、他の位置の網羅の妨げにしないこと</summary>
    [Test]
    public Task Inaccessible_subtypes_do_not_block_other_positions() => AssertExhaustive(Declarations + """
        internal static class Handler
        {
            internal static int Describe(Token token, PaymentState state) => (token, state) switch
            {
                (_, PaymentState.Pending) => 1,
                (_, PaymentState.Settled) => 2,
            };
        }
        """);
}
