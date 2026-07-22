namespace TypeModeling.Testing.Attach;

/// <summary>project 構成 pair の固定点 worklist</summary>
internal sealed class ProjectConfigurationPairWorklist
{
    /// <summary>構成 pair の走査上限</summary>
    private const int MaximumScheduledPairCount = 256;

    /// <summary>repository root</summary>
    private readonly string repoRoot;

    /// <summary>project の絶対 path</summary>
    private readonly string projectPath;

    /// <summary>発見済み Configuration 集合</summary>
    private readonly HashSet<string> configurations =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>発見済み Platform 集合</summary>
    private readonly HashSet<string> platforms =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>評価待ち構成 pair queue</summary>
    private readonly Queue<(string Configuration, string Platform)> pendingPairs = new();

    /// <summary>走査予約済み構成 pair key 集合</summary>
    private readonly HashSet<string> scheduledPairs =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>固定点 worklist の構築</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="projectPath">project の絶対 path</param>
    internal ProjectConfigurationPairWorklist(string repoRoot, string projectPath)
    {
        this.repoRoot = repoRoot;
        this.projectPath = projectPath;
    }

    /// <summary>初期構成 pair の登録</summary>
    /// <param name="initialConfigurations">初期 Configuration 集合</param>
    /// <param name="initialPlatforms">初期 Platform 集合</param>
    internal void Seed(
        IEnumerable<string> initialConfigurations,
        IEnumerable<string> initialPlatforms)
    {
        foreach (var configuration in initialConfigurations)
            configurations.Add(configuration);
        foreach (var platform in initialPlatforms)
            platforms.Add(platform);
        foreach (var configuration in configurations)
        {
            foreach (var platform in platforms)
                Enqueue(configuration, platform);
        }
    }

    /// <summary>評価待ち構成 pair の取得</summary>
    /// <param name="pair">取得済み構成 pair</param>
    /// <returns>評価待ち構成 pair がある場合に true</returns>
    internal bool TryDequeue(out (string Configuration, string Platform) pair) =>
        pendingPairs.TryDequeue(out pair);

    /// <summary>構成 pair 固有 dimension の固定点更新</summary>
    /// <param name="pair">評価対象構成 pair</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>構成 pair で有効な TargetFramework 集合の非同期取得</returns>
    internal async Task<string[]> DiscoverTargetFrameworksAsync(
        (string Configuration, string Platform) pair,
        CancellationToken cancellationToken)
    {
        using var targetDimensions = await MsBuildQuery.RunAsync(
                repoRoot,
                projectPath,
                [
                    "-property:Configuration=" + pair.Configuration,
                    "-property:Platform=" + pair.Platform,
                    "-getProperty:Configuration",
                    "-getProperty:Configurations",
                    "-getProperty:Platform",
                    "-getProperty:Platforms",
                    "-getProperty:TargetFramework",
                    "-getProperty:TargetFrameworks",
                ],
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var discoveredConfiguration in ProjectBuildDimensionValues.Resolve(
                     MsBuildQuery.Property(targetDimensions, "Configurations"),
                     MsBuildQuery.Property(targetDimensions, "Configuration"),
                     pair.Configuration))
        {
            if (!configurations.Add(discoveredConfiguration))
                continue;
            foreach (var platform in platforms)
                Enqueue(discoveredConfiguration, platform);
        }
        foreach (var discoveredPlatform in ProjectBuildDimensionValues.Resolve(
                     MsBuildQuery.Property(targetDimensions, "Platforms"),
                     MsBuildQuery.Property(targetDimensions, "Platform"),
                     pair.Platform))
        {
            if (!platforms.Add(discoveredPlatform))
                continue;
            foreach (var configuration in configurations)
                Enqueue(configuration, discoveredPlatform);
        }

        return ProjectBuildDimensionValues.Resolve(
            MsBuildQuery.Property(targetDimensions, "TargetFrameworks"),
            MsBuildQuery.Property(targetDimensions, "TargetFramework"),
            "");
    }

    /// <summary>構成 pair の走査待ち登録</summary>
    /// <param name="configuration">Configuration 値</param>
    /// <param name="platform">Platform 値</param>
    private void Enqueue(string configuration, string platform)
    {
        if (!scheduledPairs.Add(configuration + "\0" + platform))
            return;
        if (scheduledPairs.Count > MaximumScheduledPairCount)
        {
            throw new InvalidOperationException(
                "MSBuild の Configuration と Platform の組合せが上限を超えた: " +
                projectPath);
        }
        pendingPairs.Enqueue((configuration, platform));
    }
}
