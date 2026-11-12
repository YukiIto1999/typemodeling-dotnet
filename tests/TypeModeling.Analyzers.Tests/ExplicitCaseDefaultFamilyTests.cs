using System.Globalization;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>default label が隠す閉じた型の集まりの診断</summary>
public sealed class ExplicitCaseDefaultFamilyTests
{
    /// <summary>組の default が隠す異なる集まりを重複なく示す診断</summary>
    /// <returns>診断の検査完了</returns>
    [Test]
    public async Task A_tuple_default_identifies_every_hidden_family_without_repeating_the_same_family()
    {
        var diagnostics = await RunClosed(Payment() + """

            [Closed]
            public enum Delivery { Pending, Complete }

            internal static class Handler
            {
                internal static int Describe(PaymentState first, PaymentState second, Delivery delivery)
                {
                    switch ((first, (second, delivery)))
                    {
                        default: return 0;
                    }
                }
            }
            """);
        var reports = diagnostics.Where(diagnostic => diagnostic.Id == Id).ToArray();

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["default:", "default:"]);
        var format = reports[0].Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture);
        var expectedPayment = string.Format(CultureInfo.InvariantCulture, format, "PaymentState");
        var expectedDelivery = string.Format(CultureInfo.InvariantCulture, format, "Delivery");
        await Assert.That(reports.Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture)))
            .IsEquivalentTo([expectedPayment, expectedDelivery]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
