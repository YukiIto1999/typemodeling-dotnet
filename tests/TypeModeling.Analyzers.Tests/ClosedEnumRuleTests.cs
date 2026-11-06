using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ClosedTypeDiagnosticFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>閉じた enum が名前付きの member 以外の値を取らないための、C# 16 の closed enum と同じ規則</summary>
public sealed class ClosedEnumRuleTests
{
    /// <summary>三つの名前付き member を持つ閉じた enum の宣言</summary>
    private const string Color = """
        using TypeModeling.Domain;

        [Closed]
        public enum Color
        {
            Red,
            Green,
            Blue,
        }

        public enum Open
        {
            None,
        }
        """;

    /// <summary>0 に当たる member のない閉じた enum の TYPMOD018</summary>
    [Test]
    public async Task A_closed_enum_needs_a_member_for_zero()
    {
        var diagnostics = await Run("""
            using TypeModeling.Domain;

            [Closed]
            public enum Level
            {
                Low = 1,
                High = 2,
            }
            """);

        await Assert.That(Active(diagnostics, "TYPMOD018").Length).IsEqualTo(1);
    }

    /// <summary>member に当たる定数だけを許す変換の TYPMOD019</summary>
    [Test]
    public async Task Only_constants_of_declared_members_convert_to_a_closed_enum()
    {
        var diagnostics = await Run(Color + """
            internal static class Use
            {
                internal static Color Zero() => 0;
                internal static Color Member() => (Color)1;
                internal static Color Outside() => (Color)10;
                internal static Color Variable(int value) => (Color)value;
                internal static Color Same(Color color) => (Color)color;
                internal static Open OpenOutside() => (Open)10;
            }
            """);

        await Assert.That(Texts(diagnostics, "TYPMOD019")).IsEquivalentTo(["(Color)10", "(Color)value"]);
    }

    /// <summary>member に当たらない値を作る演算の TYPMOD020 と、比較の許可</summary>
    [Test]
    public async Task Operators_that_may_leave_the_declared_members_are_rejected()
    {
        var diagnostics = await Run(Color + """
            internal static class Use
            {
                internal static Color NextConstant() => Color.Red + 1;
                internal static Color NextVariable(Color color) => color + 1;
                internal static bool Compare(Color color) => color == Color.Blue;
                internal static Color Increment(Color color) => ++color;
                internal static Open OpenNext(Open open) => open + 1;
                internal static int Sum(int left, int right) => left + right;
            }
            """);

        await Assert.That(Texts(diagnostics, "TYPMOD020")).IsEquivalentTo(["color + 1", "++color"]);
    }

    /// <summary>全ての member の後の enum 型の arm の TYPMOD006</summary>
    [Test]
    public async Task An_enum_type_arm_after_every_member_is_unreachable()
    {
        var diagnostics = await Run(Color + """
            internal static class Use
            {
                internal static int Describe(Color color) => color switch
                {
                    Color.Red => 1,
                    Color.Green => 2,
                    Color.Blue => 3,
                    Color => 4,
                };
            }
            """);

        await Assert.That(Active(diagnostics, "TYPMOD006").Length).IsEqualTo(1);
    }

    /// <summary>指定した ID の抑止されていない診断が指す source の文字列</summary>
    /// <param name="diagnostics">検査結果の診断</param>
    /// <param name="id">探す診断の ID</param>
    /// <returns>診断の位置の source の文字列</returns>
    private static IEnumerable<string> Texts(System.Collections.Immutable.ImmutableArray<Microsoft.CodeAnalysis.Diagnostic> diagnostics, string id) =>
        Active(diagnostics, id).Select(diagnostic => diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
}
