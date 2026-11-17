using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ValueObjectConstructionFixture;
using static TypeModeling.Analyzers.Tests.ValueObjectSources;

namespace TypeModeling.Analyzers.Tests;

/// <summary>別の generator が通常の出力で足した member に対する診断</summary>
/// <remarks>通常の出力は ValueObject の generator の入力にならないので、最終の compilation を見る analyzer だけが検出できる</remarks>
public sealed class Typmod023GeneratedMemberTests
{
    /// <summary>別の generator の複製コンストラクタが、new を使わない with の複製で検証を迂回する経路の遮断</summary>
    [Test]
    public async Task Typmod023_blocks_a_copy_constructor_that_another_generator_adds_for_with()
    {
        var analysis = await Observe(
            Declare(SealedPositive)
                + """

                public static class Consumer
                {
                    public static Positive Copy(Positive valid) => valid with { };
                }
                """,
            """
            namespace Probe;

            public sealed partial record Positive
            {
                private Positive(Positive original) { Value = 0; }
            }
            """
        );

        await AssertCompiles(analysis);
        await AssertTypmod023(analysis, "コンストラクタ Positive(Positive) を宣言している。");
    }

    /// <summary>別の generator が足した、コンストラクタと書き込める状態の診断</summary>
    /// <param name="members">別の generator が型の本体へ足す member</param>
    /// <param name="reasons">期待する不適合理由</param>
    /// <returns>非同期 assertion</returns>
    [Test]
    [Arguments("public Positive() { }", "コンストラクタ Positive() を宣言している。")]
    [Arguments(
        "public int Extra { get; set; }",
        "set か init を持つ property Extra を宣言している。"
    )]
    [Arguments("public int Extra;", "書き込める field Extra を宣言している。")]
    [Arguments(
        "public int this[int index] { get => 0; init { } }",
        "set か init を持つ indexer this[int] を宣言している。"
    )]
    public async Task Typmod023_reports_each_member_that_another_generator_adds(
        string members,
        string reasons
    )
    {
        var analysis = await Observe(
            Declare(SealedPositive),
            $$"""
            namespace Probe;

            public sealed partial record Positive
            {
                {{members}}
            }
            """
        );

        await AssertTypmod023(analysis, reasons);
    }

    /// <summary>別の generator が足した、interface の明示実装の indexer の診断</summary>
    [Test]
    public async Task Typmod023_reports_an_explicit_interface_indexer_that_another_generator_adds()
    {
        var analysis = await Observe(
            Declare(SealedPositive),
            """
            namespace Probe;

            public interface ISlots
            {
                int this[int index] { get; set; }
            }

            public sealed partial record Positive : ISlots
            {
                int ISlots.this[int index] { get => 0; set { } }
            }
            """
        );

        await AssertCompiles(analysis);
        await AssertTypmod023(analysis, "set か init を持つ indexer this[int] を宣言している。");
    }
}
