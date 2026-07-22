namespace TypeModeling.Testing.Attach;

/// <summary>project 構成 pair の初期値取得</summary>
internal static class ProjectConfigurationDimensionSeeder
{
    /// <summary>project 既定値に基づく初期構成 pair 生成</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>初期構成 pair の非同期取得</returns>
    internal static async Task<(string[] Configurations, string[] Platforms)> ReadAsync(
        string repoRoot,
        string projectPath,
        CancellationToken cancellationToken)
    {
        using var dimensions = await MsBuildQuery.RunAsync(
                repoRoot,
                projectPath,
                [
                    "-getProperty:Configuration",
                    "-getProperty:Configurations",
                    "-getProperty:Platform",
                    "-getProperty:Platforms",
                ],
                cancellationToken)
            .ConfigureAwait(false);
        return (
            ProjectBuildDimensionValues.Resolve(
                MsBuildQuery.Property(dimensions, "Configurations"),
                MsBuildQuery.Property(dimensions, "Configuration"),
                "Debug"),
            ProjectBuildDimensionValues.Resolve(
                MsBuildQuery.Property(dimensions, "Platforms"),
                MsBuildQuery.Property(dimensions, "Platform"),
                "AnyCPU"));
    }
}
