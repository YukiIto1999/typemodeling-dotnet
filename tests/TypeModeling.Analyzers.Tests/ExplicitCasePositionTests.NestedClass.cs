using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>閉じた型の階層内の位置ごとの明示的な分岐</summary>
public sealed partial class ExplicitCasePositionTests
{
    /// <summary>閉じた class を入れ子に持つ閉じた record の宣言</summary>
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

    /// <summary>派生を個別に指定した閉じた型の一括分岐の拒否</summary>
    /// <returns>診断対象と compiler error の比較完了</returns>
    [Test]
    public async Task A_nested_closed_class_arm_is_rejected_after_naming_one_of_its_subtypes()
    {
        var diagnostics = await RunClosed(
            ShapeDeclaration
                + """


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
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["Shape.Rect"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
