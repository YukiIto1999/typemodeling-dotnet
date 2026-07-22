namespace TypeModeling.Testing.Closure;

/// <summary>ロード済み solution 閉包の事後条件</summary>
internal static class LoadedSolutionClosureValidator
{
    /// <summary>solution 閉包のロード結果検証</summary>
    /// <param name="closure">solution 閉包のロード結果</param>
    internal static void Validate(LoadedSolutionClosure closure)
    {
        var expectedProjectPaths = closure.ExpectedProjectPaths.ToHashSet(StringComparer.Ordinal);
        var loadedProjectPaths = closure.Projects
            .Select(project => project.ProjectPath)
            .ToHashSet(StringComparer.Ordinal);
        var inventoryViolations = expectedProjectPaths
            .Except(loadedProjectPaths, StringComparer.Ordinal)
            .Select(projectPath => $"{projectPath}: build graph project was not loaded")
            .Concat(loadedProjectPaths
                .Except(expectedProjectPaths, StringComparer.Ordinal)
                .Select(projectPath =>
                    $"{projectPath}: loaded project is absent from the declared build graph"));
        var violations = closure.Projects
            .SelectMany(project =>
            {
                var matchingAssemblies = closure.Projects.Count(candidate =>
                    candidate.AssemblyName == project.AssemblyName);
                var loadedTypeCount = closure.ReflectionTypes.Count(type =>
                    type.Assembly == project.Assembly);
                var projectViolations = new List<string>();
                if (!string.Equals(
                        Path.GetFullPath(project.Assembly.Location),
                        Path.GetFullPath(project.TargetPath),
                        StringComparison.Ordinal))
                {
                    projectViolations.Add(
                        $"{project.ProjectPath}: assembly {project.AssemblyName} was loaded from " +
                        $"{project.Assembly.Location}, expected TargetPath {project.TargetPath}");
                }

                if (project.Assembly.ManifestModule.ModuleVersionId != project.TargetMvid)
                {
                    projectViolations.Add(
                        $"{project.ProjectPath}: assembly {project.AssemblyName} has MVID " +
                        $"{project.Assembly.ManifestModule.ModuleVersionId}, " +
                        $"expected TargetPath MVID {project.TargetMvid}");
                }

                if (matchingAssemblies != 1)
                {
                    projectViolations.Add(
                        $"{project.ProjectPath}: assembly {project.AssemblyName} loaded " +
                        $"{matchingAssemblies} times");
                }

                if (loadedTypeCount == 0)
                {
                    projectViolations.Add(
                        $"{project.ProjectPath}: assembly {project.AssemblyName} has zero CLR reflection types");
                }

                return projectViolations;
            })
            .Concat(inventoryViolations)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (violations.Length > 0)
        {
            throw new InvalidOperationException(
                "solution の推移 ProjectReference 閉包にある全 managed project をロードすること" +
                $"(expected={expectedProjectPaths.Count}, loaded={loadedProjectPaths.Count})。違反:\n  " +
                string.Join("\n  ", violations));
        }
    }
}
