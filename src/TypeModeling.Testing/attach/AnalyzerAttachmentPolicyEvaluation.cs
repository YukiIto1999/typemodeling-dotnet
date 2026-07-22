namespace TypeModeling.Testing.Attach;

/// <summary>guarded project の analyzer attachment policy 照合</summary>
internal static class AnalyzerAttachmentPolicyEvaluation
{
    /// <summary>guarded project ごとの policy 違反追加</summary>
    /// <param name="repoRoot">repository root の絶対 path</param>
    /// <param name="analyzerPath">analyzer project の絶対 path</param>
    /// <param name="requirement">analyzer attachment 要件</param>
    /// <param name="graph">評価済み project graph</param>
    /// <param name="guardedProjects">検出済み guarded project 集合</param>
    /// <param name="violations">違反の追加先</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>policy 違反追加の非同期完了</returns>
    internal static async Task AddViolationsAsync(
        string repoRoot,
        string analyzerPath,
        AnalyzerAttachmentRequirement requirement,
        AnalyzerAttachmentProjectGraph graph,
        IEnumerable<string> guardedProjects,
        ISet<string> violations,
        CancellationToken cancellationToken)
    {
        var matcher = new DiagnosticMatcher(
            requirement.ProtectedDiagnosticIds,
            requirement.ProtectedDiagnosticPrefixes,
            requirement.ProtectedDiagnosticCategories);
        foreach (var relativeProject in guardedProjects)
        {
            var projectViolations = await AnalyzerReferencePolicy.ViolationsAsync(
                    repoRoot,
                    graph.ProjectPaths[relativeProject],
                    analyzerPath,
                    graph.Evaluations[relativeProject],
                    matcher,
                    cancellationToken)
                .ConfigureAwait(false);
            foreach (var violation in projectViolations)
            {
                violations.Add(relativeProject + ": " + violation);
            }
        }
    }
}
