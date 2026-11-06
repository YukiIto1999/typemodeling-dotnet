using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers.Diagnostics.UnionType;
using TypeModeling.Analyzers.Diagnostics.UnreachableArm;
using TypeModeling.Analyzers.Generation.Union;
using TypeModeling.Analyzers.Suppression.Exhaustiveness;

namespace TypeModeling.Analyzers.Tests;

/// <summary>union の生成と診断と抑止の実行基盤</summary>
internal static class UnionFixture
{
    /// <summary>union の generator を通した compilation への analyzer と suppressor の適用</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <param name="languageVersion">検査対象の言語版</param>
    /// <returns>compiler と analyzer の全ての診断、抑止された診断を含む</returns>
    internal static async Task<ImmutableArray<Diagnostic>> Run(string source, LanguageVersion languageVersion = LanguageVersion.CSharp14)
    {
        var compilation = Generate(source, languageVersion);
        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: null,
            concurrentAnalysis: false,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: true);
        var withAnalyzers = compilation.WithAnalyzers(
            [new ExhaustivenessSuppressor(), new UnionTypeAnalyzer(), new UnreachableArmAnalyzer()],
            options);

        return await withAnalyzers.GetAllDiagnosticsAsync();
    }

    /// <summary>union の generator が足した source</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <param name="languageVersion">検査対象の言語版</param>
    /// <returns>generator が足した source の連結</returns>
    internal static string GeneratedSource(string source, LanguageVersion languageVersion = LanguageVersion.CSharp14)
    {
        var generated = Generate(source, languageVersion).SyntaxTrees.Skip(1).Select(tree => tree.ToString());
        return string.Join(Environment.NewLine, generated);
    }

    /// <summary>union の generator が足した source の file 名</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <returns>generator が足した source の file 名</returns>
    internal static IEnumerable<string> GeneratedFileNames(string source) =>
        Generate(source, LanguageVersion.CSharp14).SyntaxTrees.Skip(1).Select(tree => Path.GetFileName(tree.FilePath));

    /// <summary>エラーの重大度の診断</summary>
    /// <param name="diagnostics">検査結果の診断</param>
    /// <returns>エラーの診断</returns>
    internal static ImmutableArray<Diagnostic> Errors(ImmutableArray<Diagnostic> diagnostics) =>
        [.. diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];

    /// <summary>union の generator を通した compilation の構築</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <param name="languageVersion">検査対象の言語版</param>
    /// <returns>生成 source を含む compilation</returns>
    private static Compilation Generate(string source, LanguageVersion languageVersion)
    {
        var parseOptions = new CSharpParseOptions(languageVersion);
        var compilation = CSharpCompilation.Create(
            $"union-probe-{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            CompilationReferences.Platform,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        CSharpGeneratorDriver
            .Create([new UnionGenerator().AsSourceGenerator()], parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var generated, out _);
        return generated;
    }
}
