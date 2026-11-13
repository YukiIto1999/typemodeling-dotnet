using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>case を挙げて分岐した中身を持つ、閉じていない入力の switch 文の default の label を、同じ中身を隠す束縛の case と同じく拒否すること</summary>
public sealed class ExplicitCaseOpenDefaultTests
{
    /// <summary>閉じた型を中身に持つ閉じていない record の宣言</summary>
    private const string Declarations = """

        [Closed]
        public enum Delivery { Pending, Complete }

        internal sealed record Receipt(PaymentState State, int Count);

        internal sealed record Parcel(PaymentState First, PaymentState Second, Delivery Mode);
        """;

    /// <summary>他の case が中身で分岐しているときの default の label の拒否と、どの case も分岐していないときの許可</summary>
    [Test]
    public async Task A_default_label_is_rejected_where_another_case_branched_on_the_payload()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

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
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["default:", "var rest"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>default の label が隠す、分岐した中身の異なる閉じた型を一つずつ、同じ型は重複なく診断すること</summary>
    [Test]
    public async Task A_default_label_identifies_each_distinct_hidden_family_once()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

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
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["default:", "default:"]);
        await Assert.That(ReportedMessages(diagnostics)).IsEquivalentTo(MessagesFor(diagnostics, "PaymentState", "Delivery"));
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
