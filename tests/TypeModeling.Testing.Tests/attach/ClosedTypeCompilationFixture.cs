using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>closed type detector 用の semantic compilation fixture</summary>
internal static class ClosedTypeCompilationFixture
{
    /// <summary>正しい ClosedUnion metadata identity と polymorphic hierarchy の宣言</summary>
    private const string ClosedTypeFixtureSource = """
        namespace TypeModeling.Domain
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface)]
            public sealed class ClosedUnionAttribute : System.Attribute;
        }

        namespace Probe.Unrelated
        {
            public sealed class ClosedUnionAttribute : System.Attribute;
        }

        namespace Probe.Types
        {
            using System.Text.Json.Serialization;
            using TypeModeling.Domain;

            [ClosedUnion]
            public abstract record ClosedBase;

            public sealed record ClosedVariant : ClosedBase;

            [JsonPolymorphic]
            [JsonDerivedType(typeof(JsonVariant))]
            public abstract record JsonBase;

            public sealed record JsonVariant : JsonBase;

            [JsonPolymorphic]
            public abstract record OpenJsonBase;

            public sealed record OpenJsonVariant : OpenJsonBase;

            [Probe.Unrelated.ClosedUnion]
            public abstract record SameNamedClosedBase;
        }
        """;

    /// <summary>実行環境由来の framework metadata reference 集合</summary>
    private static readonly ImmutableArray<MetadataReference> PlatformReferences =
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        .ToImmutableArray();

    /// <summary>consumer compilation から分離した closed type metadata reference</summary>
    private static readonly MetadataReference ClosedTypeReference = CreateClosedTypeReference();

    /// <summary>consumer source と closed type 宣言からの compilation 構築</summary>
    /// <param name="consumer">検査対象の consumer source</param>
    /// <returns>closed type detector へ渡す C# compilation</returns>
    internal static Compilation Create(string consumer)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Latest);
        return CSharpCompilation.Create(
            "ClosedTypeDetectorFixture",
            [CSharpSyntaxTree.ParseText(consumer, parseOptions, "consumer.cs")],
            [.. PlatformReferences, ClosedTypeReference],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>closed type fixture assembly の in-memory metadata reference 構築</summary>
    /// <returns>consumer compilation 用の metadata reference</returns>
    private static PortableExecutableReference CreateClosedTypeReference()
    {
        var compilation = CSharpCompilation.Create(
            "ClosedTypeDefinitions",
            [CSharpSyntaxTree.ParseText(
                ClosedTypeFixtureSource,
                new CSharpParseOptions(LanguageVersion.Latest),
                "closed-types.cs")],
            PlatformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        if (!result.Success)
        {
            throw new InvalidOperationException(string.Join(
                Environment.NewLine,
                result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        }

        return MetadataReference.CreateFromImage(stream.ToArray());
    }
}
