using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ClosedTypeDiagnosticFixture;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>Deconstruct が property を返すと確かめられない record は、positional と property を混ぜず、一方の書き方で揃えた arm を許可すること</summary>
public sealed class ExplicitCaseReferencedRecordTests
{
    /// <summary>別の assembly で宣言した、閉じた class と、それを持つ record の宣言</summary>
    private const string LibrarySource = """
        namespace Library
        {
            [TypeModeling.Domain.Closed]
            public abstract record Signal
            {
                private Signal() { }
                public sealed record Red : Signal;
                public sealed record Green : Signal;
            }

            public sealed record Lamp(Signal Light, int Brightness);
        }
        """;

    /// <summary>別の assembly の record を、positional pattern だけ、または property pattern だけで書く arm の許可</summary>
    [Test]
    public async Task A_referenced_record_is_accepted_when_one_form_names_every_case()
    {
        var diagnostics = await RunClosed("""
            using Library;

            internal static class Handler
            {
                internal static int Positional(Lamp lamp) => lamp switch
                {
                    Lamp(Signal.Red, _) => 1,
                    Lamp(Signal.Green, _) => 2,
                };

                internal static int ByProperty(Lamp lamp) => lamp switch
                {
                    { Light: Signal.Red } => 1,
                    { Light: Signal.Green } => 2,
                };
            }
            """, true, Library(LibrarySource));

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>別の assembly の record の Deconstruct は、compiler が作ったと確かめられないので、property pattern と混ぜた arm の拒否</summary>
    [Test]
    public async Task A_referenced_record_deconstruct_is_never_merged_with_its_property()
    {
        var diagnostics = await RunClosed("""
            using Library;

            internal static class Handler
            {
                internal static int Describe(Lamp lamp) => lamp switch
                {
                    Lamp(Signal.Red, _) => 1,
                    { Light: Signal.Green } => 2,
                };
            }
            """, true, Library(LibrarySource));

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["Lamp(Signal.Red, _)", "{ Light: Signal.Green }"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>CompilerGenerated を付けて書いた Deconstruct は、compiler が作った Deconstruct と同じにせず、property と混ぜないこと</summary>
    [Test]
    public async Task A_hand_written_deconstruct_marked_compiler_generated_is_never_merged_with_the_property()
    {
        var diagnostics = await RunClosed(Payment() + """

            internal sealed record Forged(PaymentState State)
            {
                public PaymentState Previous { get; init; } = new PaymentState.Pending();

                [System.Runtime.CompilerServices.CompilerGenerated]
                public void Deconstruct(out PaymentState state) => state = Previous;
            }

            internal static class Handler
            {
                internal static int PropertyOmitsSlot(Forged forged) => forged switch
                {
                    Forged(PaymentState.Pending) => 1,
                    { State: var current } => current.Label.Length,
                };

                internal static int SlotOmitsProperty(Forged forged) => forged switch
                {
                    { State: PaymentState.Pending } => 1,
                    Forged(var shown) => shown.Label.Length,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["{ State: var current }", "Forged(var shown)"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
