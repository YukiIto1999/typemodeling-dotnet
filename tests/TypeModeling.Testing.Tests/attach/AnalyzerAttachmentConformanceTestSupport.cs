using Microsoft.CodeAnalysis;
using TUnit.Core;
using TypeModeling.Testing.Attach;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>analyzer attachment conformance test の共有検証処理</summary>
internal static class AnalyzerAttachmentConformanceTestSupport
{
    /// <summary>既定 closed type detector による conformance violation 取得</summary>
    /// <param name="fixture">検査対象の一時 repository</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>conformance 違反集合の非同期取得</returns>
    internal static Task<IReadOnlyList<string>> ViolationsAsync(
        TemporaryAnalyzerAttachmentFixture fixture,
        CancellationToken cancellationToken) =>
        AnalyzerAttachmentConformance.ViolationsAsync(
            fixture.RepoRoot,
            fixture.Requirement,
            new ClosedTypeUsageDetector(),
            cancellationToken);

    /// <summary>指定文字列を含む policy violation の検証</summary>
    /// <param name="violations">engine が返した violation 集合</param>
    /// <param name="expected">violation に含まれる文字列</param>
    /// <param name="message">該当 violation がない場合の失敗メッセージ</param>
    internal static void AssertPolicyViolationContaining(
        IReadOnlyList<string> violations,
        string expected,
        string message)
    {
        if (!violations.Any(violation => violation.Contains(expected, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                message + ":\n" + string.Join("\n", violations));
        }
    }

    /// <summary>複数の指定文字列を全て含む policy violation の検証</summary>
    /// <param name="violations">engine が返した violation 集合</param>
    /// <param name="expected">同じ violation に含まれる文字列集合</param>
    /// <param name="message">該当 violation がない場合の失敗メッセージ</param>
    internal static void AssertPolicyViolationContainingAll(
        IReadOnlyList<string> violations,
        IReadOnlyList<string> expected,
        string message)
    {
        if (!violations.Any(violation => expected.All(fragment =>
                violation.Contains(fragment, StringComparison.Ordinal))))
        {
            throw new InvalidOperationException(
                message + ":\n" + string.Join("\n", violations));
        }
    }

}
