namespace TypeModeling.Testing.Closure;

/// <summary>solution の推移 ProjectReference 閉包の loader</summary>
public static class SolutionClosureLoader
{
    /// <summary>solution build 成果物に基づく推移 ProjectReference 閉包のロード</summary>
    /// <param name="scan">solution 閉包検査の宣言</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>静的 project 目録を含むロード済み CLR 閉包の非同期取得</returns>
    /// <exception cref="OperationCanceledException">処理中止 token の取消</exception>
    public static async Task<LoadedSolutionClosure> LoadAsync(
        SolutionClosureScan scan,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var repoRoot = Path.GetFullPath(scan.RepoRoot);
        var expectedProjectPaths = SolutionBuildGraph.DeclaredProjectPaths(
            repoRoot,
            scan.SolutionFileName);
        var outputRoot = Path.Combine(
            Path.GetTempPath(),
            "typemodeling-solution-closure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outputRoot);
        await SolutionBuilder.EnsureRestoredAsync(
                repoRoot,
                scan.SolutionFileName,
                expectedProjectPaths,
                cancellationToken)
            .ConfigureAwait(false);
        await SolutionBuilder.BuildAsync(
                repoRoot,
                scan.SolutionFileName,
                scan.Configuration,
                outputRoot,
                cancellationToken)
            .ConfigureAwait(false);
        var evaluatedProjects = await SolutionProjectClosureEvaluator.EvaluateAsync(
                repoRoot,
                scan.SolutionFileName,
                scan.Configuration,
                outputRoot,
                cancellationToken)
            .ConfigureAwait(false);
        var projects = SolutionAssemblyLoader.LoadProjects(evaluatedProjects);
        var reflectionTypes = SolutionAssemblyLoader.GetReflectionTypes(projects);
        var closure = new LoadedSolutionClosure(
            expectedProjectPaths,
            projects,
            reflectionTypes);
        LoadedSolutionClosureValidator.Validate(closure);
        return closure;
    }
}
