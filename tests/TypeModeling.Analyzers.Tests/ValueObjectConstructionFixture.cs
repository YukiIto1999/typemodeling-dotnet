using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TypeModeling.Analyzers.Diagnostics.ValueObject;
using static TypeModeling.Analyzers.Tests.ValueObjectGeneratorDiagnosticFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>値オブジェクトの宣言の形と new による構築を検出する analyzer の実行基盤</summary>
internal static class ValueObjectConstructionFixture
{
    /// <summary>generator を適用した compilation への値オブジェクトの analyzer の実行</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <param name="otherGeneratorSource">追加しなければ null の別の generator が追加する source</param>
    /// <returns>generator の実行結果と、analyzer が報告した診断</returns>
    internal static async Task<ValueObjectAnalysis> Observe(
        string source,
        string? otherGeneratorSource = null
    )
    {
        var result = RunGenerator(source, otherGeneratorSource);
        var diagnostics = await result
            .Compilation.WithAnalyzers([
                new ValueObjectShapeAnalyzer(),
                new ValueObjectConstructionAnalyzer(),
            ])
            .GetAnalyzerDiagnosticsAsync();

        return new ValueObjectAnalysis(result, diagnostics);
    }

    /// <summary>generator を適用した compilation への analyzer の実行</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <param name="otherGeneratorSource">追加しなければ null の別の generator が追加する source</param>
    /// <returns>generator の実行結果と、TYPMOD024 が指す式の source 文字列</returns>
    internal static async Task<(ValueObjectGeneratorResult Result, string[] Flagged)> Analyze(
        string source,
        string? otherGeneratorSource = null
    )
    {
        var analysis = await Observe(source, otherGeneratorSource);
        var flagged = analysis
            .Diagnostics.Where(diagnostic => diagnostic.Id == "TYPMOD024")
            .Select(diagnostic =>
                diagnostic.Location.SourceTree?.GetText().ToString(diagnostic.Location.SourceSpan)
                ?? "<no source>"
            )
            .ToArray();

        return (analysis.Result, flagged);
    }

    /// <summary>TYPMOD023 の内容検証</summary>
    /// <param name="analysis">generator と analyzer の観測結果</param>
    /// <param name="reasons">期待する不適合理由の連結</param>
    /// <returns>非同期 assertion</returns>
    internal static async Task AssertTypmod023(ValueObjectAnalysis analysis, string reasons)
    {
        var diagnostic = analysis
            .Result.GeneratorDiagnostics.Concat(analysis.Diagnostics)
            .Single(item => item.Id == "TYPMOD023");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        var sourceTree =
            diagnostic.Location.SourceTree
            ?? throw new InvalidOperationException("TYPMOD023 diagnostic has no source tree");
        var sourceText = await sourceTree.GetTextAsync(CancellationToken.None);
        await Assert
            .That(sourceText.ToString(diagnostic.Location.SourceSpan))
            .IsEqualTo("Positive");
        await Assert
            .That(diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture))
            .IsEqualTo(
                "型 Positive は検証を経ずに構築も書込もできない宣言にする。"
                    + "基底型のない sealed partial record とし、コンストラクタ、Value、書き込める field、property、indexer を宣言しない。"
                    + reasons
            );
    }

    /// <summary>compilation に compiler の error が無いことの確認</summary>
    /// <param name="analysis">generator と analyzer の観測結果</param>
    /// <returns>非同期 assertion</returns>
    internal static async Task AssertCompiles(ValueObjectAnalysis analysis) =>
        await Assert
            .That(
                analysis
                    .Result.Compilation.GetDiagnostics()
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic => diagnostic.Id)
            )
            .IsEmpty();

    /// <summary>generator と値オブジェクトの analyzer の観測結果</summary>
    /// <param name="Result">generator の実行結果</param>
    /// <param name="Diagnostics">analyzer が報告した診断</param>
    internal sealed record ValueObjectAnalysis(
        ValueObjectGeneratorResult Result,
        ImmutableArray<Diagnostic> Diagnostics
    );
}
