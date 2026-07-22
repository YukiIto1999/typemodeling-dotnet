using System.Xml.Linq;

namespace TypeModeling.Testing.Attach;

/// <summary>repository authored graph の analyzer attachment policy</summary>
internal static class RepoAuthoredAnalyzerPolicy
{
    /// <summary>repository authored graph の analyzer attachment 違反収集</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="analyzerPath">analyzer project の絶対 path</param>
    /// <param name="matcher">保護対象診断の照合器</param>
    /// <param name="violations">違反の追加先</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>repository authored 違反追加の非同期完了</returns>
    internal static async Task AddViolationsAsync(
        string repoRoot,
        string projectPath,
        string analyzerPath,
        DiagnosticMatcher matcher,
        ISet<string> violations,
        CancellationToken cancellationToken)
    {
        var authoredFiles = await RepoAuthoredMsBuildGraphTraversal.FilesAsync(
                repoRoot,
                projectPath,
                cancellationToken)
            .ConfigureAwait(false);
        var authoredDocuments = authoredFiles.ToDictionary(
            file => file.Path,
            file => XDocument.Load(file.Path, LoadOptions.PreserveWhitespace),
            StringComparer.Ordinal);
        var propertyValues = new Dictionary<string, List<RepoAuthoredPropertyValue>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var authoredPath in authoredFiles.Select(authoredFile => authoredFile.Path))
        {
            RepoAuthoredPropertyCatalog.AddValues(
                authoredDocuments[authoredPath],
                authoredPath,
                propertyValues);
        }

        RepoAuthoredAnalyzerSuppressionPolicy.AddViolations(
            authoredFiles,
            authoredDocuments,
            propertyValues,
            matcher,
            violations);
        RepoAuthoredAnalyzerExecutionPolicy.AddViolations(
            authoredFiles,
            authoredDocuments,
            propertyValues,
            violations);
        RepoAuthoredAnalyzerProjectReferencePolicy.AddViolations(
            authoredFiles,
            authoredDocuments,
            projectPath,
            analyzerPath,
            violations);
    }
}
