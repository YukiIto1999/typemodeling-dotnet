namespace TypeModeling.Testing.Attach;

/// <summary>guarded feature 利用 project の analyzer attachment 検査</summary>
public static class AnalyzerAttachmentConformance
{
    /// <summary>repository 全体の analyzer attachment 違反収集</summary>
    /// <remarks>
    /// project の評価と compile は検出器に依らないので、検めが複数あっても一回の走査で済ませる。
    /// 検めごとに走査すると、同じ project の compile が検めの数だけ重なる
    /// </remarks>
    /// <param name="repoRoot">検査対象 repository root</param>
    /// <param name="checks">同時に判ずる検めの並び</param>
    /// <param name="cacheDirectory">project の入力が同じなら検出の結果を使い回す控えの置き場で、使わなければ null</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>検めと同じ並びの正規化済み違反集合の非同期取得</returns>
    /// <exception cref="ArgumentException">検めが一つも無い</exception>
    /// <exception cref="OperationCanceledException">処理中止 token の取消</exception>
    public static async Task<IReadOnlyList<IReadOnlyList<string>>> ViolationsAsync(
        string repoRoot,
        IReadOnlyList<AnalyzerAttachmentCheck> checks,
        string? cacheDirectory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(checks);
        if (checks.Count == 0)
            throw new ArgumentException("検めが一つも無い", nameof(checks));
        var inputs = checks
            .Select(check => AnalyzerAttachmentInputValidator.Validate(
                repoRoot,
                check.Requirement,
                check.Detector))
            .ToArray();
        var root = inputs[0].RepositoryRoot;
        var graph = await AnalyzerAttachmentProjectGraph.CreateAsync(root, cancellationToken)
            .ConfigureAwait(false);
        var cache = cacheDirectory is null
            ? null
            : AttachVerdictCache.Create(root, cacheDirectory);
        var guardedProjects = await GuardedProjectInventoryProbe.FindAsync(
                root,
                graph,
                [.. checks.Select(check => check.Detector)],
                cache,
                cancellationToken)
            .ConfigureAwait(false);
        var results = new List<IReadOnlyList<string>>();
        for (var index = 0; index < checks.Count; index++)
        {
            var violations = new HashSet<string>(StringComparer.Ordinal);
            GuardedProjectInventoryPolicy.AddViolations(
                checks[index].Requirement,
                graph.PathComparer,
                guardedProjects[index],
                violations);
            await AnalyzerAttachmentPolicyEvaluation.AddViolationsAsync(
                    root,
                    inputs[index].AnalyzerPath,
                    checks[index].Requirement,
                    graph,
                    guardedProjects[index],
                    violations,
                    cancellationToken)
                .ConfigureAwait(false);
            results.Add(violations.Order(StringComparer.Ordinal).ToArray());
        }
        return results;
    }

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
        CancellationToken cancellationToken) =>
        (
            await ViolationsAsync(
                    repoRoot,
                    [new AnalyzerAttachmentCheck(requirement, detector)],
                    null,
                    cancellationToken)
                .ConfigureAwait(false)
        )[0];
}
