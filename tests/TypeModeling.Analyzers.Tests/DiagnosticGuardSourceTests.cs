using Microsoft.CodeAnalysis;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.DiagnosticGuardFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>source の中の #pragma と SuppressMessage による、TypeModeling の診断を弱める記述の検出</summary>
public sealed class DiagnosticGuardSourceTests
{
    /// <summary>restore のない #pragma warning disable の検出</summary>
    /// <param name="directive">source の先頭に置く directive</param>
    /// <param name="expected">期待する message</param>
    [Test]
    [Arguments(
        "#pragma warning disable TYPMOD003",
        "Source.cs の #pragma warning disable が 'TYPMOD003' を restore なしに無効にしている"
    )]
    [Arguments(
        "#pragma warning disable CS1591, TYPMOD003",
        "Source.cs の #pragma warning disable が 'TYPMOD003' を restore なしに無効にしている"
    )]
    [Arguments(
        "#pragma warning disable TYPMOD*",
        "Source.cs の #pragma warning disable が 'TYPMOD*' を restore なしに無効にしている"
    )]
    [Arguments(
        "#pragma warning disable",
        "Source.cs の #pragma warning disable に restore が無く、全ての診断が無効になる"
    )]
    public async Task An_unrestored_pragma_is_reported(string directive, string expected)
    {
        var messages = await Messages(directive + "\nclass C;\n");

        await Assert.That(messages).IsEquivalentTo([expected]);
    }

    /// <summary>同じ file の中で restore する #pragma と、有効でない記述の許可</summary>
    [Test]
    public async Task A_scoped_pragma_and_inactive_lookalikes_are_accepted()
    {
        var messages = await Messages(
            """
            // #pragma warning disable TYPMOD003
            #if false
            #pragma warning disable TYPMOD004
            #endif
            #pragma warning disable TYPMOD003
            class Scoped;
            #pragma warning restore TYPMOD003
            #pragma warning disable
            class AllScoped;
            #pragma warning restore
            #pragma warning disable CS1591
            class Unrelated;
            """
        );

        await Assert.That(messages).IsEmpty();
    }

    /// <summary>別の ID を restore しても残る #pragma の検出</summary>
    [Test]
    public async Task Restoring_another_diagnostic_does_not_restore_a_disabled_one()
    {
        var messages = await Messages(
            """
            #pragma warning disable TYPMOD003, TYPMOD004
            class C;
            #pragma warning restore TYPMOD004
            """
        );

        await Assert
            .That(messages)
            .IsEquivalentTo([
                "Source.cs の #pragma warning disable が 'TYPMOD003' を restore なしに無効にしている",
            ]);
    }

    /// <summary>assembly 全体を対象にする SuppressMessage の検出</summary>
    /// <param name="checkId">SuppressMessage の CheckId</param>
    [Test]
    [Arguments("TYPMOD003")]
    [Arguments("TYPMOD003:ClosedMustBeAbstract")]
    public async Task An_assembly_wide_SuppressMessage_is_reported(string checkId)
    {
        var messages = await Messages(
            $$"""
            using System.Diagnostics.CodeAnalysis;

            [assembly: SuppressMessage("TypeModeling", "{{checkId}}")]
            """
        );

        await Assert
            .That(messages)
            .IsEquivalentTo(["assembly の SuppressMessage が 'TYPMOD003' を抑止している"]);
    }

    /// <summary>範囲を絞った SuppressMessage と、無関係な診断の SuppressMessage の許可</summary>
    [Test]
    public async Task A_scoped_SuppressMessage_is_accepted()
    {
        var messages = await Messages(
            """
            using System.Diagnostics.CodeAnalysis;

            [assembly: SuppressMessage("TypeModeling", "TYPMOD003", Scope = "type", Target = "~T:Scoped")]
            [assembly: SuppressMessage("Documentation", "CS1591")]

            [SuppressMessage("TypeModeling", "TYPMOD004")]
            class Scoped;
            """
        );

        await Assert.That(messages).IsEmpty();
    }

    /// <summary>検出の診断は設定で変えられない</summary>
    [Test]
    public async Task The_guard_diagnostic_is_not_configurable()
    {
        var rule = new Diagnostics.Guard.DiagnosticGuardAnalyzer().SupportedDiagnostics.Single();

        await Assert.That(rule.CustomTags).Contains(WellKnownDiagnosticTags.NotConfigurable);
        await Assert.That(rule.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Error);
        await Assert.That(rule.IsEnabledByDefault).IsTrue();
    }
}
