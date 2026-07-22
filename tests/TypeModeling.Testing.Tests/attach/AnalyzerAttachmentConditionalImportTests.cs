using Microsoft.CodeAnalysis;
using TUnit.Core;
using TypeModeling.Testing.Attach;
using static TypeModeling.Testing.Tests.Attach.AnalyzerAttachmentConformanceTestSupport;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>条件付き Import の analyzer policy 検証</summary>
[NotInParallel]
public sealed class AnalyzerAttachmentConditionalImportTests
{
    /// <summary>条件付き repository authored Import による analyzer attachment の拒否</summary>
    [Test]
    public async Task Conditional_repo_authored_import_cannot_provide_analyzer_attach()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <DisableTypeModelingAnalyzer Condition="'$(DisableTypeModelingAnalyzer)' == ''">false</DisableTypeModelingAnalyzer>
              </PropertyGroup>
              <Import Project="Policy.props"
                      Condition="'$(DisableTypeModelingAnalyzer)' == 'true'" />
            </Project>
            """);
        fixture.WriteAdditionalFile(
            "consumer/Policy.props",
            """
            <Project>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
            </Project>
            """);

        AssertPolicyViolationContaining(
            await ViolationsAsync(fixture, CancellationToken.None),
            "条件付き Import",
            "条件付き Import 配下の companion analyzer Include を無条件と誤認した");
    }

    /// <summary>property と glob を使う条件付き Import 内 analyzer mutation の拒否</summary>
    /// <param name="importExpression">検査対象の Import Project expression</param>
    [Test]
    [Arguments("$(PolicyFile)")]
    [Arguments("Policy*.props")]
    public async Task Conditional_import_expressions_cannot_hide_analyzer_mutations(string importExpression)
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        var propertyDeclaration = importExpression == "$(PolicyFile)"
            ? "<PolicyFile>Policy.props</PolicyFile>"
            : "";
        fixture.WriteConsumerProject($$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <DisableTypeModelingAnalyzer Condition="'$(DisableTypeModelingAnalyzer)' == ''">false</DisableTypeModelingAnalyzer>
                {{propertyDeclaration}}
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
              <Import Project="{{importExpression}}"
                      Condition="'$(DisableTypeModelingAnalyzer)' == 'true'" />
            </Project>
            """);
        fixture.WriteAdditionalFile(
            "consumer/Policy.props",
            """
            <Project>
              <ItemGroup>
                <ProjectReference Remove="../analyzers/Fixture.Analyzers.csproj" />
              </ItemGroup>
              <PropertyGroup>
                <NoWarn>$(NoWarn);TYPMOD*</NoWarn>
              </PropertyGroup>
            </Project>
            """);

        var violations = await ViolationsAsync(fixture, CancellationToken.None);
        AssertPolicyViolationContaining(
            violations,
            "ProjectReference Remove",
            importExpression + " Import 内の companion analyzer Remove を構造検出できなかった");
        AssertPolicyViolationContaining(
            violations,
            "NoWarn",
            importExpression + " Import 内の TYPMOD* NoWarn を構造検出できなかった");
    }

}
