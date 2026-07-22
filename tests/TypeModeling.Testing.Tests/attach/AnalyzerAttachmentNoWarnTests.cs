using Microsoft.CodeAnalysis;
using TUnit.Core;
using TypeModeling.Testing.Attach;
using static TypeModeling.Testing.Tests.Attach.AnalyzerAttachmentConformanceTestSupport;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>NoWarn による診断抑止の検証</summary>
[NotInParallel]
public sealed class AnalyzerAttachmentNoWarnTests
{
    /// <summary>property 間接参照による診断抑止の拒否</summary>
    [Test]
    public async Task Indirect_no_warn_property_is_rejected()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <DisableTypeModelingAnalyzer Condition="'$(DisableTypeModelingAnalyzer)' == ''">false</DisableTypeModelingAnalyzer>
                <HiddenNoWarn Condition="'$(DisableTypeModelingAnalyzer)' == 'true'">TYPMOD*</HiddenNoWarn>
                <NoWarn>$(NoWarn);$(HiddenNoWarn)</NoWarn>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
            </Project>
            """);

        AssertPolicyViolationContaining(
            await ViolationsAsync(fixture, CancellationToken.None),
            "NoWarn",
            "property 間接参照で有効になる TYPMOD* NoWarn を構造検出できなかった");
    }

    /// <summary>NoWarn による保護対象診断の抑止拒否</summary>
    /// <param name="diagnosticId">NoWarn に指定する診断 ID または prefix wildcard</param>
    [Test]
    [Arguments("TYPMOD001")]
    [Arguments("TYPMOD*")]
    [Arguments("TCSSUP001")]
    [Arguments("TCSSUP*")]
    public async Task Analyzer_diagnostic_in_no_warn_is_rejected(string diagnosticId)
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject($$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <Configurations>Debug;Release</Configurations>
                <NoWarn Condition="'$(Configuration)' == 'Release'">$(NoWarn);{{diagnosticId}}</NoWarn>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
            </Project>
            """);

        AssertPolicyViolationContainingAll(
            await ViolationsAsync(fixture, CancellationToken.None),
            ["Configuration=Release", "NoWarn", diagnosticId],
            "NoWarn で " + diagnosticId + " を抑止する反例を検出できなかった");
    }

}
