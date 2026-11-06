using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TypeModeling.Domain;

namespace TypeModeling.Testing.Tests.Closure;

/// <summary>構造検査 fixture の in-memory C# compiler</summary>
internal static class DynamicFixtureCompiler
{
    /// <summary>構築した fixture assembly の image</summary>
    private static readonly ConditionalWeakTable<Assembly, byte[]> Images = new();

    /// <summary>C# source からの動的 assembly 構築</summary>
    /// <param name="source">fixture の C# source</param>
    /// <param name="assemblyName">重複しない assembly 名</param>
    /// <param name="dependencies">参照する構築済みの fixture assembly</param>
    /// <returns>既定 load context へロードした fixture assembly</returns>
    internal static Assembly Compile(string source, string assemblyName, params Assembly[] dependencies)
    {
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            [.. References(), .. dependencies.Select(dependency => MetadataReference.CreateFromImage(Images.GetValue(
                dependency,
                _ => throw new InvalidOperationException(dependency.FullName + " is not a dynamic fixture"))))],
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        }

        stream.Position = 0;
        var assembly = AssemblyLoadContext.Default.LoadFromStream(stream);
        Images.Add(assembly, stream.ToArray());
        return assembly;
    }

    private static ImmutableArray<MetadataReference> References()
    {
        var platformReferences = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
        return [
            .. platformReferences,
            MetadataReference.CreateFromFile(typeof(ClosedAttribute).Assembly.Location),
        ];
    }
}
