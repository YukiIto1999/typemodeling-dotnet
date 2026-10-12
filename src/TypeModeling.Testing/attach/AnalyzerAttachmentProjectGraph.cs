using System.Collections.Concurrent;
using TypeModeling.Testing.Closure;

namespace TypeModeling.Testing.Attach;

/// <summary>analyzer attachment 検査対象の project graph</summary>
internal sealed class AnalyzerAttachmentProjectGraph
{
    /// <summary>MSBuild の評価を同時に走らせる数</summary>
    /// <remarks>評価は読むだけなので重ねてよい。外の process を起こす数が CPU の数を越えないようにする</remarks>
    private const int EvaluationParallelism = 6;

    /// <summary>project graph の構築</summary>
    /// <param name="pathComparer">file system path の比較器</param>
    /// <param name="projectPaths">相対 path ごとの project 絶対 path</param>
    /// <param name="evaluations">相対 path ごとの全 build 構成評価</param>
    /// <param name="projectReferences">相対 path ごとの参照 project 集合</param>
    private AnalyzerAttachmentProjectGraph(
        StringComparer pathComparer,
        Dictionary<string, string> projectPaths,
        Dictionary<string, IReadOnlyList<EvaluatedProjectConfiguration>> evaluations,
        Dictionary<string, string[]> projectReferences)
    {
        PathComparer = pathComparer;
        ProjectPaths = projectPaths;
        Evaluations = evaluations;
        ProjectReferences = projectReferences;
    }

    /// <summary>file system path の比較器</summary>
    internal StringComparer PathComparer { get; }

    /// <summary>相対 path ごとの project 絶対 path</summary>
    internal IReadOnlyDictionary<string, string> ProjectPaths { get; }

    /// <summary>相対 path ごとの全 build 構成評価</summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<EvaluatedProjectConfiguration>>
        Evaluations { get; }

    /// <summary>相対 path ごとの参照 project 集合</summary>
    internal IReadOnlyDictionary<string, string[]> ProjectReferences { get; }

    /// <summary>repository 内 project graph の構築</summary>
    /// <param name="repoRoot">repository root の絶対 path</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>評価済み project graph の非同期取得</returns>
    internal static async Task<AnalyzerAttachmentProjectGraph> CreateAsync(
        string repoRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pathComparer = FileSystemPathComparer;
        var projectPaths = RepoProjectInventory.ProjectPaths(repoRoot)
            .ToDictionary(
                path => path,
                path => Path.GetFullPath(Path.Combine(repoRoot, path)),
                pathComparer);
        var evaluated =
            new ConcurrentDictionary<string, IReadOnlyList<EvaluatedProjectConfiguration>>(
                pathComparer);
        await Parallel.ForEachAsync(
                projectPaths,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = EvaluationParallelism,
                    CancellationToken = cancellationToken,
                },
                async (pair, token) =>
                    evaluated[pair.Key] = await new ProjectConfigurationEvaluationTraversal(
                            repoRoot,
                            pair.Value)
                        .EvaluateAllAsync(token)
                        .ConfigureAwait(false))
            .ConfigureAwait(false);
        var evaluations = new Dictionary<string, IReadOnlyList<EvaluatedProjectConfiguration>>(
            evaluated,
            pathComparer);
        var projectReferences = projectPaths.ToDictionary(
            pair => pair.Key,
            pair => evaluations[pair.Key]
                .SelectMany(evaluation => evaluation.ProjectReferences)
                .Select(reference => reference.RelativePath)
                .Distinct(pathComparer)
                .ToArray(),
            pathComparer);
        return new AnalyzerAttachmentProjectGraph(
            pathComparer,
            projectPaths,
            evaluations,
            projectReferences);
    }

    /// <summary>実行環境に対応する file system path 比較器</summary>
    private static StringComparer FileSystemPathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
