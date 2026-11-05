using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers.Diagnostics.ClosedEnum;
using TypeModeling.Analyzers.Diagnostics.ClosedType;
using TypeModeling.Analyzers.Diagnostics.UnreachableArm;
using TypeModeling.Analyzers.Generation.ClosedSubtypeCount;
using TypeModeling.Analyzers.Suppression.Exhaustiveness;

namespace TypeModeling.Analyzers.Tests;

/// <summary>閉じた型の診断と抑止の実行基盤</summary>
internal static class ClosedTypeDiagnosticFixture
{
    // attribute は metadata 名で判定されるため実 assembly を参照しない同名 attribute の合成
    private const string ClosedAttributeSource = """
        namespace TypeModeling.Domain;

        [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
        public sealed class ClosedAttribute : System.Attribute;

        [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
        public sealed class ClosedSubtypeCountAttribute(System.Type closedType, int directSubtypeCount) : System.Attribute
        {
            public System.Type ClosedType { get; } = closedType;
            public int DirectSubtypeCount { get; } = directSubtypeCount;
        }
        """;

    /// <summary>閉じた型の analyzer と suppressor を全て当てた診断</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <param name="references">追加で参照する assembly</param>
    /// <returns>抑止された診断を含む全ての診断</returns>
    internal static async Task<ImmutableArray<Diagnostic>> Run(string source, params MetadataReference[] references)
    {
        var compilation = Compilation("closed-type-probe", [source, ClosedAttributeSource], references);
        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: null,
            concurrentAnalysis: false,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: true);
        var withAnalyzers = compilation.WithAnalyzers(
            [new ExhaustivenessSuppressor(), new ClosedTypeDeclarationAnalyzer(), new UnreachableArmAnalyzer(), new ClosedEnumAnalyzer()],
            options);

        return await withAnalyzers.GetAllDiagnosticsAsync();
    }

    /// <summary>閉じた型を宣言する別 assembly の構築</summary>
    /// <param name="source">別 assembly の C# source</param>
    /// <param name="recordSubtypeCounts">直接の派生の数を記録する generator を通す場合に true</param>
    /// <returns>検査対象から参照する metadata</returns>
    internal static MetadataReference Library(string source, bool recordSubtypeCounts = true)
    {
        Compilation compilation = Compilation("closed-type-library", [source, ClosedAttributeSource], []);
        if (recordSubtypeCounts)
        {
            CSharpGeneratorDriver.Create(new ClosedSubtypeCountGenerator())
                .RunGeneratorsAndUpdateCompilation(compilation, out compilation, out _);
        }

        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));

        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    /// <summary>直接の派生の数を記録する generator が足す source</summary>
    /// <param name="source">閉じた型を宣言する C# source</param>
    /// <returns>generator が足した source</returns>
    internal static string SubtypeCountSource(string source)
    {
        Compilation compilation = Compilation("closed-type-library", [source, ClosedAttributeSource], []);
        var result = CSharpGeneratorDriver.Create(new ClosedSubtypeCountGenerator()).RunGenerators(compilation).GetRunResult();
        return result.GeneratedTrees.Single().ToString();
    }

    /// <summary>指定した ID の、抑止されていない診断</summary>
    /// <param name="diagnostics">検査結果の診断</param>
    /// <param name="id">探す診断の ID</param>
    /// <returns>抑止されていない診断</returns>
    internal static ImmutableArray<Diagnostic> Active(ImmutableArray<Diagnostic> diagnostics, string id) =>
        [.. diagnostics.Where(diagnostic => diagnostic.Id == id && !diagnostic.IsSuppressed)];

    /// <summary>指定した ID の、抑止された診断</summary>
    /// <param name="diagnostics">検査結果の診断</param>
    /// <param name="id">探す診断の ID</param>
    /// <returns>抑止された診断</returns>
    internal static ImmutableArray<Diagnostic> Suppressed(ImmutableArray<Diagnostic> diagnostics, string id) =>
        [.. diagnostics.Where(diagnostic => diagnostic.Id == id && diagnostic.IsSuppressed)];

    /// <summary>nullable を有効にした compilation の構築</summary>
    /// <param name="name">assembly 名の接頭辞</param>
    /// <param name="sources">C# source</param>
    /// <param name="references">追加で参照する assembly</param>
    /// <returns>構築した compilation</returns>
    private static CSharpCompilation Compilation(string name, string[] sources, MetadataReference[] references)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        return CSharpCompilation.Create(
            $"{name}-{Guid.NewGuid():N}",
            [.. sources.Select(source => CSharpSyntaxTree.ParseText(source, parseOptions))],
            [.. PlatformReferences(), .. references],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>実行環境の framework assembly の参照</summary>
    /// <returns>framework の metadata 参照</returns>
    private static ImmutableArray<MetadataReference> PlatformReferences() =>
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
}
