namespace TypeModeling.Testing.Attach;

/// <summary>guarded feature 利用 project の semantic 探査</summary>
internal static class GuardedProjectInventoryProbe
{
    /// <summary>依存先先行 compile による guarded project 検出</summary>
    /// <remarks>
    /// compile は検出器に依らないので、全ての検出器を同じ compilation へ当てる。
    /// 検出器ごとに走査すると、同じ project の build が検出器の数だけ重なる。
    /// 入力が同じ project は控えた結果を使い、build しない
    /// </remarks>
    /// <param name="repoRoot">repository root の絶対 path</param>
    /// <param name="graph">評価済み project graph</param>
    /// <param name="detectors">guarded feature の semantic 検出器の並び</param>
    /// <param name="cache">検出の結果を使い回す控えで、使わなければ null</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>検出器と同じ並びの guarded feature を使う project の相対 path 集合の非同期取得</returns>
    internal static async Task<IReadOnlyList<IReadOnlySet<string>>> FindAsync(
        string repoRoot,
        AnalyzerAttachmentProjectGraph graph,
        IReadOnlyList<IGuardedFeatureDetector> detectors,
        AttachVerdictCache? cache,
        CancellationToken cancellationToken)
    {
        var guardedProjects = detectors
            .Select(_ => new HashSet<string>(graph.PathComparer))
            .ToArray();
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
                var uses = await VerdictsAsync(
                        repoRoot,
                        graph,
                        relativeProject,
                        generatedRoot,
                        detectors,
                        cache,
                        cancellationToken)
                    .ConfigureAwait(false);
                foreach (var index in Enumerable.Range(0, detectors.Count).Where(at => uses[at]))
                    guardedProjects[index].Add(relativeProject);
            }
        }
        finally
        {
            Directory.Delete(generatedRoot, recursive: true);
        }

        return guardedProjects;
    }

    /// <summary>一つの project の検出の結果の取得</summary>
    /// <remarks>控えに全ての検出器の結果が在れば build せず、無ければ build して控える</remarks>
    /// <param name="repoRoot">repository root の絶対 path</param>
    /// <param name="graph">評価済み project graph</param>
    /// <param name="project">project の相対 path</param>
    /// <param name="generatedRoot">生成物用一時 directory</param>
    /// <param name="detectors">guarded feature の semantic 検出器の並び</param>
    /// <param name="cache">検出の結果を使い回す控えで、使わなければ null</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>検出器と同じ並びの、guarded feature を使うかの真偽</returns>
    private static async Task<bool[]> VerdictsAsync(
        string repoRoot,
        AnalyzerAttachmentProjectGraph graph,
        string project,
        string generatedRoot,
        IReadOnlyList<IGuardedFeatureDetector> detectors,
        AttachVerdictCache? cache,
        CancellationToken cancellationToken)
    {
        var key = cache?.KeyOf(project, graph);
        var held = key is null ? [] : detectors.Select(detector => cache!.Read(key, detector)).ToArray();
        if (held.Length == detectors.Count && held.All(verdict => verdict is not null))
            return [.. held.Select(verdict => verdict!.Value)];
        await MsBuildQuery.RestoreAsync(repoRoot, graph.ProjectPaths[project], cancellationToken)
            .ConfigureAwait(false);
        var uses = await UsesGuardedFeaturesAsync(
                repoRoot,
                graph.ProjectPaths[project],
                graph.Evaluations[project],
                generatedRoot,
                detectors,
                cancellationToken)
            .ConfigureAwait(false);
        if (key is not null)
        {
            for (var index = 0; index < detectors.Count; index++)
                cache!.Write(key, detectors[index], uses[index]);
        }

        return uses;
    }

    /// <summary>単一 project の guarded feature 利用判定</summary>
    /// <param name="repoRoot">repository root の絶対 path</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="evaluations">評価済み project 構成</param>
    /// <param name="generatedRoot">生成物用一時 directory</param>
    /// <param name="detectors">guarded feature の semantic 検出器の並び</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>検出器と同じ並びの、いずれかの構成が guarded feature を使うかの真偽</returns>
    private static async Task<bool[]> UsesGuardedFeaturesAsync(
        string repoRoot,
        string projectPath,
        IEnumerable<EvaluatedProjectConfiguration> evaluations,
        string generatedRoot,
        IReadOnlyList<IGuardedFeatureDetector> detectors,
        CancellationToken cancellationToken)
    {
        var uses = new bool[detectors.Count];
        foreach (var evaluation in evaluations)
        {
            if (uses.All(held => held))
                break;
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
            for (var index = 0; index < detectors.Count; index++)
            {
                if (!uses[index])
                    uses[index] = detectors[index].UsesGuardedFeature(compilation);
            }
        }

        return uses;
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
