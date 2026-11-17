using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers.Diagnostics.ValueObject;
using static TypeModeling.Analyzers.Tests.ValueObjectGeneratorDiagnosticFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>値オブジェクトの new による構築を検出する analyzer の実行基盤</summary>
internal static class ValueObjectConstructionFixture
{
    /// <summary>generator を適用した compilation への analyzer の実行</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <param name="otherGeneratorSource">追加しなければ null の別の generator が追加する source</param>
    /// <returns>generator の実行結果と、TYPMOD024 が指す式の source 文字列</returns>
    internal static async Task<(ValueObjectGeneratorResult Result, string[] Flagged)> Analyze(
        string source,
        string? otherGeneratorSource = null
    )
    {
        var result = RunGenerator(source, otherGeneratorSource);
        var diagnostics = await result
            .Compilation.WithAnalyzers([new ValueObjectConstructionAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
        var flagged = diagnostics
            .Where(diagnostic => diagnostic.Id == "TYPMOD024")
            .Select(diagnostic =>
                diagnostic.Location.SourceTree?.GetText().ToString(diagnostic.Location.SourceSpan)
                ?? "<no source>"
            )
            .ToArray();

        return (result, flagged);
    }
}
