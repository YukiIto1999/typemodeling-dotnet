using Microsoft.CodeAnalysis;
using TUnit.Core;
using TypeModeling.Testing.Attach;
using static TypeModeling.Testing.Tests.Attach.AnalyzerAttachmentConformanceTestSupport;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>pragma による診断抑止の検証</summary>
[NotInParallel]
public sealed class AnalyzerAttachmentPragmaTests
{
    /// <summary>restore のない保護対象 pragma の拒否</summary>
    /// <param name="diagnosticId">pragma に指定する診断 ID または prefix wildcard</param>
    [Test]
    [Arguments("TYPMOD001")]
    [Arguments("TYPMOD*")]
    [Arguments("TCSSUP001")]
    [Arguments("TCSSUP*")]
    public async Task Unrestored_analyzer_pragma_is_rejected(string diagnosticId)
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerSource(
            TemporaryAnalyzerAttachmentFixture.ClosedTypeDeclarations + Environment.NewLine + $$"""
            #pragma warning disable {{diagnosticId}}
            internal sealed class UnrestoredPragmaProbe;
            """);

        AssertPolicyViolationContainingAll(
            await ViolationsAsync(fixture, CancellationToken.None),
            ["#pragma warning disable", diagnosticId],
            "restore のない " + diagnosticId + " pragma 反例を検出できなかった");
    }

    /// <summary>scoped pragma と inactive lookalike の許可</summary>
    [Test]
    public async Task Scoped_analyzer_pragma_and_inactive_lookalikes_are_allowed()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.WriteConsumerSource(
            TemporaryAnalyzerAttachmentFixture.ClosedTypeDeclarations + Environment.NewLine + """
            // #pragma warning disable TYPMOD001
            #if false
            #pragma warning disable TCSSUP001
            #endif
            #pragma warning disable TYPMOD001
            internal sealed class ScopedPragmaProbe;
            #pragma warning restore TYPMOD001
            """);
        fixture.WriteEditorConfig("""
            root = true

            # dotnet_diagnostic.TYPMOD*.severity = none
            [DoesNotMatch.cs]
            dotnet_diagnostic.TCSSUP*.severity = none
            """);

        var violations = await ViolationsAsync(fixture, CancellationToken.None);
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "同一 file の局所 disable/restore または comment/無効コードを誤検出した:\n" +
                string.Join("\n", violations));
        }
    }

}
