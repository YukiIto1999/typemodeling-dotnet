namespace TypeModeling.Testing.Attach;

/// <summary>analyzer attachment 検査入力の検証</summary>
internal static class AnalyzerAttachmentInputValidator
{
    /// <summary>公開入力の正規化</summary>
    /// <param name="repoRoot">検査対象 repository root</param>
    /// <param name="requirement">analyzer attachment 要件</param>
    /// <param name="detector">guarded feature の semantic 検出器</param>
    /// <returns>正規化済み attachment 検査入力</returns>
    internal static (string RepositoryRoot, string AnalyzerPath) Validate(
        string repoRoot,
        AnalyzerAttachmentRequirement requirement,
        IGuardedFeatureDetector detector)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        ArgumentNullException.ThrowIfNull(detector);
        var normalizedRoot = Path.GetFullPath(repoRoot);
        if (!Directory.Exists(normalizedRoot))
        {
            throw new DirectoryNotFoundException(
                "repository root が存在しない: " + normalizedRoot);
        }
        if (Path.IsPathRooted(requirement.AnalyzerProjectPath))
        {
            throw new ArgumentException(
                "analyzer project path は repository 内の相対 path であること",
                nameof(requirement));
        }
        var analyzerPath = Path.GetFullPath(
            Path.Combine(normalizedRoot, requirement.AnalyzerProjectPath));
        if (IsOutsideRepository(normalizedRoot, analyzerPath))
        {
            throw new ArgumentException(
                "analyzer project path は repository 内の相対 path であること",
                nameof(requirement));
        }
        if (!File.Exists(analyzerPath))
            throw new FileNotFoundException("analyzer project が存在しない", analyzerPath);
        return (normalizedRoot, analyzerPath);
    }

    /// <summary>repository 外 path の判定</summary>
    /// <param name="repoRoot">repository root の絶対 path</param>
    /// <param name="path">判定対象の絶対 path</param>
    /// <returns>repository 外の場合に true</returns>
    private static bool IsOutsideRepository(string repoRoot, string path)
    {
        var relative = Path.GetRelativePath(repoRoot, path);
        return string.Equals(relative, "..", StringComparison.Ordinal) ||
               relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
               relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }
}
