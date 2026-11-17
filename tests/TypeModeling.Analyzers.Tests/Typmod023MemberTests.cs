using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ValueObjectConstructionFixture;
using static TypeModeling.Analyzers.Tests.ValueObjectSources;

namespace TypeModeling.Analyzers.Tests;

/// <summary>ValueObject の型の本体に書いた member に対する診断</summary>
public sealed class Typmod023MemberTests
{
    /// <summary>手書きのコンストラクタで Parse を迂回して Guid.Empty の値オブジェクトを作る経路の遮断</summary>
    [Test]
    public async Task Typmod023_blocks_a_public_constructor_that_bypasses_Parse()
    {
        var analysis = await Observe(
            """
            using TypeModeling.Domain;

            namespace Probe;

            public sealed record ValidationFailure;

            [ValueObject<System.Guid>]
            public sealed partial record Positive
            {
                public Positive() { }

                private static Result<System.Guid, ValidationFailure> Parse(System.Guid raw) => throw null!;
            }

            public static class Consumer
            {
                public static Positive Empty() => new Positive();
            }
            """
        );

        await AssertTypmod023(analysis, "コンストラクタ Positive() を宣言している。");
    }

    /// <summary>コンストラクタ、Value、書き込める状態のうち、迂回経路を足す member の診断</summary>
    /// <param name="members">型の本体へ足す member</param>
    /// <param name="reasons">期待する不適合理由</param>
    /// <returns>非同期 assertion</returns>
    [Test]
    [Arguments("public Positive() { }", "コンストラクタ Positive() を宣言している。")]
    [Arguments(
        "internal Positive(string text) { }",
        "コンストラクタ Positive(string) を宣言している。"
    )]
    [Arguments(
        "private Positive(int first, int second) { }",
        "コンストラクタ Positive(int, int) を宣言している。"
    )]
    [Arguments(
        "private Positive(Positive original) { }",
        "コンストラクタ Positive(Positive) を宣言している。"
    )]
    [Arguments("private Positive(int value) { }", "コンストラクタ Positive(int) を宣言している。")]
    [Arguments("public int Value => 1;", "生成する member の Value を宣言している。")]
    [Arguments(
        "public int Extra { get; set; }",
        "set か init を持つ property Extra を宣言している。"
    )]
    [Arguments(
        "public int Extra { get; init; }",
        "set か init を持つ property Extra を宣言している。"
    )]
    [Arguments("public int Extra;", "書き込める field Extra を宣言している。")]
    [Arguments("private int cached;", "書き込める field cached を宣言している。")]
    [Arguments(
        "public int this[int index] { get => 0; set { } }",
        "set か init を持つ indexer this[int] を宣言している。"
    )]
    [Arguments(
        "public int this[string key, int index] { get => 0; init { } }",
        "set か init を持つ indexer this[string, int] を宣言している。"
    )]
    [Arguments(
        "private readonly int[] cells = new int[1]; "
            + "public int this[int index] { get => cells[index]; set => cells[index] = value; }",
        "set か init を持つ indexer this[int] を宣言している。"
    )]
    public async Task Typmod023_reports_each_member_that_adds_a_path(string members, string reasons)
    {
        var analysis = await Observe(Declare(SealedPositive, members));

        await AssertTypmod023(analysis, reasons);
    }

    /// <summary>別の partial 宣言のコンストラクタの診断</summary>
    [Test]
    public async Task Typmod023_reports_a_constructor_in_another_partial_declaration()
    {
        var analysis = await Observe(
            Declare(SealedPositive)
                + """

                public partial record Positive
                {
                    public Positive() { }
                }
                """
        );

        await AssertTypmod023(analysis, "コンストラクタ Positive() を宣言している。");
    }

    /// <summary>構築も書込も足さない member の診断抑止</summary>
    [Test]
    public async Task Typmod023_does_not_report_for_members_that_add_no_path()
    {
        var analysis = await Observe(
            Declare(
                "public sealed partial record Positive : System.IComparable<Positive>",
                "private const int Limit = 10; "
                    + "private static int created; "
                    + "static Positive() { } "
                    + "public static int Created { get => created; set => created = value; } "
                    + "public bool IsSmall => Value < Limit; "
                    + "public int CompareTo(Positive other) => Value.CompareTo(other.Value);"
            )
        );

        await Assert.That(analysis.Diagnostics).IsEmpty();
        await Assert.That(analysis.Result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(analysis.Result.GeneratedSources.Length).IsEqualTo(1);
        await AssertCompiles(analysis);
    }
}
