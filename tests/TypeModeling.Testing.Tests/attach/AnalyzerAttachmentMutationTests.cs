using Microsoft.CodeAnalysis;
using TUnit.Core;
using TypeModeling.Testing.Attach;
using static TypeModeling.Testing.Tests.Attach.AnalyzerAttachmentConformanceTestSupport;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>条件付き analyzer mutation の検証</summary>
[NotInParallel]
public sealed class AnalyzerAttachmentMutationTests
{
    /// <summary>configuration 条件による analyzer attachment 欠落の拒否</summary>
    [Test]
    public async Task Conditional_analyzer_removal_is_rejected()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <Configurations>Debug;Release</Configurations>
              </PropertyGroup>
              <ItemGroup Condition="'$(Configuration)' != 'Release'">
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
            </Project>
            """);

        AssertPolicyViolationContainingAll(
            await ViolationsAsync(fixture, CancellationToken.None),
            ["Configuration=Release", "ProjectReference が有効でない"],
            "Release configuration で companion analyzer を外す反例を検出できなかった");
    }

    /// <summary>repository authored policy 内の条件付き analyzer mutation 拒否</summary>
    [Test]
    public async Task Repo_authored_conditional_analyzer_mutations_are_rejected()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <DisableTypeModelingAnalyzer Condition="'$(DisableTypeModelingAnalyzer)' == ''">false</DisableTypeModelingAnalyzer>
              </PropertyGroup>
              <Import Project="Policy.props" />
            </Project>
            """);
        fixture.WriteAdditionalFile(
            "consumer/Policy.props",
            """
            <Project>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
                <ProjectReference Remove="../analyzers/Fixture.Analyzers.csproj"
                                  Condition="'$(DisableTypeModelingAnalyzer)' == 'true'" />
              </ItemGroup>
              <PropertyGroup Condition="'$(DisableTypeModelingAnalyzer)' == 'true'">
                <NoWarn>$(NoWarn);TYPMOD*</NoWarn>
              </PropertyGroup>
            </Project>
            """);

        var violations = await ViolationsAsync(fixture, CancellationToken.None);
        AssertPolicyViolationContaining(
            violations,
            "ProjectReference Remove",
            "custom property で有効になる companion analyzer Remove を構造検出できなかった");
        AssertPolicyViolationContaining(
            violations,
            "NoWarn",
            "custom property で有効になる TYPMOD* NoWarn を構造検出できなかった");
    }

    /// <summary>item expression に隠れた analyzer mutation の拒否</summary>
    [Test]
    public async Task Item_expression_cannot_hide_analyzer_mutation()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <DisableTypeModelingAnalyzer Condition="'$(DisableTypeModelingAnalyzer)' == ''">false</DisableTypeModelingAnalyzer>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
                <HiddenAnalyzerRefs Include="../analyzers/Fixture.Analyzers.csproj" />
                <ProjectReference Remove="@(HiddenAnalyzerRefs)"
                                  Condition="'$(DisableTypeModelingAnalyzer)' == 'true'" />
              </ItemGroup>
            </Project>
            """);

        AssertPolicyViolationContaining(
            await ViolationsAsync(fixture, CancellationToken.None),
            "ProjectReference Remove",
            "item expression に隠れた companion analyzer Remove を構造検出できなかった");
    }

}
