using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace TypeModeling.Analysis.Tests;

/// <summary>analyzer を止める設定が build を失敗させること</summary>
/// <remarks>旧 AnalyzerAttachmentExecutionDisableTests、AnalyzerAttachmentExecutionOverrideTests の性質に当たる</remarks>
[NotInParallel]
public sealed class AnalyzerStopTests
{
    /// <summary>RunAnalyzers か RunAnalyzersDuringBuild を false にした project の build が TYPMOD202 で失敗すること</summary>
    /// <param name="property">analyzer の実行を制御する MSBuild property</param>
    [Test]
    [Arguments("RunAnalyzers")]
    [Arguments("RunAnalyzersDuringBuild")]
    public async Task Stopping_the_analyzers_fails_the_build(string property)
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject($"<PropertyGroup><{property}>false</{property}></PropertyGroup>");

        var result = await fixture.BuildAsync();

        await Assert.That(result.Reports("TYPMOD202")).IsTrue().Because(result.Describe());
    }

    /// <summary>Release 構成だけで analyzer を止める project は、Release の build だけが失敗すること</summary>
    /// <param name="property">analyzer の実行を制御する MSBuild property</param>
    [Test]
    [Arguments("RunAnalyzers")]
    [Arguments("RunAnalyzersDuringBuild")]
    public async Task A_configuration_specific_stop_fails_that_configuration(string property)
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            $"<PropertyGroup Condition=\"'$(Configuration)' == 'Release'\"><{property}>false</{property}></PropertyGroup>"
        );

        var debug = await fixture.BuildAsync("-c", "Debug");
        var release = await fixture.BuildAsync("-c", "Release");

        await Assert.That(debug.Succeeded).IsTrue().Because(debug.Describe());
        await Assert.That(release.Reports("TYPMOD202")).IsTrue().Because(release.Describe());
    }

    /// <summary>property が真のときだけ analyzer を止める記述は、その property を与えた build が失敗すること</summary>
    /// <param name="property">analyzer の実行を制御する MSBuild property</param>
    [Test]
    [Arguments("RunAnalyzers")]
    [Arguments("RunAnalyzersDuringBuild")]
    public async Task A_stop_behind_a_custom_property_fails_when_the_property_is_set(
        string property
    )
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            $"""
            <PropertyGroup>
              <DisableTypeModelingAnalyzer Condition="'$(DisableTypeModelingAnalyzer)' == ''">false</DisableTypeModelingAnalyzer>
              <{property} Condition="'$(DisableTypeModelingAnalyzer)' == 'true'">false</{property}>
            </PropertyGroup>
            """
        );

        var normal = await fixture.BuildAsync();
        var disabled = await fixture.BuildAsync("-p:DisableTypeModelingAnalyzer=true");

        await Assert.That(normal.Succeeded).IsTrue().Because(normal.Describe());
        await Assert.That(disabled.Reports("TYPMOD202")).IsTrue().Because(disabled.Describe());
    }

    /// <summary>RunAnalyzers を空にして RunAnalyzersDuringBuild の false を効かせる記述が失敗すること</summary>
    [Test]
    public async Task Clearing_RunAnalyzers_lets_the_build_time_stop_take_effect()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            """
            <PropertyGroup>
              <DisableTypeModelingAnalyzer Condition="'$(DisableTypeModelingAnalyzer)' == ''">false</DisableTypeModelingAnalyzer>
              <RunAnalyzers>true</RunAnalyzers>
              <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
              <RunAnalyzers Condition="'$(DisableTypeModelingAnalyzer)' == 'true'"></RunAnalyzers>
            </PropertyGroup>
            """
        );

        var normal = await fixture.BuildAsync();
        var disabled = await fixture.BuildAsync("-p:DisableTypeModelingAnalyzer=true");

        await Assert.That(normal.Succeeded).IsTrue().Because(normal.Describe());
        await Assert.That(disabled.Reports("TYPMOD202")).IsTrue().Because(disabled.Describe());
    }

    /// <summary>RunAnalyzers が true なら RunAnalyzersDuringBuild の false は analyzer を止めないので許すこと</summary>
    /// <param name="runAnalyzers">RunAnalyzers に書く値。property 経由の真値を含む</param>
    [Test]
    [Arguments("true")]
    [Arguments("$(AlwaysRunAnalyzers)")]
    public async Task RunAnalyzers_true_overrides_the_build_time_stop(string runAnalyzers)
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            $"""
            <PropertyGroup>
              <AlwaysRunAnalyzers>true</AlwaysRunAnalyzers>
              <RunAnalyzers>{runAnalyzers}</RunAnalyzers>
              <RunAnalyzersDuringBuild>false</RunAnalyzersDuringBuild>
            </PropertyGroup>
            """
        );
        fixture.WriteSource(ConsumerFixture.ViolatingSource);

        var result = await fixture.BuildAsync();

        await Assert
            .That(result.Reports("TYPMOD003"))
            .IsTrue()
            .Because("analyzer が動いて違反を報告する:" + result.Describe());
        await Assert.That(result.Reports("TYPMOD202")).IsFalse().Because(result.Describe());
    }
}
