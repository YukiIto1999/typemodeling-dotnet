namespace TypeModeling.Testing.Attach;

/// <summary>repository 内で到達可能な MSBuild file の探索</summary>
internal static class RepoAuthoredMsBuildGraphTraversal
{
    /// <summary>project から到達する repository authored MSBuild file 集合</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>到達可能な MSBuild file 集合の非同期取得</returns>
    internal static async Task<IReadOnlyList<RepoAuthoredMsBuildFile>> FilesAsync(
        string repoRoot,
        string projectPath,
        CancellationToken cancellationToken)
    {
        var projectFullPath = Path.GetFullPath(projectPath);
        var authoredRoot = Path.GetFullPath(repoRoot);
        var pending = new Queue<RepoAuthoredMsBuildFile>();
        var files = new Dictionary<string, bool>(StringComparer.Ordinal);
        var propertyValues = new Dictionary<string, List<RepoAuthoredPropertyValue>>(
            StringComparer.OrdinalIgnoreCase);
        var unresolvedImports = new HashSet<RepoAuthoredImport>();
        pending.Enqueue(new RepoAuthoredMsBuildFile(projectFullPath, false));

        RepoAuthoredImportTraversal.Drain(
            pending,
            files,
            propertyValues,
            unresolvedImports,
            projectFullPath,
            authoredRoot);
        var evaluatedFiles = await RepoAuthoredEvaluatedFileDiscovery.FilesAsync(
                repoRoot,
                projectPath,
                authoredRoot,
                cancellationToken)
            .ConfigureAwait(false);
        EnqueueMissing(evaluatedFiles, false, files, pending);

        RepoAuthoredImportTraversal.Drain(
            pending,
            files,
            propertyValues,
            unresolvedImports,
            projectFullPath,
            authoredRoot);
        EnqueueFallbackImports(
            unresolvedImports,
            projectFullPath,
            authoredRoot,
            propertyValues,
            files,
            pending);

        unresolvedImports.Clear();
        RepoAuthoredImportTraversal.Drain(
            pending,
            files,
            propertyValues,
            unresolvedImports,
            projectFullPath,
            authoredRoot);
        return files
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new RepoAuthoredMsBuildFile(pair.Key, pair.Value))
            .ToArray();
    }

    /// <summary>未解決 import の fallback 候補追加</summary>
    /// <param name="unresolvedImports">未解決 import 集合</param>
    /// <param name="projectFullPath">project の絶対 path</param>
    /// <param name="authoredRoot">repository authored path の root</param>
    /// <param name="propertyValues">property 名別の値集合</param>
    /// <param name="files">走査済み file 集合</param>
    /// <param name="pending">走査待ち file の追加先</param>
    private static void EnqueueFallbackImports(
        IEnumerable<RepoAuthoredImport> unresolvedImports,
        string projectFullPath,
        string authoredRoot,
        IReadOnlyDictionary<string, List<RepoAuthoredPropertyValue>> propertyValues,
        IReadOnlyDictionary<string, bool> files,
        Queue<RepoAuthoredMsBuildFile> pending)
    {
        foreach (var request in unresolvedImports)
        {
            var resolution = RepoAuthoredImportResolver.Resolve(
                request.Project,
                request.ImportingFile,
                projectFullPath,
                authoredRoot,
                propertyValues);
            EnqueueMissing(
                RepoAuthoredFallbackImportDiscovery.Candidates(
                    authoredRoot,
                    resolution.UnresolvedPatterns),
                true,
                files,
                pending);
        }
    }

    /// <summary>未走査候補の走査待ち queue への追加</summary>
    /// <param name="candidates">追加候補 path 集合</param>
    /// <param name="isConditionallyImported">条件付き import の判定</param>
    /// <param name="files">走査済み file 集合</param>
    /// <param name="pending">走査待ち file の追加先</param>
    private static void EnqueueMissing(
        IEnumerable<string> candidates,
        bool isConditionallyImported,
        IReadOnlyDictionary<string, bool> files,
        Queue<RepoAuthoredMsBuildFile> pending)
    {
        foreach (var candidate in candidates.Where(candidate => !files.ContainsKey(candidate)))
        {
            pending.Enqueue(new RepoAuthoredMsBuildFile(candidate, isConditionallyImported));
        }
    }
}
