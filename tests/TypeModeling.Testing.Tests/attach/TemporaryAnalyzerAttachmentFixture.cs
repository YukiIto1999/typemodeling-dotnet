using TypeModeling.Testing.Attach;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>analyzer attachment 規約用の一時 repository fixture</summary>
internal sealed class TemporaryAnalyzerAttachmentFixture : IDisposable
{
    /// <summary>正しい metadata identity を持つ closed type 宣言</summary>
    internal const string ClosedTypeDeclarations = """
        #if NETSTANDARD2_0
        namespace System.Runtime.CompilerServices
        {
            internal sealed class IsExternalInit;
        }
        #endif

        namespace TypeModeling.Domain
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Interface)]
            public sealed class ClosedUnionAttribute : System.Attribute;
        }

        namespace Fixture.Consumer
        {
            using TypeModeling.Domain;

            [ClosedUnion]
            public abstract record ClosedBase
            {
                private ClosedBase() { }

                public sealed record ClosedVariant : ClosedBase;
            }

            internal static class ClosedTypeUse
            {
                internal static ClosedBase Preserve(ClosedBase.ClosedVariant value) => value;
            }
        }
        """;

    /// <summary>既定の analyzer-only ProjectReference を持つ consumer project</summary>
    private const string DefaultConsumerProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <AssemblyName>Fixture.Consumer</AssemblyName>
          </PropertyGroup>
          <ItemGroup>
            <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                              OutputItemType="Analyzer"
                              ReferenceOutputAssembly="false" />
          </ItemGroup>
        </Project>
        """;

    /// <summary>一時 repository の構築</summary>
    internal TemporaryAnalyzerAttachmentFixture()
    {
        RepoRoot = Path.Combine(
            Path.GetTempPath(),
            "typemodeling-testing-attach-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(RepoRoot, "analyzers"));
        Directory.CreateDirectory(Path.Combine(RepoRoot, "consumer"));
        File.WriteAllText(
            Path.Combine(RepoRoot, "analyzers", "Fixture.Analyzers.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>netstandard2.0</TargetFramework>
                <AssemblyName>Fixture.Analyzers</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(RepoRoot, "analyzers", "AnalyzerProbe.cs"),
            "namespace Fixture.Analyzers { internal sealed class AnalyzerProbe { } }");
        WriteConsumerProject(DefaultConsumerProject);
        WriteConsumerSource(ClosedTypeDeclarations);
        Requirement = new AnalyzerAttachmentRequirement(
            "analyzers/Fixture.Analyzers.csproj",
            new HashSet<string>(StringComparer.Ordinal) { "TCSSUP001", "TCSSUP002" },
            new HashSet<string>(StringComparer.Ordinal) { "TYPMOD" },
            new HashSet<string>(StringComparer.Ordinal) { "consumer/Consumer.csproj" },
            new HashSet<string>(StringComparer.Ordinal) { "TypeModeling" });
    }

    /// <summary>一時 repository の絶対パス</summary>
    internal string RepoRoot { get; }

    /// <summary>attachment 規約の固定要件</summary>
    internal AnalyzerAttachmentRequirement Requirement { get; }

    /// <summary>consumer project ファイルの上書き</summary>
    /// <param name="project">consumer project の XML</param>
    internal void WriteConsumerProject(string project) =>
        File.WriteAllText(Path.Combine(RepoRoot, "consumer", "Consumer.csproj"), project);

    /// <summary>consumer source ファイルの上書き</summary>
    /// <param name="source">consumer の C# source</param>
    internal void WriteConsumerSource(string source) =>
        File.WriteAllText(Path.Combine(RepoRoot, "consumer", "ClosedTypes.cs"), source);

    /// <summary>consumer に適用する editorconfig の上書き</summary>
    /// <param name="editorConfig">editorconfig の内容</param>
    internal void WriteEditorConfig(string editorConfig) =>
        File.WriteAllText(Path.Combine(RepoRoot, "consumer", ".editorconfig"), editorConfig);

    /// <summary>repository 内の追加 props targets XML source の生成</summary>
    /// <param name="relativePath">repository root からの相対パス</param>
    /// <param name="content">追加ファイルの内容</param>
    internal void WriteAdditionalFile(string relativePath, string content)
    {
        var path = Path.Combine(RepoRoot, relativePath);
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("追加ファイルの親 directory を解決できない");
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, content);
    }

    /// <summary>consumer へ source を生成する analyzer project への変更</summary>
    internal void UseSourceGeneratorAnalyzer()
    {
        File.WriteAllText(
            Path.Combine(RepoRoot, "analyzers", "Fixture.Analyzers.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Analyzers</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="Microsoft.CodeAnalysis"
                           HintPath="$(MSBuildSDKsPath)/../Roslyn/bincore/Microsoft.CodeAnalysis.dll"
                           Private="false" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(RepoRoot, "analyzers", "AnalyzerProbe.cs"),
            """
            using Microsoft.CodeAnalysis;

            namespace Fixture.Analyzers;

            [Generator]
            public sealed class AnalyzerProbe : IIncrementalGenerator
            {
                public void Initialize(IncrementalGeneratorInitializationContext context) =>
                    context.RegisterPostInitializationOutput(static output =>
                        output.AddSource(
                            "GeneratedGuardedUse.g.cs",
                            "namespace Fixture.Consumer; internal sealed class GeneratedGuardedUse;"));
            }
            """);
    }

    /// <summary>一時 repository の削除</summary>
    public void Dispose() =>
        Directory.Delete(RepoRoot, recursive: true);
}
