namespace TypeModeling.Testing.Attach;

/// <summary>guarded feature 利用 project の analyzer attachment 検査</summary>
public static class AnalyzerAttachmentConformance
{
    /// <summary>repository 全体の analyzer attachment 違反収集</summary>
    /// <param name="repoRoot">検査対象 repository root</param>
    /// <param name="requirement">analyzer attachment 要件</param>
    /// <param name="detector">guarded feature の semantic 検出器</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>正規化済み違反集合の非同期取得</returns>
    /// <exception cref="OperationCanceledException">処理中止 token の取消</exception>
    public static async Task<IReadOnlyList<string>> ViolationsAsync(
        string repoRoot,
        AnalyzerAttachmentRequirement requirement,
        IGuardedFeatureDetector detector,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var input = AnalyzerAttachmentInputValidator.Validate(
            repoRoot,
            requirement,
            detector);
        var graph = await AnalyzerAttachmentProjectGraph.CreateAsync(
                input.RepositoryRoot,
                cancellationToken)
            .ConfigureAwait(false);
        var guardedProjects = await GuardedProjectInventoryProbe.FindAsync(
                input.RepositoryRoot,
                graph,
                detector,
                cancellationToken)
            .ConfigureAwait(false);
        var violations = new HashSet<string>(StringComparer.Ordinal);
        GuardedProjectInventoryPolicy.AddViolations(
            requirement,
            graph.PathComparer,
            guardedProjects,
            violations);
        await AnalyzerAttachmentPolicyEvaluation.AddViolationsAsync(
                input.RepositoryRoot,
                input.AnalyzerPath,
                requirement,
                graph,
                guardedProjects,
                violations,
                cancellationToken)
            .ConfigureAwait(false);
        return violations.Order(StringComparer.Ordinal).ToArray();
    }
}
