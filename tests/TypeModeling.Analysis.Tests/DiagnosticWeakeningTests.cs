using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace TypeModeling.Analysis.Tests;

/// <summary>NoWarn と editorconfig で TypeModeling の診断を弱めても、build が TYPMOD022 で失敗すること</summary>
/// <remarks>旧 AnalyzerAttachmentNoWarnTests、AnalyzerAttachmentEditorConfigTests の性質に当たる</remarks>
[NotInParallel]
public sealed class DiagnosticWeakeningTests
{
    /// <summary>Release 構成だけの NoWarn が、その構成の build を TYPMOD022 で失敗させること</summary>
    /// <param name="id">NoWarn に足す診断の ID</param>
    [Test]
    [Arguments("TYPMOD002")]
    [Arguments("TYPMOD003")]
    [Arguments("TYPMOD101")]
    public async Task NoWarn_of_a_TypeModeling_diagnostic_fails_the_build(string id)
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            $"<PropertyGroup Condition=\"'$(Configuration)' == 'Release'\"><NoWarn>$(NoWarn);{id}</NoWarn></PropertyGroup>"
        );

        var debug = await fixture.BuildAsync("-c", "Debug");
        var release = await fixture.BuildAsync("-c", "Release");

        await Assert.That(debug.Succeeded).IsTrue().Because(debug.Describe());
        await Assert.That(release.Reports("TYPMOD022")).IsTrue().Because(release.Describe());
    }

    /// <summary>property を間接に参照して足した NoWarn も、TYPMOD022 で失敗すること</summary>
    [Test]
    public async Task An_indirect_NoWarn_fails_the_build()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            """
            <PropertyGroup>
              <DisableTypeModelingAnalyzer Condition="'$(DisableTypeModelingAnalyzer)' == ''">false</DisableTypeModelingAnalyzer>
              <HiddenNoWarn Condition="'$(DisableTypeModelingAnalyzer)' == 'true'">TYPMOD003</HiddenNoWarn>
              <NoWarn>$(NoWarn);$(HiddenNoWarn)</NoWarn>
            </PropertyGroup>
            """
        );

        var disabled = await fixture.BuildAsync("-p:DisableTypeModelingAnalyzer=true");

        await Assert.That(disabled.Reports("TYPMOD022")).IsTrue().Because(disabled.Describe());
    }

    /// <summary>NoWarn の wildcard は compiler が解釈しないので、違反の診断が error のまま build が失敗すること</summary>
    [Test]
    public async Task A_NoWarn_wildcard_does_not_hide_a_violation()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject("<PropertyGroup><NoWarn>$(NoWarn);TYPMOD*</NoWarn></PropertyGroup>");
        fixture.WriteSource(ConsumerFixture.ViolatingSource);

        var result = await fixture.BuildAsync();

        await Assert.That(result.Reports("TYPMOD003")).IsTrue().Because(result.Describe());
    }

    /// <summary>editorconfig が診断の重大度を既定より下げると、build が TYPMOD022 で失敗すること</summary>
    /// <param name="severity">editorconfig の重大度</param>
    [Test]
    [Arguments("none")]
    [Arguments("silent")]
    [Arguments("suggestion")]
    [Arguments("warning")]
    public async Task An_editorconfig_that_lowers_a_diagnostic_fails_the_build(string severity)
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteFile(
            "consumer/.editorconfig",
            $"root = true\n\n[Source.cs]\ndotnet_diagnostic.TYPMOD003.severity = {severity}\n\n[DoesNotMatch.cs]\ndotnet_diagnostic.TYPMOD004.severity = none\n"
        );
        fixture.WriteSource(ConsumerFixture.ViolatingSource);

        var result = await fixture.BuildAsync();

        await Assert.That(result.Reports("TYPMOD022")).IsTrue().Because(result.Describe());
    }

    /// <summary>global config が診断の重大度を下げても、build が TYPMOD022 で失敗すること</summary>
    [Test]
    public async Task A_global_config_that_lowers_a_diagnostic_fails_the_build()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            "<ItemGroup><GlobalAnalyzerConfigFiles Include=\"lower.globalconfig\" /></ItemGroup>"
        );
        fixture.WriteFile(
            "consumer/lower.globalconfig",
            "is_global = true\ndotnet_diagnostic.TYPMOD003.severity = none\n"
        );
        fixture.WriteSource(ConsumerFixture.ViolatingSource);

        var result = await fixture.BuildAsync();

        await Assert.That(result.Reports("TYPMOD022")).IsTrue().Because(result.Describe());
    }

    /// <summary>editorconfig の一括の重大度が診断を隠すと、build が TYPMOD022 で失敗すること</summary>
    /// <param name="key">一括の重大度の key</param>
    [Test]
    [Arguments("dotnet_analyzer_diagnostic.severity")]
    [Arguments("dotnet_analyzer_diagnostic.category-TypeModeling.severity")]
    public async Task A_bulk_severity_that_hides_the_diagnostics_fails_the_build(string key)
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteFile("consumer/.editorconfig", $"root = true\n\n[Source.cs]\n{key} = none\n");
        fixture.WriteSource(ConsumerFixture.ViolatingSource);

        var result = await fixture.BuildAsync();

        await Assert.That(result.Reports("TYPMOD022")).IsTrue().Because(result.Describe());
    }

    /// <summary>editorconfig の診断 ID の wildcard は compiler が解釈しないので、違反の診断が error のまま失敗すること</summary>
    [Test]
    public async Task An_editorconfig_wildcard_does_not_hide_a_violation()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteFile(
            "consumer/.editorconfig",
            "root = true\n\n[Source.cs]\ndotnet_diagnostic.TYPMOD*.severity = none\n"
        );
        fixture.WriteSource(ConsumerFixture.ViolatingSource);

        var result = await fixture.BuildAsync();

        await Assert.That(result.Reports("TYPMOD003")).IsTrue().Because(result.Describe());
    }

    /// <summary>TypeModeling と無関係な一括の重大度は許し、規則に従う project が build できること</summary>
    [Test]
    public async Task An_unrelated_bulk_severity_is_accepted()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteFile(
            "consumer/.editorconfig",
            "root = true\n\n[Source.cs]\ndotnet_analyzer_diagnostic.category-Style.severity = none\ndotnet_diagnostic.TYPMOD003.severity = error\n"
        );

        var result = await fixture.BuildAsync();

        await Assert.That(result.Succeeded).IsTrue().Because(result.Describe());
    }
}
