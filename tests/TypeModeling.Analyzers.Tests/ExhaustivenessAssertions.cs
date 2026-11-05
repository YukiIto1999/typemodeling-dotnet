using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using static TypeModeling.Analyzers.Tests.ClosedTypeDiagnosticFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>switch 式の網羅の判定の検証</summary>
internal static class ExhaustivenessAssertions
{
    /// <summary>網羅していない switch 式の compiler 診断の ID</summary>
    private static readonly string[] NotExhaustiveIds = ["CS8509", "CS8524", "CS8846"];

    /// <summary>analyzer か suppressor の例外を報告する診断の ID</summary>
    private const string AnalyzerExceptionId = "AD0001";

    /// <summary>エラーなく compile でき、C# 15 と同じく網羅と判定され、網羅の診断が全て抑止されること</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <param name="references">追加で参照する assembly</param>
    /// <returns>検証の完了</returns>
    internal static async Task AssertExhaustive(string source, params Microsoft.CodeAnalysis.MetadataReference[] references)
    {
        var diagnostics = await Run(source, references);
        var reported = NotExhaustiveIds.SelectMany(id => diagnostics.Where(diagnostic => diagnostic.Id == id)).ToArray();

        await Assert.That(reported).IsNotEmpty();
        await Assert.That(reported.Where(diagnostic => !diagnostic.IsSuppressed)).IsEmpty();
        await Assert.That(Active(diagnostics, AnalyzerExceptionId)).IsEmpty();
        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)).IsEmpty();
    }

    /// <summary>C# 15 でも網羅でなく、網羅していない警告が抑止されずに残ること</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <param name="references">追加で参照する assembly</param>
    /// <returns>検証の完了</returns>
    /// <remarks>when だけが残りを扱う場合、C# 15 の CS8846 に対し C# 14 以前の compiler は CS8509 を出すので、ID は問わない</remarks>
    internal static async Task AssertNotExhaustive(string source, params Microsoft.CodeAnalysis.MetadataReference[] references)
    {
        var diagnostics = await Run(source, references);

        await Assert.That(NotExhaustiveIds.SelectMany(id => Active(diagnostics, id))).IsNotEmpty();
        await Assert.That(Active(diagnostics, AnalyzerExceptionId)).IsEmpty();
    }
}
