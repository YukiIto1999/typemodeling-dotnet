using Microsoft.CodeAnalysis;
using TUnit.Core;
using TypeModeling.Testing.Attach;
using static TypeModeling.Testing.Tests.Attach.AnalyzerAttachmentConformanceTestSupport;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>editorconfig による診断抑止の検証</summary>
[NotInParallel]
public sealed class AnalyzerAttachmentEditorConfigTests
{
    /// <summary>適用対象 editorconfig による診断 severity 低下の拒否</summary>
    /// <param name="severity">検査対象の editorconfig severity</param>
    [Test]
    [Arguments("none")]
    [Arguments("silent")]
    [Arguments("suggestion")]
    public async Task Applicable_editorconfig_suppression_is_rejected(string severity)
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteEditorConfig($$"""
            root = true

            [ClosedTypes.cs]
            dotnet_diagnostic.TYPMOD001.severity = {{severity}}

            [DoesNotMatch.cs]
            dotnet_diagnostic.TCSSUP001.severity = none
            """);

        AssertPolicyViolationContainingAll(
            await ViolationsAsync(fixture, CancellationToken.None),
            [".editorconfig", "TYPMOD001", severity],
            "適用対象 .editorconfig で TYPMOD001 を " + severity + " にする反例を検出できなかった");
    }

    /// <summary>適用対象 editorconfig wildcard による診断抑止の拒否</summary>
    /// <param name="diagnosticPattern">検査対象の診断 prefix wildcard</param>
    [Test]
    [Arguments("TYPMOD*")]
    [Arguments("TCSSUP*")]
    public async Task Applicable_editorconfig_wildcard_suppression_is_rejected(string diagnosticPattern)
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteEditorConfig($$"""
            root = true

            [ClosedTypes.cs]
            dotnet_diagnostic.{{diagnosticPattern}}.severity = none

            [DoesNotMatch.cs]
            dotnet_diagnostic.TYPMOD001.severity = none
            """);

        AssertPolicyViolationContainingAll(
            await ViolationsAsync(fixture, CancellationToken.None),
            [".editorconfig", diagnosticPattern, "none"],
            "適用対象 .editorconfig で " + diagnosticPattern + " を none にする反例を検出できなかった");
    }

    /// <summary>editorconfig の analyzer 一括 severity 低下の拒否</summary>
    /// <param name="bulkKey">検査対象の一括 severity key</param>
    [Test]
    [Arguments("dotnet_analyzer_diagnostic.severity")]
    [Arguments("dotnet_analyzer_diagnostic.category-TypeModeling.severity")]
    public async Task Applicable_editorconfig_bulk_suppression_is_rejected(string bulkKey)
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteEditorConfig($$"""
            root = true

            [ClosedTypes.cs]
            {{bulkKey}} = none
            """);

        AssertPolicyViolationContainingAll(
            await ViolationsAsync(fixture, CancellationToken.None),
            [".editorconfig", bulkKey, "none"],
            "適用対象 .editorconfig の analyzer 一括抑止を検出できなかった");
    }

    /// <summary>無関係な analyzer category 一括抑止の許可</summary>
    [Test]
    public async Task Unrelated_editorconfig_bulk_category_suppression_is_allowed()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteEditorConfig("""
            root = true

            [ClosedTypes.cs]
            dotnet_analyzer_diagnostic.category-Style.severity = none
            """);

        var violations = await ViolationsAsync(fixture, CancellationToken.None);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "保護対象と無関係な analyzer category 設定を誤検出した:\n" +
                string.Join("\n", violations));
        }
    }

}
