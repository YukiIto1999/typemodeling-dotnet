namespace TypeModeling.Testing.Attach;

/// <summary>guarded project の analyzer attachment policy</summary>
internal static class AnalyzerReferencePolicy
{
    /// <summary>project の analyzer attachment 違反収集</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="analyzerPath">analyzer project の絶対 path</param>
    /// <param name="evaluations">全評価構成</param>
    /// <param name="matcher">保護対象診断の照合器</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>project path prefix 前の違反集合の非同期取得</returns>
    internal static async Task<IReadOnlyList<string>> ViolationsAsync(
        string repoRoot,
        string projectPath,
        string analyzerPath,
        IReadOnlyList<EvaluatedProjectConfiguration> evaluations,
        DiagnosticMatcher matcher,
        CancellationToken cancellationToken)
    {
        var violations = new HashSet<string>(StringComparer.Ordinal);
        await RepoAuthoredAnalyzerPolicy.AddViolationsAsync(
                repoRoot,
                projectPath,
                analyzerPath,
                matcher,
                violations,
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var evaluation in evaluations)
        {
            EvaluatedAnalyzerAttachmentPolicy.AddViolations(
                repoRoot,
                analyzerPath,
                evaluation,
                matcher,
                violations);
        }
        return violations.Order(StringComparer.Ordinal).ToArray();
    }
}
