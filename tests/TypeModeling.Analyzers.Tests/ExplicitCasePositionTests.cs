using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>switch の入力の一部と case の中身の位置を、case を挙げて分岐するときだけ守ること</summary>
public sealed class ExplicitCasePositionTests
{
    /// <summary>決済の状態を中身に持つ、閉じた record の宣言</summary>
    private const string OutcomeDeclaration = """

        [Closed]
        public abstract record Outcome
        {
            private Outcome() { }
            public sealed record Done(PaymentState State) : Outcome;
            public sealed record Skipped : Outcome;
        }
        """;

    /// <summary>閉じた class を入れ子に持つ、閉じた record の宣言</summary>
    private const string ShapeDeclaration = """
        using TypeModeling.Domain;

        namespace Probe;

        [Closed]
        public abstract record Shape
        {
            private Shape() { }
            public sealed record Circle : Shape;

            [Closed]
            public abstract record Rect : Shape
            {
                private Rect() { }
                public sealed record Square : Rect;
                public sealed record Oblong : Rect;
            }
        }
        """;

    /// <summary>case の中身の閉じた型を、どの arm も挙げて分岐しなければ束縛と discard を許し、挙げて分岐すれば拒否すること</summary>
    [Test]
    public async Task A_payload_position_is_protected_only_where_a_case_is_named()
    {
        var diagnostics = await RunClosed(Payment() + OutcomeDeclaration + """


            internal static class Handler
            {
                internal static int Untouched(Outcome outcome) => outcome switch
                {
                    Outcome.Done(var state) => state.Label.Length,
                    Outcome.Skipped => 0,
                };

                internal static int Branched(Outcome outcome) => outcome switch
                {
                    Outcome.Done(PaymentState.Pending) => 1,
                    Outcome.Done(var other) => other.Label.Length,
                    Outcome.Skipped => 0,
                };

                internal static int Explicit(Outcome outcome) => outcome switch
                {
                    Outcome.Done(PaymentState.Pending) => 1,
                    Outcome.Done(PaymentState.Settled) => 2,
                    Outcome.Done(PaymentState.Failed) => 3,
                    Outcome.Skipped => 0,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["var other"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>入力の組の各要素の位置を、組の式、組を返す呼び出し、入れ子の組のどれでも、どの arm も派生を挙げなくても守ること</summary>
    [Test]
    public async Task Every_closed_element_of_a_tuple_input_is_protected()
    {
        var diagnostics = await RunClosed(Payment() + """

            internal static class Handler
            {
                internal static int Complete(PaymentState left, PaymentState right) => (left, right) switch
                {
                    (PaymentState.Pending, PaymentState.Pending or PaymentState.Settled or PaymentState.Failed) => 1,
                    (PaymentState.Settled or PaymentState.Failed, PaymentState.Pending or PaymentState.Settled or PaymentState.Failed) => 2,
                };

                internal static int Partial(PaymentState left, PaymentState right) => (left, right) switch
                {
                    (PaymentState.Pending, _) => 1,
                    (PaymentState.Settled, PaymentState.Failed) => 2,
                    (var other, PaymentState.Pending) => 3,
                };

                internal static (PaymentState, bool) Current() => (new PaymentState.Pending(), true);

                internal static int Returned() => Current() switch
                {
                    (_, true) => 1,
                    (_, false) => 2,
                };

                internal static int Nested(PaymentState left, PaymentState right, int number) => ((left, right), number) switch
                {
                    ((PaymentState.Pending, _), 1) => 1,
                    ((var other, PaymentState.Pending), _) => 2,
                    _ => 3,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["_", "var other", "_", "_", "_", "var other"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>閉じた class の中の閉じた class を、一部の派生を挙げた後に丸ごと受け取る arm の拒否</summary>
    [Test]
    public async Task A_nested_closed_class_arm_is_rejected_after_naming_one_of_its_subtypes()
    {
        var diagnostics = await RunClosed(ShapeDeclaration + """


            internal static class Handler
            {
                internal static int Split(Shape shape) => shape switch
                {
                    Shape.Circle => 1,
                    Shape.Rect.Square => 2,
                    Shape.Rect => 3,
                };

                internal static int Whole(Shape shape) => shape switch
                {
                    Shape.Circle => 1,
                    Shape.Rect => 2,
                };

                internal static int Complete(Shape shape) => shape switch
                {
                    Shape.Circle => 1,
                    Shape.Rect.Square => 2,
                    Shape.Rect.Oblong => 3,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["Shape.Rect"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>閉じていない型の switch の fallback は、診断を設定しても許すこと</summary>
    [Test]
    public async Task Open_types_keep_their_fallback_arms()
    {
        var diagnostics = await RunClosed("""
            namespace Probe;

            public abstract record Open;
            public sealed record Leaf : Open;

            public enum Plain { None, Some }

            internal static class Handler
            {
                internal static int Records(Open value) => value switch { Leaf => 1, _ => 0 };

                internal static int Objects(object value) => value switch { int => 1, string => 2, _ => 0 };

                internal static int Enums(Plain value) => value switch { Plain.None => 1, _ => 0 };

                internal static int Statement(Open value)
                {
                    switch (value)
                    {
                        case Leaf: return 1;
                        default: return 0;
                    }
                }
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>閉じた class に制約された型引数の位置の、catch-all の拒否</summary>
    [Test]
    public async Task A_type_parameter_constrained_to_a_closed_class_is_protected()
    {
        var diagnostics = await RunClosed(Payment() + """

            internal static class Handler
            {
                internal static int Describe<TState>(TState state) where TState : PaymentState => state switch
                {
                    PaymentState.Pending => 1,
                    TState other => other.Label.Length,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["TState other"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
