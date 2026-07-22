namespace TypeModeling.Testing.Attach;

/// <summary>repository authored MSBuild import の解決</summary>
internal static class RepoAuthoredImportResolver
{
    /// <summary>MSBuild import expression の解決</summary>
    /// <param name="importedProject">Import Project expression</param>
    /// <param name="importingFile">宣言元 file</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="authoredRoot">repository authored path の root</param>
    /// <param name="propertyValues">repository authored property 値集合</param>
    /// <returns>import expression の解決結果</returns>
    internal static RepoAuthoredImportResolution Resolve(
        string importedProject,
        string importingFile,
        string projectPath,
        string authoredRoot,
        IReadOnlyDictionary<string, List<RepoAuthoredPropertyValue>> propertyValues)
    {
        var expandedBuiltIns = importedProject
            .Replace(
                "$(MSBuildThisFileDirectory)",
                Path.GetDirectoryName(importingFile)! + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            .Replace(
                "$(MSBuildProjectDirectory)",
                Path.GetDirectoryName(projectPath)!,
                StringComparison.OrdinalIgnoreCase);
        var expansion = RepoAuthoredPropertyExpander.Expand(
            expandedBuiltIns,
            projectPath,
            propertyValues);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var expanded in expansion.Values)
            AddResolvedPaths(expanded, importingFile, authoredRoot, paths);
        return new RepoAuthoredImportResolution(
            paths.Order(StringComparer.Ordinal).ToArray(),
            expansion.UnresolvedValues);
    }

    /// <summary>展開済み import expression から repository authored path を追加</summary>
    /// <param name="expanded">展開済み import expression</param>
    /// <param name="importingFile">宣言元 file</param>
    /// <param name="authoredRoot">repository authored path の root</param>
    /// <param name="paths">解決済み path の追加先</param>
    private static void AddResolvedPaths(
        string expanded,
        string importingFile,
        string authoredRoot,
        HashSet<string> paths)
    {
        foreach (var item in expanded.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var fullPath in ExpandImportItem(item, importingFile).Where(fullPath =>
                         File.Exists(fullPath) &&
                         RepoAuthoredMsBuildPath.IsRepoAuthored(authoredRoot, fullPath)))
            {
                paths.Add(fullPath);
            }
        }
    }

    /// <summary>import item の path 展開</summary>
    /// <param name="item">展開対象 import item</param>
    /// <param name="importingFile">宣言元 file</param>
    /// <returns>展開済み file path 集合</returns>
    private static IEnumerable<string> ExpandImportItem(string item, string importingFile)
    {
        if (item.Contains("$(", StringComparison.Ordinal))
            yield break;
        var importingDirectory = Path.GetDirectoryName(importingFile)!;
        if (item.IndexOfAny(['*', '?']) < 0)
        {
            yield return Path.GetFullPath(
                Path.IsPathRooted(item) ? item : Path.Combine(importingDirectory, item));
            yield break;
        }
        var rootedPattern = Path.IsPathRooted(item)
            ? item
            : Path.Combine(importingDirectory, item);
        var wildcardIndex = rootedPattern.IndexOfAny(['*', '?']);
        var separatorIndex = rootedPattern.LastIndexOfAny(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            wildcardIndex);
        var searchRoot = separatorIndex == 0
            ? Path.GetPathRoot(rootedPattern)!
            : rootedPattern[..separatorIndex];
        if (!Directory.Exists(searchRoot))
            yield break;
        var relativePattern = rootedPattern[(separatorIndex + 1)..].Replace('\\', '/');
        var searchOption = relativePattern.Contains('/', StringComparison.Ordinal)
            ? SearchOption.AllDirectories
            : SearchOption.TopDirectoryOnly;
        foreach (var path in Directory.EnumerateFiles(searchRoot, "*", searchOption))
        {
            var relativePath = Path.GetRelativePath(searchRoot, path).Replace('\\', '/');
            if (System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(
                    relativePattern,
                    relativePath,
                    ignoreCase: false))
            {
                yield return Path.GetFullPath(path);
            }
        }
    }
}
