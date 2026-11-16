using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.DiagnosticGuardFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>NoWarn などが compiler へ渡す設定による、TypeModeling の診断を弱める記述の検出</summary>
public sealed class DiagnosticGuardCompilerSettingsTests
{
    /// <summary>NoWarn などが compiler へ渡す診断の抑止の検出</summary>
    /// <param name="id">NoWarn に指定する診断 ID</param>
    [Test]
    [Arguments("TYPMOD002")]
    [Arguments("TYPMOD003")]
    [Arguments("TYPMOD101")]
    [Arguments("TYPMOD*")]
    public async Task NoWarn_of_a_TypeModeling_diagnostic_is_reported(string id)
    {
        var messages = await Messages(
            "class C;",
            new GuardSettings
            {
                Specific = ImmutableDictionary<string, ReportDiagnostic>.Empty.Add(
                    id,
                    ReportDiagnostic.Suppress
                ),
            }
        );

        await Assert
            .That(messages)
            .IsEquivalentTo([
                $"'{id}' の重大度が NoWarn など compiler の設定により none に下げられている",
            ]);
    }

    /// <summary>診断を弱めない compiler の設定の許可</summary>
    [Test]
    public async Task Compiler_settings_that_keep_the_diagnostics_are_accepted()
    {
        var messages = await Messages(
            "class C;",
            new GuardSettings
            {
                Specific = ImmutableDictionary<string, ReportDiagnostic>
                    .Empty.Add("TYPMOD003", ReportDiagnostic.Error)
                    .Add("TYPMOD017", ReportDiagnostic.Warn)
                    .Add("CS1591", ReportDiagnostic.Suppress),
            }
        );

        await Assert.That(messages).IsEmpty();
    }

    /// <summary>error の診断を warning へ下げる compiler の設定の検出</summary>
    [Test]
    public async Task A_compiler_setting_that_lowers_an_error_to_a_warning_is_reported()
    {
        var messages = await Messages(
            "class C;",
            new GuardSettings
            {
                Specific = ImmutableDictionary<string, ReportDiagnostic>.Empty.Add(
                    "TYPMOD003",
                    ReportDiagnostic.Warn
                ),
            }
        );

        await Assert
            .That(messages)
            .IsEquivalentTo([
                "'TYPMOD003' の重大度が NoWarn など compiler の設定により warning に下げられている",
            ]);
    }
}
