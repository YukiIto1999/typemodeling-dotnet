namespace TypeModeling.Testing.Attach;

/// <summary>MSBuild 評価済み repository authored file の発見</summary>
internal static class RepoAuthoredEvaluatedFileDiscovery
{
    /// <summary>MSBuild 評価結果に含まれる file 集合</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="authoredRoot">repository authored path の root</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>MSBuild 評価済み file 集合の非同期取得</returns>
    internal static async Task<IReadOnlyList<string>> FilesAsync(
        string repoRoot,
        string projectPath,
        string authoredRoot,
        CancellationToken cancellationToken)
    {
        var files = new List<string>();
        using var evaluation = await MsBuildQuery.RunAsync(
                repoRoot,
                projectPath,
                [
                    "-getProperty:MSBuildAllProjects",
                    "-getProperty:DirectoryBuildPropsPath",
                    "-getProperty:DirectoryBuildTargetsPath",
                    "-getProperty:DirectoryPackagesPropsPath",
                ],
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var importedPath in MsBuildValueSplitter.SplitSemicolonList(
                     MsBuildQuery.Property(evaluation, "MSBuildAllProjects")))
        {
            if (!Path.IsPathRooted(importedPath) ||
                !File.Exists(importedPath) ||
                !RepoAuthoredMsBuildPath.IsRepoAuthored(authoredRoot, importedPath))
            {
                continue;
            }
            files.Add(Path.GetFullPath(importedPath));
        }

        foreach (var propertyName in new[]
                 {
                     "DirectoryBuildPropsPath",
                     "DirectoryBuildTargetsPath",
                     "DirectoryPackagesPropsPath",
                 })
        {
            var importedPath = MsBuildQuery.Property(evaluation, propertyName);
            if (!string.IsNullOrWhiteSpace(importedPath) &&
                File.Exists(importedPath) &&
                RepoAuthoredMsBuildPath.IsRepoAuthored(authoredRoot, importedPath))
            {
                files.Add(Path.GetFullPath(importedPath));
            }
        }
        return files;
    }
}
