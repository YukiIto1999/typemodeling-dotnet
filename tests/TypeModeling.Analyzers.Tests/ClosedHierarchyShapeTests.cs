using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ClosedTypeDiagnosticFixture;
using static TypeModeling.Analyzers.Tests.ExhaustivenessAssertions;

namespace TypeModeling.Analyzers.Tests;

/// <summary>閉じた class の派生の集合を、C# 15 の closed と同じ規則で数えること</summary>
public sealed class ClosedHierarchyShapeTests
{
    /// <summary>同じ assembly の top-level で sealed でない派生を持つ階層の網羅</summary>
    [Test]
    public Task Top_level_and_unsealed_subtypes_in_the_same_assembly_count() => AssertExhaustive("""
        using TypeModeling.Domain;

        [Closed]
        public abstract record GateState;
        public record Shut : GateState;
        public sealed record Locked : Shut;
        public sealed record Open(float Percent) : GateState;

        internal static class Handler
        {
            internal static int Describe(GateState state) => state switch
            {
                Shut => 1,
                Open(var percent) => 2,
            };
        }
        """);

    /// <summary>閉じた派生をさらに場合分けした階層の網羅と、その欠落の維持</summary>
    [Test]
    public async Task Closed_subtypes_are_exhausted_by_their_own_subtypes()
    {
        const string hierarchy = """
            using TypeModeling.Domain;

            [Closed]
            public abstract record Shape;
            [Closed]
            public abstract record Polygon : Shape;
            public sealed record Triangle : Polygon;
            public sealed record Square : Polygon;
            public sealed record Circle : Shape;
            """;
        await AssertExhaustive(hierarchy + """
            internal static class Handler
            {
                internal static int Corners(Shape shape) => shape switch
                {
                    Triangle => 3,
                    Square => 4,
                    Circle => 0,
                };
            }
            """);
        await AssertNotExhaustive(hierarchy + """
            internal static class Handler
            {
                internal static int Corners(Shape shape) => shape switch
                {
                    Triangle => 3,
                    Circle => 0,
                };
            }
            """);
    }

    /// <summary>generic な閉じた record を generic method の中で網羅する switch 式</summary>
    [Test]
    public Task Generic_closed_types_are_exhaustive_inside_generic_methods() => AssertExhaustive("""
        using TypeModeling.Domain;

        [Closed]
        public abstract record Outcome<TValue, TFailure>
        {
            private Outcome() { }
            public sealed record Succeeded(TValue Value) : Outcome<TValue, TFailure>;
            public sealed record Failed(TFailure Failure) : Outcome<TValue, TFailure>;
        }

        internal static class Handler
        {
            internal static TResult Fold<TValue, TFailure, TResult>(
                Outcome<TValue, TFailure> outcome, System.Func<TValue, TResult> onValue, System.Func<TFailure, TResult> onFailure) =>
                outcome switch
                {
                    Outcome<TValue, TFailure>.Succeeded(var value) => onValue(value),
                    Outcome<TValue, TFailure>.Failed(var failure) => onFailure(failure),
                };
        }
        """);

    /// <summary>閉じた class に制約した型引数の網羅</summary>
    [Test]
    public Task Type_parameters_constrained_to_a_closed_class_are_exhaustive() => AssertExhaustive("""
        using TypeModeling.Domain;

        [Closed]
        public abstract class Node;
        public sealed class Leaf : Node;
        public sealed class Branch : Node;

        internal static class Handler
        {
            internal static int Describe<TNode>(TNode node) where TNode : Node => node switch
            {
                Leaf => 1,
                Branch => 2,
            };
        }
        """);

    /// <summary>構築形に対応しない派生を数えず、型引数で定まらない派生は名前で網羅できないこと</summary>
    [Test]
    public async Task Subtypes_are_selected_by_the_generic_instantiation()
    {
        const string hierarchy = """
            using TypeModeling.Domain;

            [Closed]
            public abstract class Box<T>;
            public sealed class Single<U> : Box<U>;
            public sealed class Many<V> : Box<V[]>;
            """;
        await AssertExhaustive(hierarchy + """
            internal static class Handler
            {
                internal static int Describe(Box<string> box) => box switch { Single<string> => 1 };
            }
            """);
        await AssertNotExhaustive(hierarchy + """
            internal static class Handler
            {
                internal static int Describe<X>(Box<X> box) => box switch { Single<X> => 1 };
            }
            """);
    }

    /// <summary>派生のない閉じた class と、参照できない派生を持つ閉じた class の網羅の不成立</summary>
    [Test]
    public async Task Empty_hierarchies_and_inaccessible_subtypes_are_not_exhaustive()
    {
        await AssertNotExhaustive("""
            using TypeModeling.Domain;

            [Closed]
            public abstract class Empty;

            internal static class Handler
            {
                internal static int Describe(Empty value) => value switch { };
            }
            """);
        await AssertNotExhaustive("""
            using TypeModeling.Domain;

            [Closed]
            public abstract class Node;
            public sealed class Leaf : Node;
            public static class Container
            {
                private sealed class Hidden : Node;
            }

            internal static class Handler
            {
                internal static int Describe(Node node) => node switch { Leaf => 1 };
            }
            """);
    }
}
