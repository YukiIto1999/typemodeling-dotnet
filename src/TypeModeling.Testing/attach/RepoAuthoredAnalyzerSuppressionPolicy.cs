using System.Xml.Linq;

namespace TypeModeling.Testing.Attach;

/// <summary>repository authored NoWarn policy</summary>
internal static class RepoAuthoredAnalyzerSuppressionPolicy
{
    /// <summary>repository authored NoWarn 違反収集</summary>
    /// <param name="authoredFiles">repository authored MSBuild file 集合</param>
    /// <param name="authoredDocuments">file path 別の MSBuild XML document</param>
    /// <param name="propertyValues">property 名別の値集合</param>
    /// <param name="matcher">保護対象診断の照合器</param>
    /// <param name="violations">違反の追加先</param>
    internal static void AddViolations(
        IReadOnlyList<RepoAuthoredMsBuildFile> authoredFiles,
        IReadOnlyDictionary<string, XDocument> authoredDocuments,
        IReadOnlyDictionary<string, List<RepoAuthoredPropertyValue>> propertyValues,
        DiagnosticMatcher matcher,
        ISet<string> violations)
    {
        foreach (var authoredPath in authoredFiles.Select(authoredFile => authoredFile.Path))
        {
            foreach (var noWarn in authoredDocuments[authoredPath].Descendants()
                         .Where(element => element.Name.LocalName == "NoWarn"))
            {
                foreach (var diagnosticId in
                         RepoAuthoredAnalyzerPropertyResolver.DiagnosticsInMsBuildValue(
                             noWarn.Value,
                             propertyValues,
                             matcher))
                {
                    violations.Add(
                        "MSBuild 構造: " + Path.GetFileName(authoredPath) +
                        " の NoWarn が " + diagnosticId + " を条件にかかわらず抑止できる");
                }
            }
        }
    }
}
