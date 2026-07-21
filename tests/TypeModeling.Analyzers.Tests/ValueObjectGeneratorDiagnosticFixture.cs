using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TypeModeling.Analyzers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace TypeModeling.Analyzers.Tests;

/// <summary>ValueObject generator 診断の実行基盤</summary>
internal static class ValueObjectGeneratorDiagnosticFixture
{
    /// <summary>ValueObject generator が参照する domain 宣言</summary>
    private const string DomainSource = """
        namespace TypeModeling.Domain
        {
            [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
            public sealed class ValueObjectAttribute<T> : System.Attribute;

            public abstract record Result<TValue, TFailure>
            {
                public Result<TNext, TFailure> Map<TNext>(System.Func<TValue, TNext> selector) => throw null!;
            }
        }
        """;

    /// <summary>ValueObject generator の実行</summary>
    /// <param name="source">検証対象 source</param>
    /// <returns>generator 実行結果</returns>
    internal static ValueObjectGeneratorResult RunGenerator(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create(
            $"value-object-generator-probe-{Guid.NewGuid():N}",
            [
                CSharpSyntaxTree.ParseText(DomainSource, parseOptions),
                CSharpSyntaxTree.ParseText(source, parseOptions),
            ],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ValueObjectGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics);
        var generatedSources = driver.GetRunResult().Results
            .SelectMany(result => result.GeneratedSources)
            .ToImmutableArray();

        return new ValueObjectGeneratorResult(
            outputCompilation,
            generatorDiagnostics,
            generatedSources);
    }

    /// <summary>TYPMOD002 の内容検証</summary>
    /// <param name="result">generator 実行結果</param>
    /// <param name="reason">期待する不適合理由</param>
    /// <returns>非同期 assertion</returns>
    internal static async Task AssertTypmod002(
        ValueObjectGeneratorResult result,
        string reason)
    {
        var diagnostic = result.GeneratorDiagnostics
            .Single(item => item.Id == "TYPMOD002");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Error);
        var sourceTree = diagnostic.Location.SourceTree
            ?? throw new InvalidOperationException("TYPMOD002 diagnostic has no source tree");
        var sourceText = await sourceTree
            .GetTextAsync(CancellationToken.None);
        await Assert.That(sourceText.ToString(diagnostic.Location.SourceSpan))
            .IsEqualTo("Positive");
        await Assert.That(diagnostic.GetMessage(
                System.Globalization.CultureInfo.InvariantCulture))
            .IsEqualTo(
                "型 Positive には private static Result<int, TFailure> Parse(int raw) が必要。" +
                reason);
        await Assert.That(result.GeneratedSources).IsEmpty();
    }

    /// <summary>generator probe の metadata 参照</summary>
    /// <returns>実行環境の metadata 参照</returns>
    private static ImmutableArray<MetadataReference> References() =>
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];

    /// <summary>ValueObject generator の観測結果</summary>
    /// <param name="Compilation">生成反映後 compilation</param>
    /// <param name="GeneratorDiagnostics">generator の診断</param>
    /// <param name="GeneratedSources">生成 source</param>
    internal sealed record ValueObjectGeneratorResult(
        Compilation Compilation,
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        ImmutableArray<GeneratedSourceResult> GeneratedSources);
}
