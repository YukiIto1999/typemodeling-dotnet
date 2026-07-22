using Microsoft.CodeAnalysis;
using TUnit.Core;
using TypeModeling.Testing.Attach;
using static TypeModeling.Testing.Tests.Attach.AnalyzerAttachmentConformanceTestSupport;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>RunAnalyzers override の検証</summary>
[NotInParallel]
public sealed class AnalyzerAttachmentExecutionOverrideTests
{
    /// <summary>RunAnalyzers による build 固有停止設定の上書き許可</summary>
    [Test]
    public async Task Run_analyzers_true_overrides_run_analyzers_during_build_false()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <RunAnalyzers>true</RunAnalyzers>
                <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
            </Project>
            """);

        var violations = await ViolationsAsync(fixture, CancellationToken.None);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "RunAnalyzers=true により analyzer が有効な設定を誤検出した:\n" +
                string.Join("\n", violations));
        }
    }

    /// <summary>property 経由 RunAnalyzers 恒真値による build 固有停止設定の上書き許可</summary>
    [Test]
    public async Task Indirect_run_analyzers_true_overrides_run_analyzers_during_build_false()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <AlwaysRunAnalyzers>true</AlwaysRunAnalyzers>
                <RunAnalyzers>$(AlwaysRunAnalyzers)</RunAnalyzers>
                <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
            </Project>
            """);

        var violations = await ViolationsAsync(fixture, CancellationToken.None);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "property 経由の RunAnalyzers 恒真値を誤検出した:\n" +
                string.Join("\n", violations));
        }
    }

}
