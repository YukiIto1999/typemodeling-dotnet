using Microsoft.CodeAnalysis;
using TUnit.Core;
using TypeModeling.Testing.Attach;
using static TypeModeling.Testing.Tests.Attach.AnalyzerAttachmentConformanceTestSupport;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>build 構成展開後の guarded feature 判定</summary>
[NotInParallel]
public sealed class AnalyzerAttachmentBuildDimensionTests
{
    /// <summary>target framework 固有 RuntimeIdentifier 構成の検査</summary>
    [Test]
    public async Task Runtime_identifiers_are_discovered_per_target_framework()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <LangVersion>latest</LangVersion>
                <RuntimeIdentifiers Condition="'$(TargetFramework)' == 'net10.0'">linux-x64</RuntimeIdentifiers>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
                <ProjectReference Remove="../analyzers/Fixture.Analyzers.csproj"
                                  Condition="'$(RuntimeIdentifier)' == 'linux-x64'" />
              </ItemGroup>
              <Target Name="RequireRuntimeDimension"
                      BeforeTargets="CoreCompile"
                      Condition="'$(TargetFramework)' == 'net10.0' and
                                 '$(RuntimeIdentifier)' != 'linux-x64'">
                <Error Text="net10.0 build requires RuntimeIdentifier=linux-x64" />
              </Target>
            </Project>
            """);

        AssertPolicyViolationContainingAll(
            await ViolationsAsync(fixture, CancellationToken.None),
            ["TargetFramework=net10.0", "RuntimeIdentifier=linux-x64", "ProjectReference が有効でない"],
            "net10.0/linux-x64 だけで companion analyzer を外す反例を評価できなかった");
    }

    /// <summary>configuration 固有 Platform 構成の検査</summary>
    [Test]
    public async Task Configuration_specific_platforms_are_evaluated()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <Configurations>Debug;Release</Configurations>
                <Platforms Condition="'$(Configuration)' == 'Release'">AnyCPU;x64</Platforms>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false"
                                  Condition="'$(Platform)' != 'x64'" />
              </ItemGroup>
            </Project>
            """);

        AssertPolicyViolationContainingAll(
            await ViolationsAsync(fixture, CancellationToken.None),
            ["Configuration=Release", "Platform=x64", "ProjectReference が有効でない"],
            "Release 固有 Platform で companion analyzer が外れる反例を検出できなかった");
    }

    /// <summary>Release 構成だけに含まれる closed type 利用の検出</summary>
    [Test]
    public async Task Closed_type_use_included_only_in_release_counts_as_closed_type_use()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerProject("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <Configurations>Debug;Release</Configurations>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="Debug.cs" />
                <ProjectReference Include="../analyzers/Fixture.Analyzers.csproj"
                                  OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
              </ItemGroup>
              <ItemGroup Condition="'$(Configuration)' == 'Release'">
                <Compile Include="ReleaseClosedTypes.cs" />
              </ItemGroup>
            </Project>
            """);
        fixture.WriteAdditionalFile(
            "consumer/Debug.cs",
            "namespace Fixture.Consumer; internal sealed class DebugOnly;");
        fixture.WriteAdditionalFile(
            "consumer/ReleaseClosedTypes.cs",
            TemporaryAnalyzerAttachmentFixture.ClosedTypeDeclarations);

        var violations = await ViolationsAsync(fixture, CancellationToken.None);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "Release だけで closed 型を使う project を検出できなかった:\n" +
                string.Join("\n", violations));
        }
    }

}
