using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TypeModeling.Analyzers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace TypeModeling.Tests;

/// <summary>値オブジェクト生成器が検証失敗の型を保った構築関数を生成することの確認</summary>
public sealed class ValueObjectGeneratorTests
{
    /// <summary>Parse が返す失敗型を保つ Create の生成</summary>
    [Test]
    public async Task Create_uses_the_failure_type_returned_by_Parse()
    {
        const string source = """
            namespace TypeModeling.Domain
            {
                [System.AttributeUsage(System.AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
                public sealed class ValueObjectAttribute<T> : System.Attribute;

                public abstract record Result<TValue, TFailure>
                {
                    public Result<TNext, TFailure> Map<TNext>(System.Func<TValue, TNext> selector) => throw null!;
                }
            }

            namespace Probe
            {
                public sealed record ValidationFailure;

                [TypeModeling.Domain.ValueObject<int>]
                public sealed partial record Positive
                {
                    private static TypeModeling.Domain.Result<int, TFailure> Parse<TFailure>(int raw) => throw null!;

                    private static TypeModeling.Domain.Result<int, ValidationFailure> Parse(int raw) => throw null!;
                }
            }
            """;

        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create(
            $"value-object-generator-probe-{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [new ValueObjectGenerator().AsSourceGenerator()],
            parseOptions: parseOptions);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out _);

        var generated = driver.GetRunResult().Results
            .Single()
            .GeneratedSources
            .Single()
            .SourceText
            .ToString()
            .ReplaceLineEndings("\n");
        await Assert.That(generated.Contains("using TypeModeling.Domain;", StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Split('\n').Count(line => line.StartsWith("using ", StringComparison.Ordinal)))
            .IsEqualTo(1);
        await Assert.That(generated.Contains(
            "public static Result<Positive, global::Probe.ValidationFailure> Create(int raw)",
            StringComparison.Ordinal)).IsTrue();
        await Assert.That(generated.Contains(
            """
                /// <summary>検証済みの基底値による値オブジェクトの内部構築</summary>
                /// <param name="value">検証済みの基底値</param>
                private Positive(int value) => Value = value;
            """,
            StringComparison.Ordinal)).IsTrue();
        await Assert.That(outputCompilation.GetDiagnostics()
            .Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)).IsEqualTo(0);
    }

    private static ImmutableArray<MetadataReference> References() =>
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
}
