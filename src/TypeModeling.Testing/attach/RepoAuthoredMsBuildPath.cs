namespace TypeModeling.Testing.Attach;

/// <summary>repository authored MSBuild path の判定</summary>
internal static class RepoAuthoredMsBuildPath
{
    /// <summary>repository authored path の判定</summary>
    /// <param name="authoredRoot">repository authored path の root</param>
    /// <param name="path">検査対象 path</param>
    /// <returns>repository authored path の場合に true</returns>
    internal static bool IsRepoAuthored(string authoredRoot, string path)
    {
        if (!IsWithin(authoredRoot, path))
            return false;
        var relative = Path.GetRelativePath(authoredRoot, path).Replace('\\', '/');
        return !relative.Split('/').Any(segment =>
            segment is "bin" or "obj" or ".git" or "node_modules" or "StrykerOutput");
    }

    /// <summary>root に対する path 包含判定</summary>
    /// <param name="root">検査範囲の root</param>
    /// <param name="path">検査対象 path</param>
    /// <returns>root が path を包含する場合に true</returns>
    private static bool IsWithin(string root, string path)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        var normalizedPath = Path.GetFullPath(path);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return normalizedPath.StartsWith(normalizedRoot, comparison) ||
            string.Equals(
                normalizedPath,
                normalizedRoot.TrimEnd(Path.DirectorySeparatorChar),
                comparison);
    }
}
