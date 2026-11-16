using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace TypeModeling.Analysis.Tests;

/// <summary>取り込んだ analyzer を外す記述が build を失敗させること</summary>
/// <remarks>旧 AnalyzerAttachmentMutationTests、AnalyzerAttachmentConditionalImportTests、AnalyzerAttachmentUnresolvedImportTests、AnalyzerAttachmentBuildDimensionTests の性質に当たる</remarks>
[NotInParallel]
public sealed class AnalyzerRemovalTests
{
    /// <summary>外す記述の本体。DisableTypeModelingAnalyzer が真のときだけ ProjectReference を外す</summary>
    private const string ConditionalRemoval = """
        <PropertyGroup>
          <DisableTypeModelingAnalyzer Condition="'$(DisableTypeModelingAnalyzer)' == ''">false</DisableTypeModelingAnalyzer>
        </PropertyGroup>
        <ItemGroup>
          <ProjectReference Remove="{Analyzer}" Condition="'$(DisableTypeModelingAnalyzer)' == 'true'" />
        </ItemGroup>
        """;

    /// <summary>property が真のときだけ analyzer を外す記述は、その property を与えた build が TYPMOD203 で失敗すること</summary>
    [Test]
    public async Task A_conditional_removal_fails_when_the_condition_holds()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(ConditionalRemoval);

        var normal = await fixture.BuildAsync();
        var disabled = await fixture.BuildAsync("-p:DisableTypeModelingAnalyzer=true");

        await Assert.That(normal.Succeeded).IsTrue().Because(normal.Describe());
        await Assert.That(disabled.Reports("TYPMOD203")).IsTrue().Because(disabled.Describe());
    }

    /// <summary>item の式に隠した Remove も、取り込んだ analyzer を外せば TYPMOD203 で失敗すること</summary>
    [Test]
    public async Task A_removal_hidden_in_an_item_expression_fails()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            """
            <ItemGroup>
              <HiddenAnalyzerRefs Include="{Analyzer}" />
              <ProjectReference Remove="@(HiddenAnalyzerRefs)" Condition="'$(DisableTypeModelingAnalyzer)' == 'true'" />
            </ItemGroup>
            """
        );

        var disabled = await fixture.BuildAsync("-p:DisableTypeModelingAnalyzer=true");

        await Assert.That(disabled.Reports("TYPMOD203")).IsTrue().Because(disabled.Describe());
    }

    /// <summary>Analyzer item を target で外す記述が TYPMOD203 で失敗すること</summary>
    [Test]
    public async Task Removing_the_analyzer_item_in_a_target_fails()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            """
            <Target Name="DropAnalyzers" AfterTargets="ResolveProjectReferences">
              <ItemGroup><Analyzer Remove="@(Analyzer)" /></ItemGroup>
            </Target>
            """
        );

        var result = await fixture.BuildAsync();

        await Assert.That(result.Reports("TYPMOD203")).IsTrue().Because(result.Describe());
    }

    /// <summary>取り込みを外す記述を、条件付きの Import や property と glob で隠しても、成立した build は失敗すること</summary>
    /// <param name="import">Import の Project 属性</param>
    [Test]
    [Arguments("Policy.props")]
    [Arguments("$(PolicyFile)")]
    [Arguments("Policy*.props")]
    public async Task A_removal_in_a_conditional_import_fails_when_the_import_holds(string import)
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            $"""
            <PropertyGroup>
              <DisableTypeModelingAnalyzer Condition="'$(DisableTypeModelingAnalyzer)' == ''">false</DisableTypeModelingAnalyzer>
              <PolicyFile>Policy.props</PolicyFile>
            </PropertyGroup>
            <Import Project="{import}" Condition="'$(DisableTypeModelingAnalyzer)' == 'true'" />
            """
        );
        fixture.WriteFile(
            "consumer/Policy.props",
            """
            <Project>
              <ItemGroup><ProjectReference Remove="{Analyzer}" /></ItemGroup>
            </Project>
            """
        );

        var normal = await fixture.BuildAsync();
        var disabled = await fixture.BuildAsync("-p:DisableTypeModelingAnalyzer=true");

        await Assert.That(normal.Succeeded).IsTrue().Because(normal.Describe());
        await Assert.That(disabled.Reports("TYPMOD203")).IsTrue().Because(disabled.Describe());
    }

    /// <summary>外部の Import が存在しない間は、無関係な policy file を読まず build が成功すること</summary>
    [Test]
    public async Task An_unresolved_external_import_does_not_pull_in_unrelated_policies()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            """
            <Import Project="$(ExternalSdkPath)/External.targets" Condition="Exists('$(ExternalSdkPath)/External.targets')" />
            """
        );
        fixture.WriteFile(
            "consumer/UnrelatedPolicy.props",
            """<Project><ItemGroup><ProjectReference Remove="{Analyzer}" /></ItemGroup></Project>"""
        );

        var result = await fixture.BuildAsync();

        await Assert.That(result.Succeeded).IsTrue().Because(result.Describe());
    }

    /// <summary>Release 構成だけで analyzer を外す project は、Release の build だけが失敗すること</summary>
    [Test]
    public async Task A_removal_in_release_fails_the_release_build()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            """
            <ItemGroup Condition="'$(Configuration)' == 'Release'">
              <ProjectReference Remove="{Analyzer}" />
            </ItemGroup>
            """
        );

        var debug = await fixture.BuildAsync("-c", "Debug");
        var release = await fixture.BuildAsync("-c", "Release");

        await Assert.That(debug.Succeeded).IsTrue().Because(debug.Describe());
        await Assert.That(release.Reports("TYPMOD203")).IsTrue().Because(release.Describe());
    }

    /// <summary>Release 構成の Platform だけで analyzer を外す project が、その組の build で失敗すること</summary>
    [Test]
    public async Task A_removal_for_one_platform_fails_that_platform()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            """
            <PropertyGroup><Configurations>Debug;Release</Configurations><Platforms>AnyCPU;x64</Platforms></PropertyGroup>
            <ItemGroup Condition="'$(Configuration)' == 'Release' And '$(Platform)' == 'x64'">
              <ProjectReference Remove="{Analyzer}" />
            </ItemGroup>
            """
        );

        var anyCpu = await fixture.BuildAsync("-c", "Release", "-p:Platform=AnyCPU");
        var x64 = await fixture.BuildAsync("-c", "Release", "-p:Platform=x64");

        await Assert.That(anyCpu.Succeeded).IsTrue().Because(anyCpu.Describe());
        await Assert.That(x64.Reports("TYPMOD203")).IsTrue().Because(x64.Describe());
    }

    /// <summary>複数 target framework のうち、RuntimeIdentifier を与えた net10.0 だけで外す記述が失敗すること</summary>
    [Test]
    public async Task A_removal_for_one_framework_and_runtime_fails_that_build()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            """
            <ItemGroup Condition="'$(TargetFramework)' == 'net10.0' And '$(RuntimeIdentifier)' == 'linux-x64'">
              <ProjectReference Remove="{Analyzer}" />
            </ItemGroup>
            """,
            "<TargetFrameworks>netstandard2.0;net10.0</TargetFrameworks><LangVersion>latest</LangVersion>"
        );

        var plain = await fixture.BuildAsync("-f", "net10.0");
        var runtime = await fixture.BuildAsync(
            "-f",
            "net10.0",
            "-r",
            "linux-x64",
            "--no-self-contained"
        );

        await Assert.That(plain.Succeeded).IsTrue().Because(plain.Describe());
        await Assert.That(runtime.Reports("TYPMOD203")).IsTrue().Because(runtime.Describe());
    }
}
