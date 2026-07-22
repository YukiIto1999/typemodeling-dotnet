namespace TypeModeling.Testing.Closure;

/// <summary>MSBuild 評価による managed project 閉包</summary>
internal static class SolutionProjectClosureEvaluator
{
    /// <summary>推移 ProjectReference 閉包の評価</summary>
    /// <param name="repoRoot">repository root の絶対パス</param>
    /// <param name="solutionFileName">solution ファイル名</param>
    /// <param name="configuration">build configuration</param>
    /// <param name="outputRoot">隔離した build output root</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>評価済み managed project 出力の非同期取得</returns>
    internal static async Task<EvaluatedSolutionProject[]> EvaluateAsync(
        string repoRoot,
        string solutionFileName,
        string configuration,
        string outputRoot,
        CancellationToken cancellationToken)
    {
        var queue = new Queue<string>(
            SolutionBuildGraph.SolutionProjectPaths(repoRoot, solutionFileName));
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var projects = new List<EvaluatedSolutionProject>();

        while (queue.TryDequeue(out var projectPath))
        {
            projectPath = SolutionBuildGraph.NormalizeProjectPath(repoRoot, projectPath);
            if (!visited.Add(projectPath))
                continue;

            using var evaluation = await ClosureMsBuildQuery.EvaluateAsync(
                    repoRoot,
                    projectPath,
                    configuration,
                    outputRoot,
                    cancellationToken)
                .ConfigureAwait(false);
            var assemblyName = ClosureMsBuildQuery.Property(evaluation, "AssemblyName");
            var targetPath = ClosureMsBuildQuery.Property(evaluation, "TargetPath");
            if (assemblyName.Length == 0 || targetPath.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{projectPath}: AssemblyName/TargetPathをMSBuild評価できない");
            }

            if (!File.Exists(targetPath))
            {
                throw new InvalidOperationException(
                    $"{projectPath}: solution build後のTargetPathが存在しない: {targetPath}");
            }

            projects.Add(new EvaluatedSolutionProject(
                projectPath,
                assemblyName,
                targetPath));
            foreach (var reference in ClosureMsBuildQuery.ProjectReferences(evaluation))
                queue.Enqueue(reference);
        }

        ThrowIfDuplicateAssemblyNames(projects);
        return projects
            .OrderBy(project => project.ProjectPath, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>AssemblyName 重複の拒否</summary>
    /// <param name="projects">評価済み managed project 出力</param>
    private static void ThrowIfDuplicateAssemblyNames(
        IReadOnlyList<EvaluatedSolutionProject> projects)
    {
        var duplicateAssemblyNames = projects
            .GroupBy(project => project.AssemblyName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"{group.Key}: {string.Join(", ", group.Select(project => project.ProjectPath))}")
            .ToArray();
        if (duplicateAssemblyNames.Length > 0)
        {
            throw new InvalidOperationException(
                "managed projectのAssemblyNameが重複している:\n  " +
                string.Join("\n  ", duplicateAssemblyNames));
        }
    }
}
