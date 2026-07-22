using Microsoft.CodeAnalysis;
using TUnit.Core;
using TypeModeling.Testing.Attach;
using static TypeModeling.Testing.Tests.Attach.AnalyzerAttachmentConformanceTestSupport;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>analyzer attachment 要件の検証</summary>
[NotInParallel]
public sealed class AnalyzerAttachmentRequirementTests
{
    /// <summary>closed type 利用 project における analyzer attachment の成立</summary>
    [Test]
    public async Task Every_closed_type_using_project_attaches_the_type_modeling_analyzer()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();

        var violations = await ViolationsAsync(fixture, CancellationToken.None);

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "closed 型を semantic に使う project は全 build 構成で typemodeling analyzer の診断を可視にすること。違反:\n  " +
                string.Join("\n  ", violations));
        }
    }

    /// <summary>analyzer-only project reference による fresh build 順の保証</summary>
    [Test]
    public async Task Analyzer_project_is_built_before_an_earlier_sorted_consumer()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        var consumerDirectory = Path.Combine(fixture.RepoRoot, "a-consumer");
        var analyzerDirectory = Path.Combine(fixture.RepoRoot, "z-analyzers");
        Directory.Move(Path.Combine(fixture.RepoRoot, "consumer"), consumerDirectory);
        Directory.Move(Path.Combine(fixture.RepoRoot, "analyzers"), analyzerDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(consumerDirectory, "Consumer.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../z-analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
            </Project>
            """,
            CancellationToken.None);
        var requirement = fixture.Requirement with
        {
            AnalyzerProjectPath = "z-analyzers/Fixture.Analyzers.csproj",
            ExpectedProjectPaths = new HashSet<string>(StringComparer.Ordinal)
            {
                "a-consumer/Consumer.csproj",
            },
        };

        var violations = await AnalyzerAttachmentConformance.ViolationsAsync(
            fixture.RepoRoot,
            requirement,
            new ClosedTypeUsageDetector(),
            CancellationToken.None);

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "analyzer-only project reference の依存順を保てなかった:\n" +
                string.Join("\n", violations));
        }
    }

    /// <summary>repository 外を指す analyzer project path の拒否</summary>
    [Test]
    public async Task Analyzer_project_path_outside_the_repository_is_rejected()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        var requirement = fixture.Requirement with
        {
            AnalyzerProjectPath = "../outside/Fixture.Analyzers.csproj",
        };

        await Assert.That(async () => await AnalyzerAttachmentConformance.ViolationsAsync(
                fixture.RepoRoot,
                requirement,
                new ClosedTypeUsageDetector(),
                CancellationToken.None))
            .ThrowsExactly<ArgumentException>()
            .WithMessageContaining("repository 内の相対 path");
    }

    /// <summary>絶対 analyzer project path の拒否</summary>
    [Test]
    public async Task Absolute_analyzer_project_path_is_rejected()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        var requirement = fixture.Requirement with
        {
            AnalyzerProjectPath = Path.Combine(
                fixture.RepoRoot,
                "analyzers",
                "Fixture.Analyzers.csproj"),
        };

        await Assert.That(async () => await AnalyzerAttachmentConformance.ViolationsAsync(
                fixture.RepoRoot,
                requirement,
                new ClosedTypeUsageDetector(),
                CancellationToken.None))
            .ThrowsExactly<ArgumentException>()
            .WithMessageContaining("repository 内の相対 path");
    }

}
