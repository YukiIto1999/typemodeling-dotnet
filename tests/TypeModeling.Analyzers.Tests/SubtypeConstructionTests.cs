using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExhaustivenessAssertions;

namespace TypeModeling.Analyzers.Tests;

/// <summary>閉じた class の構築形で取りうる派生を、C# 15 と同じく基底の型引数から決めること</summary>
public sealed class SubtypeConstructionTests
{
    /// <summary>基底の型引数の並びで取りうる派生が変わる閉じた record の宣言</summary>
    private const string Generic = """
        using TypeModeling.Domain;

        namespace Probe;

        [Closed]
        public abstract record Pair<T1, T2>;
        public sealed record Same<U>(U Value) : Pair<U, U>;
        public sealed record Diff<A, B>(A Left, B Right) : Pair<A, B>;
        public sealed record IntLeft<B>(B Right) : Pair<int, B>;

        [Closed]
        public abstract record Twin<T1, T2>;
        public sealed record Mirror<U>(U Value) : Twin<U, U>;
        public sealed record Apart<A, B>(A Left, B Right) : Twin<A, B>;

        [Closed]
        public abstract record Box<V>;
        public sealed record Grid<E>(E[,] Cells) : Box<E[,]>;
        public sealed record Row<E>(E[] Items) : Box<E[]>;
        public sealed record Dyn : Box<dynamic>;

        [Closed]
        public abstract record PaymentState
        {
            private PaymentState() { }
            public sealed record Pending : PaymentState;
            public sealed record Settled : PaymentState;
        }

        [Closed]
        public abstract record Outcome<T>
        {
            private Outcome() { }
            public sealed record Done(T Value) : Outcome<T>;
            public sealed record Odd : Outcome<int>;
        }

        [Closed]
        public abstract record Shape;
        public sealed record Circle : Shape;

        public static class Holder
        {
            public sealed record Square : Shape;
        }
        """;

    /// <summary>基底の型引数が一致しない派生を除いた網羅</summary>
    /// <param name="input">switch 式の入力の型</param>
    /// <param name="arms">switch 式の arm</param>
    [Test]
    [Arguments("Pair<int, string>", "Diff<int, string> => 1, IntLeft<string> => 2")]
    [Arguments("Pair<string, int>", "Diff<string, int> => 1")]
    [Arguments("Pair<int, int>", "Diff<int, int> => 1, Same<int> => 2, IntLeft<int> => 3")]
    [Arguments("Twin<T, T>", "Apart<T, T> => 1, Mirror<T> => 2")]
    [Arguments("Box<int[]>", "Row<int> => 1")]
    [Arguments("Box<dynamic>", "Dyn => 1")]
    [Arguments("Shape", "Circle => 1, Holder.Square => 2")]
    public Task Subtypes_whose_base_cannot_match_are_left_out(string input, string arms) => AssertExhaustive(Generic + $$"""
        internal static class Handler
        {
            internal static int Describe<T>({{input}} value) => value switch { {{arms}} };
        }
        """);

    /// <summary>基底の型引数が一致しうる派生を欠いた switch 式の CS8509 の維持</summary>
    /// <remarks>C# 15 は、型引数次第で存在する派生を型引数で書いた arm で尽くせるとはみなさない</remarks>
    /// <param name="input">switch 式の入力の型</param>
    /// <param name="arms">switch 式の arm</param>
    [Test]
    [Arguments("Pair<int, int>", "Diff<int, int> => 1")]
    [Arguments("Pair<T, int>", "Diff<T, int> => 1")]
    [Arguments("Pair<T, T>", "Diff<T, T> => 1")]
    [Arguments("Pair<T, int>", "Diff<T, int> => 1, Same<T> => 2, IntLeft<int> => 3")]
    public Task Subtypes_whose_base_may_match_must_be_handled(string input, string arms) => AssertNotExhaustive(Generic + $$"""
        internal static class Handler
        {
            internal static int Describe<T>({{input}} value) => value switch { {{arms}} };
        }
        """);

    /// <summary>名前で挙げられない派生を持つ閉じた型を、他の位置の網羅の妨げにしないこと</summary>
    /// <param name="input">tuple の先頭の要素の型</param>
    [Test]
    [Arguments("Outcome<string>")]
    [Arguments("Pair<T, int>")]
    public Task Unspeakable_subtypes_do_not_block_other_positions(string input) => AssertExhaustive(Generic + $$"""
        internal static class Handler
        {
            internal static int Describe<T>({{input}} value, PaymentState state) => (value, state) switch
            {
                (_, PaymentState.Pending) => 1,
                (_, PaymentState.Settled) => 2,
            };
        }
        """);

    /// <summary>基底で参照しない型引数を持つ派生を、例外を出さずに網羅の外に置くこと</summary>
    [Test]
    public Task Subtypes_with_unbound_type_parameters_keep_the_switch_not_exhaustive() => AssertNotExhaustive("""
        using TypeModeling.Domain;

        [Closed]
        public abstract record Pair<T1, T2>;
        public sealed record Phantom<A, B>(A Value) : Pair<A, A>;

        internal static class Handler
        {
            internal static int Describe(Pair<int, int> value) => value switch { Phantom<int, string> => 1 };
        }
        """);
}
