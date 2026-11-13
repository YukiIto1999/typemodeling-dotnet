using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>閉じた enum への switch が、後から足す member を受け取る arm を持たないことを、診断の設定で求めること</summary>
public sealed class ExplicitCaseClosedEnumTests
{
    /// <summary>閉じた enum と、閉じていない enum の宣言</summary>
    private const string Colors = """
        using TypeModeling.Domain;

        namespace Probe;

        [Closed]
        public enum Color
        {
            Red,
            Green,
            Blue,
        }

        public enum Plain
        {
            None,
            Some,
        }
        """;

    /// <summary>member を一つ挙げた後の、他の全ての member を受け取る arm の拒否</summary>
    /// <param name="arm">member を挙げた後に置く arm</param>
    /// <param name="flagged">拒否される pattern の source</param>
    [Test]
    [Arguments("_ => 0", "_")]
    [Arguments("var other => 0", "var other")]
    [Arguments("Color other => 0", "Color other")]
    [Arguments(">= Color.Green => 0", ">= Color.Green")]
    [Arguments("not Color.Red => 0", "not Color.Red")]
    public async Task Arms_that_accept_every_other_member_are_rejected(string arm, string flagged)
    {
        var diagnostics = await RunClosed(
            Colors
                + $$"""

                internal static class Handler
                {
                    internal static int Describe(Color color) => color switch
                    {
                        Color.Red => 1,
                        {{arm}},
                    };
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo([flagged]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>member を一つずつ挙げる arm、or、null の許可と、閉じていない enum の fallback の許可</summary>
    [Test]
    public async Task Named_members_are_accepted_and_open_enums_keep_their_fallback()
    {
        var diagnostics = await RunClosed(
            Colors
                + """

                internal static class Handler
                {
                    internal static int Members(Color color) => color switch
                    {
                        Color.Red => 1,
                        Color.Green => 2,
                        Color.Blue => 3,
                    };

                    internal static int Alternatives(Color color) => color switch
                    {
                        Color.Red or Color.Green => 1,
                        Color.Blue => 2,
                    };

                    internal static int Optional(Color? color) => color switch
                    {
                        null => 0,
                        Color.Red => 1,
                        Color.Green => 2,
                        Color.Blue => 3,
                    };

                    internal static int Open(Plain plain) => plain switch
                    {
                        Plain.None => 1,
                        _ => 2,
                    };
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>switch 文の default と、全ての member を挙げた case の区別</summary>
    [Test]
    public async Task A_default_label_on_a_closed_enum_is_rejected()
    {
        var diagnostics = await RunClosed(
            Colors
                + """

                internal static class Handler
                {
                    internal static int Fallback(Color color)
                    {
                        var result = 0;
                        switch (color)
                        {
                            case Color.Red: result = 1; break;
                            default: result = 2; break;
                        }

                        return result;
                    }

                    internal static int Named(Color color)
                    {
                        var result = 0;
                        switch (color)
                        {
                            case Color.Red: result = 1; break;
                            case Color.Green: result = 2; break;
                            case Color.Blue: result = 3; break;
                        }

                        return result;
                    }
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["default:"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
