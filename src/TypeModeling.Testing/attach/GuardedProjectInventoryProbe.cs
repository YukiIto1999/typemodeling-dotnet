namespace TypeModeling.Testing.Attach;

/// <summary>guarded feature 利用 project の semantic 探査</summary>
internal static class GuardedProjectInventoryProbe
{
    /// <summary>依存先先行 compile による guarded project 検出</summary>
    /// <param name="repoRoot">repository root の絶対 path</param>
    /// <param name="graph">評価済み project graph</param>
    /// <param name="detector">guarded feature の semantic 検出器</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>guarded feature を使う project の相対 path 集合の非同期取得</returns>
    internal static async Task<IReadOnlySet<string>> FindAsync(
        string repoRoot,
        AnalyzerAttachmentProjectGraph graph,
        IGuardedFeatureDetector detector,
        CancellationToken cancellationToken)
    {
        var guardedProjects = new HashSet<string>(graph.PathComparer);
        var generatedRoot = Path.Combine(
            Path.GetTempPath(),
            "typemodeling-analyzer-attachment-engine-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(generatedRoot);
        try
        {
            foreach (var relativeProject in ProjectReferenceOrder(
                         graph.ProjectPaths.Keys,
                         graph.ProjectReferences,
                         graph.PathComparer))
            {
                await MsBuildQuery.RestoreAsync(
                        repoRoot,
                        graph.ProjectPaths[relativeProject],
                        cancellationToken)
                    .ConfigureAwait(false);
                if (await UsesGuardedFeatureAsync(
                        repoRoot,
                        graph.ProjectPaths[relativeProject],
                        graph.Evaluations[relativeProject],
                        generatedRoot,
                        detector,
                        cancellationToken)
                    .ConfigureAwait(false))
                    guardedProjects.Add(relativeProject);
            }
        }
        finally
        {
            Directory.Delete(generatedRoot, recursive: true);
        }

        return guardedProjects;
    }

    /// <summary>単一 project の guarded feature 利用判定</summary>
    /// <param name="repoRoot">repository root の絶対 path</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="evaluations">評価済み project 構成</param>
    /// <param name="generatedRoot">生成物用一時 directory</param>
    /// <param name="detector">guarded feature の semantic 検出器</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>いずれかの構成が guarded feature を使う場合は <see langword="true"/></returns>
    private static async Task<bool> UsesGuardedFeatureAsync(
        string repoRoot,
        string projectPath,
        IEnumerable<EvaluatedProjectConfiguration> evaluations,
        string generatedRoot,
        IGuardedFeatureDetector detector,
        CancellationToken cancellationToken)
    {
        foreach (var evaluation in evaluations)
        {
            var generatedDirectory = Path.Combine(
                generatedRoot,
                Guid.NewGuid().ToString("N"));
            var compilation = await ProjectCompiler.CompileAsync(
                    repoRoot,
                    projectPath,
                    generatedDirectory,
                    evaluation,
                    cancellationToken)
                .ConfigureAwait(false);
            if (detector.UsesGuardedFeature(compilation))
                return true;
        }

        return false;
    }

    /// <summary>参照先を先行させる project 順序生成</summary>
    /// <param name="projects">repository 内 project 集合</param>
    /// <param name="references">project ごとの直接参照集合</param>
    /// <param name="pathComparer">file system path の比較器</param>
    /// <returns>参照先が先行する project 集合</returns>
    private static List<string> ProjectReferenceOrder(
        IEnumerable<string> projects,
        IReadOnlyDictionary<string, string[]> references,
        StringComparer pathComparer)
    {
        var ordered = new List<string>();
        var visited = new HashSet<string>(pathComparer);
        foreach (var project in projects.Order(StringComparer.Ordinal))
            Visit(project, references, visited, ordered);
        return ordered;
    }

    /// <summary>単一 project の依存先先行追加</summary>
    /// <param name="project">追加対象 project の相対 path</param>
    /// <param name="references">project ごとの直接参照集合</param>
    /// <param name="visited">追加済み project 集合</param>
    /// <param name="ordered">依存順 project の追加先</param>
    private static void Visit(
        string project,
        IReadOnlyDictionary<string, string[]> references,
        ISet<string> visited,
        ICollection<string> ordered)
    {
        if (!references.TryGetValue(project, out var directReferences) ||
            !visited.Add(project))
        {
            return;
        }
        foreach (var dependency in directReferences.Order(StringComparer.Ordinal))
            Visit(dependency, references, visited, ordered);
        ordered.Add(project);
    }
}
