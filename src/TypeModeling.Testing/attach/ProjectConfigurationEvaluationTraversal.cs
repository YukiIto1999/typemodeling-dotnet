namespace TypeModeling.Testing.Attach;

/// <summary>analyzer 接続を検査する全 build 構成の固定点列挙</summary>
internal sealed class ProjectConfigurationEvaluationTraversal
{
    /// <summary>repository root</summary>
    private readonly string _repoRoot;

    /// <summary>project の絶対 path</summary>
    private readonly string _projectPath;

    /// <summary>構成評価編成器の構築</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="projectPath">project の絶対 path</param>
    internal ProjectConfigurationEvaluationTraversal(
        string repoRoot,
        string projectPath)
    {
        _repoRoot = repoRoot;
        _projectPath = projectPath;
    }

    /// <summary>全 build 構成の評価</summary>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>評価済み project 構成集合の非同期取得</returns>
    internal async Task<IReadOnlyList<EvaluatedProjectConfiguration>> EvaluateAllAsync(
        CancellationToken cancellationToken)
    {
        var seed = await ProjectConfigurationDimensionSeeder.ReadAsync(
                _repoRoot,
                _projectPath,
                cancellationToken)
            .ConfigureAwait(false);
        var worklist = new ProjectConfigurationPairWorklist(_repoRoot, _projectPath);
        worklist.Seed(seed.Configurations, seed.Platforms);
        var evaluations = new List<EvaluatedProjectConfiguration>();
        while (worklist.TryDequeue(out var pair))
        {
            var targetFrameworks = await worklist.DiscoverTargetFrameworksAsync(
                    pair,
                    cancellationToken)
                .ConfigureAwait(false);
            await ProjectTargetDimensionEvaluator.AddEvaluationsAsync(
                    _repoRoot,
                    _projectPath,
                    pair,
                    targetFrameworks,
                    evaluations,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return evaluations;
    }
}
