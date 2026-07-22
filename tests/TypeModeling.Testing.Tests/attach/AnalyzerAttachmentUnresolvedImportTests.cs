using Microsoft.CodeAnalysis;
using TUnit.Core;
using TypeModeling.Testing.Attach;
using static TypeModeling.Testing.Tests.Attach.AnalyzerAttachmentConformanceTestSupport;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>未解決 Import の analyzer policy 検証</summary>
[NotInParallel]
public sealed class AnalyzerAttachmentUnresolvedImportTests
{
    /// <summary>一部だけ解決できる Import property の未解決 branch 検査</summary>
    [Test]
    public async Task Partially_resolved_import_cannot_hide_unresolved_analyzer_mutations()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <DisableTypeModelingAnalyzer Condition="'$(DisableTypeModelingAnalyzer)' == ''">false</DisableTypeModelingAnalyzer>
                <PolicyFile Condition="'$(DisableTypeModelingAnalyzer)' != 'true'">Safe.props</PolicyFile>
                <PolicyFile Condition="'$(DisableTypeModelingAnalyzer)' == 'true'">$(HiddenPolicy)</PolicyFile>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
              <Import Project="$(PolicyFile)" />
            </Project>
            """);
        fixture.WriteAdditionalFile("consumer/Safe.props", "<Project />");
        fixture.WriteAdditionalFile(
            "consumer/Policy.xml",
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
            "一部だけ解決できる Import property の未解決 branch 内 Remove を検出できなかった");
        AssertPolicyViolationContaining(
            violations,
            "NoWarn",
            "一部だけ解決できる Import property の未解決 branch 内 NoWarn を検出できなかった");
    }

    /// <summary>未解決 external Import に対する無関係 policy 非走査</summary>
    [Test]
    public async Task Unresolved_external_import_does_not_scan_unrelated_repo_policies()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
              <Import Project="$(ExternalSdkPath)/External.targets"
                      Condition="Exists('$(ExternalSdkPath)/External.targets')" />
            </Project>
            """);
        fixture.WriteAdditionalFile(
            "consumer/UnrelatedPolicy.props",
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
        if (violations.Any(violation =>
                violation.Contains("ProjectReference Remove", StringComparison.Ordinal) ||
                violation.Contains("NoWarn", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "未解決 external/SDK Import が無関係な repo policy を走査した:\n" +
                string.Join("\n", violations));
        }
    }

}
