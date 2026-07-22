namespace TypeModeling.Testing.Attach;

/// <summary>target dimension の構成評価</summary>
internal static class ProjectTargetDimensionEvaluator
{
    /// <summary>target dimension 展開による評価済み構成追加</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="pair">評価対象構成 pair</param>
    /// <param name="targetFrameworks">評価対象の TargetFramework 集合</param>
    /// <param name="evaluations">評価済み構成の追加先</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>target dimension 評価の非同期完了</returns>
    internal static async Task AddEvaluationsAsync(
        string repoRoot,
        string projectPath,
        (string Configuration, string Platform) pair,
        IReadOnlyList<string> targetFrameworks,
        ICollection<EvaluatedProjectConfiguration> evaluations,
        CancellationToken cancellationToken)
    {
        var dimensionArguments = new[]
        {
            "-property:Configuration=" + pair.Configuration,
            "-property:Platform=" + pair.Platform,
        };
        foreach (var targetFramework in targetFrameworks)
        {
            var targetArguments = new List<string>(dimensionArguments);
            if (targetFramework.Length > 0)
                targetArguments.Add("-property:TargetFramework=" + targetFramework);
            using var runtimeDimensions = await MsBuildQuery.RunAsync(
                    repoRoot,
                    projectPath,
                    [
                        .. targetArguments,
                        "-getProperty:RuntimeIdentifier",
                        "-getProperty:RuntimeIdentifiers",
                    ],
                    cancellationToken)
                .ConfigureAwait(false);
            var runtimeIdentifiers = ProjectBuildDimensionValues.Resolve(
                MsBuildQuery.Property(runtimeDimensions, "RuntimeIdentifiers"),
                MsBuildQuery.Property(runtimeDimensions, "RuntimeIdentifier"),
                "",
                includeEmpty: true);
            foreach (var runtimeIdentifier in runtimeIdentifiers)
            {
                evaluations.Add(
                    await EvaluatedProjectConfigurationFactory.CreateAsync(
                            repoRoot,
                            projectPath,
                            pair.Configuration,
                            pair.Platform,
                            targetFramework,
                            runtimeIdentifier,
                            cancellationToken)
                        .ConfigureAwait(false));
            }
        }
    }
}
