using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ValueObjectGeneratorDiagnosticFixture;
using static TypeModeling.Analyzers.Tests.ValueObjectSources;

namespace TypeModeling.Analyzers.Tests;

/// <summary>ValueObject の型宣言の形に対する生成時診断</summary>
public sealed class Typmod023DeclarationTests
{
    /// <summary>基底型のない sealed partial record でない宣言の診断</summary>
    /// <param name="declaration">属性の直後に置く型宣言の頭</param>
    /// <param name="reasons">期待する不適合理由</param>
    /// <returns>非同期 assertion</returns>
    [Test]
    [Arguments("public partial record Positive", "sealed ではない。")]
    [Arguments("public sealed record Positive", "partial ではない。")]
    [Arguments("public sealed partial class Positive", "record ではない。")]
    [Arguments("public sealed partial record Positive : Base", "基底型 Base を持つ。")]
    [Arguments(
        "public sealed partial record Positive(int Amount)",
        "primary コンストラクタを宣言している。"
    )]
    public async Task Typmod023_reports_a_declaration_outside_the_supported_shape(
        string declaration,
        string reasons
    )
    {
        var result = RunGenerator(Declare(declaration));

        await AssertTypmod023(result, reasons);
    }

    /// <summary>複数の迂回経路を決めた順で一度に報告する診断</summary>
    [Test]
    public async Task Typmod023_reports_every_violation_at_once()
    {
        var result = RunGenerator(
            Declare(
                "public partial record Positive : Base",
                "public Positive() { } public int Extra { get; set; }"
            )
        );

        await AssertTypmod023(
            result,
            "sealed ではない。基底型 Base を持つ。"
                + "コンストラクタ Positive() を宣言している。"
                + "set か init を持つ property Extra を宣言している。"
        );
    }
}
