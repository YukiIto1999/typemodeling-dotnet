namespace TypeModeling.Testing.Attach;

/// <summary>guarded project inventory の期待差分検査</summary>
internal static class GuardedProjectInventoryPolicy
{
    /// <summary>guarded project inventory 差分の追加</summary>
    /// <param name="requirement">analyzer attachment 要件</param>
    /// <param name="pathComparer">file system path の比較器</param>
    /// <param name="guardedProjects">検出済み guarded project 集合</param>
    /// <param name="violations">違反の追加先</param>
    internal static void AddViolations(
        AnalyzerAttachmentRequirement requirement,
        StringComparer pathComparer,
        IReadOnlySet<string> guardedProjects,
        ISet<string> violations)
    {
        var expectedProjects = requirement.ExpectedProjectPaths
            .Select(path => path.Replace('\\', '/'))
            .ToHashSet(pathComparer);
        var missing = expectedProjects.Except(guardedProjects, pathComparer)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var extra = guardedProjects.Except(expectedProjects, pathComparer)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (missing.Length == 0 && extra.Length == 0)
            return;
        violations.Add(
            "guarded project inventory: 不足: " + DisplayInventory(missing) +
            "。追加: " + DisplayInventory(extra));
    }

    /// <summary>inventory path 集合の表示</summary>
    /// <param name="paths">表示対象 path 集合</param>
    /// <returns>空集合を含む inventory 表示</returns>
    private static string DisplayInventory(string[] paths) =>
        paths.Length == 0 ? "なし" : string.Join(", ", paths);
}
