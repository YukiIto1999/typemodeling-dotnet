using System.Xml.Linq;

namespace TypeModeling.Testing.Attach;

/// <summary>未解決 import の保守的な候補発見</summary>
internal static class RepoAuthoredFallbackImportDiscovery
{
    /// <summary>未解決 import pattern に対応する file 集合</summary>
    /// <param name="authoredRoot">repository authored path の root</param>
    /// <param name="unresolvedPatterns">未解決 import pattern 集合</param>
    /// <returns>fallback 対象の MSBuild file 集合</returns>
    internal static IEnumerable<string> Candidates(
        string authoredRoot,
        IReadOnlyList<string> unresolvedPatterns)
    {
        var candidates = Directory.EnumerateFiles(authoredRoot, "*", SearchOption.AllDirectories)
            .Where(path => RepoAuthoredMsBuildPath.IsRepoAuthored(authoredRoot, path))
            .Select(Path.GetFullPath)
            .ToArray();
        foreach (var unresolvedPattern in unresolvedPatterns)
        {
            var normalized = unresolvedPattern.Replace('\\', '/');
            var filePattern = normalized[(normalized.LastIndexOf('/') + 1)..];
            var hasLiteralFilePattern = !filePattern.Contains("$(", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(filePattern);
            foreach (var candidate in candidates)
            {
                if (hasLiteralFilePattern &&
                    !System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(
                        filePattern,
                        Path.GetFileName(candidate),
                        ignoreCase: false))
                {
                    continue;
                }
                if (IsMsBuildDocument(candidate))
                    yield return candidate;
            }
        }
    }

    /// <summary>MSBuild XML document の判定</summary>
    /// <param name="path">検査対象 file path</param>
    /// <returns>MSBuild document の場合に true</returns>
    private static bool IsMsBuildDocument(string path)
    {
        try
        {
            return XDocument.Load(path).Root?.Name.LocalName == "Project";
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
