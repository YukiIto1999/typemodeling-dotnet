using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.DiagnosticGuardFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>TypeModeling の診断を弱める設定の検出</summary>
public sealed class DiagnosticGuardTests
{
    /// <summary>設定が無い compilation は何も報告しない</summary>
    [Test]
    public async Task A_compilation_without_settings_is_accepted()
    {
        await Assert.That(await Messages("class C;")).IsEmpty();
    }

    /// <summary>.editorconfig が TypeModeling の診断を既定より下げる設定の検出</summary>
    /// <param name="severity">.editorconfig の重大度</param>
    [Test]
    [Arguments("none")]
    [Arguments("silent")]
    [Arguments("suggestion")]
    [Arguments("warning")]
    public async Task An_editorconfig_that_lowers_an_error_diagnostic_is_reported(string severity)
    {
        var messages = await Messages(
            "class C;",
            new GuardSettings
            {
                EditorConfig =
                    $"root = true\n\n[Source.cs]\ndotnet_diagnostic.TYPMOD003.severity = {severity}\n",
            }
        );

        await Assert
            .That(messages)
            .IsEquivalentTo([
                $"'TYPMOD003' の重大度が .editorconfig の設定により {severity} に下げられている",
            ]);
    }

    /// <summary>重大度を保つか上げる設定と、source に当たらない section の許可</summary>
    [Test]
    public async Task An_editorconfig_that_keeps_the_severity_is_accepted()
    {
        var messages = await Messages(
            "class C;",
            new GuardSettings
            {
                EditorConfig = """
                root = true

                [Source.cs]
                dotnet_diagnostic.TYPMOD003.severity = error
                dotnet_diagnostic.TYPMOD017.severity = error

                [DoesNotMatch.cs]
                dotnet_diagnostic.TYPMOD004.severity = none
                """,
            }
        );

        await Assert.That(messages).IsEmpty();
    }

    /// <summary>既定が warning の診断は、warning までは下げたと数えない</summary>
    /// <param name="severity">.editorconfig の重大度</param>
    /// <param name="reported">報告を期待する場合に true</param>
    [Test]
    [Arguments("warning", false)]
    [Arguments("suggestion", true)]
    [Arguments("none", true)]
    public async Task A_warning_by_default_diagnostic_is_lowered_only_below_warning(
        string severity,
        bool reported
    )
    {
        var messages = await Messages(
            "class C;",
            new GuardSettings
            {
                EditorConfig =
                    $"root = true\n\n[Source.cs]\ndotnet_diagnostic.TYPMOD017.severity = {severity}\n",
            }
        );

        string[] expected = reported
            ? [$"'TYPMOD017' の重大度が .editorconfig の設定により {severity} に下げられている"]
            : [];

        await Assert.That(messages).IsEquivalentTo(expected);
    }

    /// <summary>既定で無効な診断は、利用側が有効にしていないだけなので守らない</summary>
    [Test]
    public async Task The_opt_in_diagnostic_is_not_guarded()
    {
        var messages = await Messages(
            "class C;",
            new GuardSettings
            {
                EditorConfig =
                    "root = true\n\n[Source.cs]\ndotnet_diagnostic.TYPMOD021.severity = none\n",
            }
        );

        await Assert.That(messages).IsEmpty();
    }

    /// <summary>抑止の規則を無効にする設定の検出</summary>
    [Test]
    public async Task Disabling_a_suppression_is_reported()
    {
        var messages = await Messages(
            "class C;",
            new GuardSettings
            {
                EditorConfig =
                    "root = true\n\n[Source.cs]\ndotnet_diagnostic.TYPMOD101.severity = none\n",
            }
        );

        await Assert
            .That(messages)
            .IsEquivalentTo([
                "'TYPMOD101' の重大度が .editorconfig の設定により none に下げられている",
            ]);
    }

    /// <summary>全ての source に当たる global config の設定の検出</summary>
    [Test]
    public async Task A_global_config_that_lowers_a_diagnostic_is_reported()
    {
        var messages = await Messages(
            "class C;",
            new GuardSettings
            {
                GlobalConfig = "is_global = true\ndotnet_diagnostic.TYPMOD005.severity = none\n",
            }
        );

        await Assert
            .That(messages)
            .IsEquivalentTo([
                "'TYPMOD005' の重大度が global config の設定により none に下げられている",
            ]);
    }

    /// <summary>診断を隠す一括の重大度の設定の検出</summary>
    /// <param name="key">一括の重大度の key</param>
    /// <param name="severity">一括の重大度</param>
    [Test]
    [Arguments("dotnet_analyzer_diagnostic.severity", "none")]
    [Arguments("dotnet_analyzer_diagnostic.severity", "silent")]
    [Arguments("dotnet_analyzer_diagnostic.severity", "suggestion")]
    [Arguments("dotnet_analyzer_diagnostic.category-TypeModeling.severity", "none")]
    [Arguments("dotnet_analyzer_diagnostic.category-TypeModeling.severity", "suggestion")]
    public async Task A_bulk_severity_that_hides_the_diagnostics_is_reported(
        string key,
        string severity
    )
    {
        var messages = await Messages(
            "class C;",
            new GuardSettings { EditorConfig = $"root = true\n\n[Source.cs]\n{key} = {severity}\n" }
        );

        await Assert
            .That(messages)
            .IsEquivalentTo([$"{key} が {severity} なので、TypeModeling の診断が隠れる"]);
    }

    /// <summary>global config の一括の重大度の設定の検出</summary>
    [Test]
    public async Task A_global_bulk_severity_that_hides_the_diagnostics_is_reported()
    {
        var messages = await Messages(
            "class C;",
            new GuardSettings
            {
                GlobalConfig = "is_global = true\ndotnet_analyzer_diagnostic.severity = none\n",
            }
        );

        await Assert
            .That(messages)
            .IsEquivalentTo([
                "dotnet_analyzer_diagnostic.severity が none なので、TypeModeling の診断が隠れる",
            ]);
    }

    /// <summary>TypeModeling に無関係な一括の設定と、診断を隠さない一括の設定の許可</summary>
    /// <param name="line">.editorconfig の行</param>
    [Test]
    [Arguments("dotnet_analyzer_diagnostic.category-Style.severity = none")]
    [Arguments("dotnet_analyzer_diagnostic.severity = warning")]
    [Arguments("dotnet_analyzer_diagnostic.category-TypeModeling.severity = error")]
    public async Task A_bulk_severity_that_keeps_the_diagnostics_visible_is_accepted(string line)
    {
        var messages = await Messages(
            "class C;",
            new GuardSettings { EditorConfig = $"root = true\n\n[Source.cs]\n{line}\n" }
        );

        await Assert.That(messages).IsEmpty();
    }
}
