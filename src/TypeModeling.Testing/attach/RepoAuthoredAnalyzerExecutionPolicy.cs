using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;

namespace TypeModeling.Testing.Attach;

/// <summary>companion analyzer の build 実行停止を拒否する構造検査</summary>
internal static class RepoAuthoredAnalyzerExecutionPolicy
{
    /// <summary>repository authored analyzer 実行違反収集</summary>
    /// <param name="authoredFiles">repository authored MSBuild file 集合</param>
    /// <param name="authoredDocuments">file path 別の MSBuild XML document</param>
    /// <param name="propertyValues">property 名別の値集合</param>
    /// <param name="violations">違反の追加先</param>
    [SuppressMessage("Critical Code Smell", "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "S3776 の導入前からある複雑度 17 の既存違反。基線台帳 S3776-007 に記録し、15 以下へ分割した時点で抑止を外す")]
    internal static void AddViolations(
        IReadOnlyList<RepoAuthoredMsBuildFile> authoredFiles,
        IReadOnlyDictionary<string, XDocument> authoredDocuments,
        IReadOnlyDictionary<string, List<RepoAuthoredPropertyValue>> propertyValues,
        ISet<string> violations)
    {
        var authoredPropertyAssignments = new Dictionary<string, List<string>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var property in authoredDocuments.Values.SelectMany(document =>
                     document.Descendants().Where(element =>
                         element.Parent?.Name.LocalName == "PropertyGroup")))
        {
            if (!authoredPropertyAssignments.TryGetValue(
                    property.Name.LocalName,
                    out var assignments))
            {
                assignments = [];
                authoredPropertyAssignments[property.Name.LocalName] = assignments;
            }
            assignments.Add(property.Value);
        }
        var runAnalyzersAssignments = authoredFiles.SelectMany(authoredFile =>
                authoredDocuments[authoredFile.Path].Descendants()
                    .Where(element => element.Name.LocalName == "RunAnalyzers" &&
                                      element.Parent?.Name.LocalName == "PropertyGroup")
                    .Select(element => (File: authoredFile, Element: element)))
            .ToArray();
        var hasUnconditionalRunAnalyzersTrue = runAnalyzersAssignments.Any(assignment =>
                !assignment.File.IsConditionallyImported &&
                RepoAuthoredAnalyzerPropertyResolver.ResolvesOnlyToTrue(
                    assignment.Element.Value,
                    authoredPropertyAssignments) &&
                !RepoAuthoredMsBuildElement.HasCondition(assignment.Element) &&
                !RepoAuthoredMsBuildElement.IsInsideTarget(assignment.Element)) &&
            runAnalyzersAssignments.All(assignment =>
                RepoAuthoredAnalyzerPropertyResolver.ResolvesOnlyToTrue(
                    assignment.Element.Value,
                    authoredPropertyAssignments));

        foreach (var authoredPath in authoredFiles.Select(authoredFile => authoredFile.Path))
        {
            var document = authoredDocuments[authoredPath];
            foreach (var propertyName in new[] { "RunAnalyzers", "RunAnalyzersDuringBuild" })
            {
                if (propertyName == "RunAnalyzersDuringBuild" &&
                    hasUnconditionalRunAnalyzersTrue)
                {
                    continue;
                }
                foreach (var property in document.Descendants()
                             .Where(element =>
                                 element.Name.LocalName == propertyName &&
                                 element.Parent?.Name.LocalName == "PropertyGroup" &&
                                 RepoAuthoredAnalyzerPropertyResolver.CanResolveFalse(
                                     element.Value,
                                     propertyValues)))
                {
                    violations.Add(
                        "MSBuild 構造: " + Path.GetFileName(authoredPath) +
                        " の " + propertyName + " が false になり得る");
                }
            }
        }
    }
}
