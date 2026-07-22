using System.Xml.Linq;

namespace TypeModeling.Testing.Closure;

/// <summary>solution の authored project graph</summary>
internal static class SolutionBuildGraph
{
    /// <summary>authored ProjectReference による静的 project 閉包</summary>
    /// <param name="repoRoot">solution root の絶対パス</param>
    /// <param name="solutionFileName">solution ファイル名</param>
    /// <returns>solution root からの project 相対パス</returns>
    internal static string[] DeclaredProjectPaths(
        string repoRoot,
        string solutionFileName)
    {
        var queue = new Queue<string>(SolutionProjectPaths(repoRoot, solutionFileName));
        var projectPaths = new HashSet<string>(StringComparer.Ordinal);

        while (queue.TryDequeue(out var projectPath))
        {
            projectPath = NormalizeProjectPath(repoRoot, projectPath);
            if (!projectPaths.Add(projectPath))
                continue;

            var projectDirectory = Path.GetDirectoryName(projectPath) ?? "";
            var declaredReferences = XDocument.Load(Path.Combine(repoRoot, projectPath))
                .Descendants()
                .Where(element => element.Name.LocalName == "ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .Where(include => !string.IsNullOrWhiteSpace(include))
                .Cast<string>();
            foreach (var declaredReference in declaredReferences)
            {
                if (declaredReference.Contains("$(", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"{projectPath}: inventoryが静的に解決できないProjectReference: " +
                        declaredReference);
                }

                queue.Enqueue(Path.Combine(projectDirectory, declaredReference));
            }
        }

        return projectPaths.Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>solution からの managed project 列挙</summary>
    /// <param name="repoRoot">solution root の絶対パス</param>
    /// <param name="solutionFileName">solution ファイル名</param>
    /// <returns>solution が宣言する project パス</returns>
    internal static string[] SolutionProjectPaths(
        string repoRoot,
        string solutionFileName)
    {
        var projectPaths = XDocument.Load(Path.Combine(repoRoot, solutionFileName))
            .Descendants()
            .Where(element => element.Name.LocalName == "Project")
            .Select(project => project.Attribute("Path")?.Value.Replace('\\', '/'))
            .Where(path => path?.EndsWith(".csproj", StringComparison.Ordinal) == true)
            .Cast<string>()
            .ToArray();
        if (projectPaths.Length == 0)
        {
            throw new InvalidOperationException(
                $"{solutionFileName} にmanaged .csprojがない");
        }

        return projectPaths;
    }

    /// <summary>solution root 相対 project path への正規化</summary>
    /// <param name="repoRoot">solution root の絶対パス</param>
    /// <param name="projectPath">正規化対象の project パス</param>
    /// <returns>solution root からの project 相対パス</returns>
    internal static string NormalizeProjectPath(string repoRoot, string projectPath)
    {
        var fullPath = Path.GetFullPath(Path.IsPathRooted(projectPath)
            ? projectPath
            : Path.Combine(repoRoot, projectPath));
        return Path.GetRelativePath(repoRoot, fullPath).Replace('\\', '/');
    }
}
