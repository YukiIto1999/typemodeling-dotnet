using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>開いた入力の既定分岐が隠す閉じた型の診断</summary>
public sealed class ExplicitCaseOpenDefaultTests
{
    /// <summary>閉じた型を中身に持つ閉じていない record の宣言</summary>
    private const string Declarations = """

        [Closed]
        public enum Delivery { Pending, Complete }

        internal sealed record Receipt(PaymentState State, int Count);

        internal sealed record Parcel(PaymentState First, PaymentState Second, Delivery Mode);
        """;

    /// <summary>明示した case に基づく payload の既定分岐の保護</summary>
    /// <returns>分岐による保護の有効化の検証の完了</returns>
    [Test]
    public async Task A_default_label_is_rejected_where_another_case_branched_on_the_payload()
    {
        var diagnostics = await RunClosed(
            Payment()
                + Declarations
                + """

                internal static class Handler
                {
                    internal static int Default(Receipt receipt)
                    {
                        var result = 0;
                        switch (receipt)
                        {
                            case { State: PaymentState.Pending }: result = 1; break;
                            default: result = 2; break;
                        }

                        return result;
                    }

                    internal static int Bound(Receipt receipt)
                    {
                        var result = 0;
                        switch (receipt)
                        {
                            case { State: PaymentState.Pending }: result = 1; break;
                            case var rest: result = rest.Count; break;
                        }

                        return result;
                    }

                    internal static int Untouched(Receipt receipt)
                    {
                        var result = 0;
                        switch (receipt)
                        {
                            case { Count: 0 }: result = 1; break;
                            default: result = 2; break;
                        }

                        return result;
                    }
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["default:", "var rest"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>既定分岐が隠す閉じた型ごとの重複しない診断</summary>
    /// <returns>異なる型を区別する診断の検証の完了</returns>
    [Test]
    public async Task A_default_label_identifies_each_distinct_hidden_family_once()
    {
        var diagnostics = await RunClosed(
            Payment()
                + Declarations
                + """

                internal static class Handler
                {
                    internal static int Describe(Parcel parcel)
                    {
                        var result = 0;
                        switch (parcel)
                        {
                            case { First: PaymentState.Pending, Second: PaymentState.Pending, Mode: Delivery.Pending }: result = 1; break;
                            default: result = 2; break;
                        }

                        return result;
                    }
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["default:", "default:"]);
        await Assert
            .That(ReportedMessages(diagnostics))
            .IsEquivalentTo(MessagesFor(diagnostics, "PaymentState", "Delivery"));
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>一つの payload 位置で明示した入れ子の閉じた型の既定分岐の診断</summary>
    /// <returns>階層ごとに異なる診断が返ることの検証の完了</returns>
    [Test]
    public async Task A_default_label_reports_every_activated_level_of_a_closed_hierarchy()
    {
        var diagnostics = await RunClosed(
            """
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

            internal sealed record Envelope(Shape Value);

            internal static class Handler
            {
                internal static int Describe(Envelope envelope)
                {
                    switch (envelope)
                    {
                        case { Value: Shape.Rect.Square }: return 1;
                        default: return 0;
                    }
                }
            }
            """
        );

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["default:", "default:"]);
        await Assert.That(ReportedMessages(diagnostics).Distinct().Count()).IsEqualTo(2);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
