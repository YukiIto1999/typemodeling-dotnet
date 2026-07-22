using Microsoft.CodeAnalysis;
using TUnit.Core;
using TypeModeling.Testing.Attach;
using static TypeModeling.Testing.Tests.Attach.AnalyzerAttachmentConformanceTestSupport;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>analyzer 実行停止の検証</summary>
[NotInParallel]
public sealed class AnalyzerAttachmentExecutionDisableTests
{
    /// <summary>構成固有の analyzer 実行停止の拒否</summary>
    /// <param name="propertyName">analyzer 実行を制御する MSBuild property 名</param>
    [Test]
    [Arguments("RunAnalyzers")]
    [Arguments("RunAnalyzersDuringBuild")]
    public async Task Configuration_specific_analyzer_execution_disable_is_rejected(string propertyName)
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject($$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <Configurations>Debug;Release</Configurations>
                <{{propertyName}} Condition="'$(Configuration)' == 'Release'">false</{{propertyName}}>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
            </Project>
            """);

        AssertPolicyViolationContainingAll(
            await ViolationsAsync(fixture, CancellationToken.None),
            ["Configuration=Release", propertyName, "false"],
            "Release 構成の analyzer 実行停止を検出できなかった");
    }

    /// <summary>未評価条件に隠れた analyzer 実行停止の拒否</summary>
    /// <param name="propertyName">analyzer 実行を制御する MSBuild property 名</param>
    [Test]
    [Arguments("RunAnalyzers")]
    [Arguments("RunAnalyzersDuringBuild")]
    public async Task Repo_authored_analyzer_execution_disable_is_rejected(string propertyName)
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject($$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <DisableTypeModelingAnalyzer Condition="'$(DisableTypeModelingAnalyzer)' == ''">false</DisableTypeModelingAnalyzer>
                <{{propertyName}} Condition="'$(DisableTypeModelingAnalyzer)' == 'true'">false</{{propertyName}}>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
            </Project>
            """);

        AssertPolicyViolationContainingAll(
            await ViolationsAsync(fixture, CancellationToken.None),
            ["MSBuild 構造", propertyName, "false"],
            "未評価条件に隠れた analyzer 実行停止を構造検出できなかった");
    }

    /// <summary>条件付き RunAnalyzers 消去による build analyzer 停止の拒否</summary>
    [Test]
    public async Task Conditional_run_analyzers_clear_invalidates_the_true_override()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <DisableTypeModelingAnalyzer Condition="'$(DisableTypeModelingAnalyzer)' == ''">false</DisableTypeModelingAnalyzer>
                <RunAnalyzers>true</RunAnalyzers>
                <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
                <RunAnalyzers Condition="'$(DisableTypeModelingAnalyzer)' == 'true'"></RunAnalyzers>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
            </Project>
            """);

        AssertPolicyViolationContainingAll(
            await ViolationsAsync(fixture, CancellationToken.None),
            ["MSBuild 構造", "RunAnalyzersDuringBuild", "false"],
            "条件付き RunAnalyzers 消去で build analyzer が停止する分岐を検出できなかった");
    }

}
