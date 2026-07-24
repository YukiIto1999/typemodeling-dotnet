using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers.Diagnostics.ClosedUnionSwitchStatement;
using TypeModeling.Analyzers.Suppression.ClosedUnionExhaustiveness;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace TypeModeling.Analyzers.Tests;

/// <summary>閉じた型診断の実行基盤</summary>
internal static class ClosedTypeDiagnosticFixture
{
    // attribute は metadata 名で判定されるため実 assembly を参照しない同名 attribute の合成
    private const string ClosedUnionAttributeSource = """
        namespace TypeModeling.Domain;

        [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
        public sealed class ClosedUnionAttribute : System.Attribute;
        """;

    internal static async Task<ImmutableArray<Diagnostic>> RunSuppressor(string source)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        var compilation = CSharpCompilation.Create(
            $"closed-union-enum-suppressor-probe-{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source, parseOptions), CSharpSyntaxTree.ParseText(ClosedUnionAttributeSource, parseOptions)],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
            onAnalyzerException: null,
            concurrentAnalysis: false,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: true);
        var withAnalyzers = compilation.WithAnalyzers(
            ImmutableArray.Create<DiagnosticAnalyzer>(
                new ClosedUnionExhaustivenessSuppressor(),
                new ClosedUnionSwitchStatementAnalyzer()), options);

        return await withAnalyzers.GetAllDiagnosticsAsync();
    }

    private static ImmutableArray<MetadataReference> References() =>
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
}
