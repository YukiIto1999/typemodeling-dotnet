using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace TypeModeling.Analysis.Tests;

/// <summary>source の #pragma と SuppressMessage で TypeModeling の診断を弱めても、build が TYPMOD022 で失敗すること</summary>
/// <remarks>旧 AnalyzerAttachmentPragmaTests の性質に当たる。SuppressMessage は旧 test に無い</remarks>
[NotInParallel]
public sealed class SuppressionAttemptTests
{
    /// <summary>restore のない #pragma warning disable が、build を TYPMOD022 で失敗させること</summary>
    /// <param name="directive">source の末尾に足す directive</param>
    [Test]
    [Arguments("#pragma warning disable TYPMOD002")]
    [Arguments("#pragma warning disable TYPMOD003")]
    [Arguments("#pragma warning disable")]
    public async Task An_unrestored_pragma_fails_the_build(string directive)
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteSource(
            ConsumerFixture.CleanSource
                + "\n"
                + directive
                + "\nnamespace Fixture.Consumer { internal sealed class Probe; }\n"
        );

        var result = await fixture.BuildAsync();

        await Assert.That(result.Reports("TYPMOD022")).IsTrue().Because(result.Describe());
    }

    /// <summary>#pragma の wildcard は compiler が解釈しないので、違反の診断が error のまま失敗すること</summary>
    [Test]
    public async Task A_pragma_wildcard_does_not_hide_a_violation()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteSource("#pragma warning disable TYPMOD*\n" + ConsumerFixture.ViolatingSource);

        var result = await fixture.BuildAsync();

        await Assert.That(result.Reports("TYPMOD003")).IsTrue().Because(result.Describe());
    }

    /// <summary>同じ file の中で restore する #pragma と、コメントと無効な記述は許し、build が成功すること</summary>
    [Test]
    public async Task A_scoped_pragma_and_inactive_lookalikes_are_accepted()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteSource(
            """
            // #pragma warning disable TYPMOD002
            #if false
            #pragma warning disable TYPMOD003
            #endif
            #pragma warning disable TYPMOD002
            namespace Fixture.Consumer { internal sealed class Scoped; }
            #pragma warning restore TYPMOD002
            """
        );
        fixture.WriteFile(
            "consumer/.editorconfig",
            "root = true\n\n# dotnet_diagnostic.TYPMOD*.severity = none\n[DoesNotMatch.cs]\ndotnet_diagnostic.TYPMOD003.severity = none\n"
        );

        var result = await fixture.BuildAsync();

        await Assert.That(result.Succeeded).IsTrue().Because(result.Describe());
    }

    /// <summary>assembly 全体を対象にする SuppressMessage が、build を TYPMOD022 で失敗させること</summary>
    [Test]
    public async Task An_assembly_wide_SuppressMessage_fails_the_build()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteSource(
            "[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(\"TypeModeling\", \"TYPMOD003\")]\n"
                + ConsumerFixture.CleanSource
        );

        var result = await fixture.BuildAsync();

        await Assert.That(result.Reports("TYPMOD022")).IsTrue().Because(result.Describe());
    }
}
