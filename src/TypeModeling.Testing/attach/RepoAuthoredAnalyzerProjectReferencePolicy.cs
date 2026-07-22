using System.Xml.Linq;

namespace TypeModeling.Testing.Attach;

/// <summary>companion analyzer の無条件 analyzer-only 接続を強制する構造検査</summary>
internal static class RepoAuthoredAnalyzerProjectReferencePolicy
{
    /// <summary>repository authored analyzer ProjectReference 違反収集</summary>
    /// <param name="authoredFiles">repository authored MSBuild file 集合</param>
    /// <param name="authoredDocuments">file path 別の MSBuild XML document</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="analyzerPath">analyzer project の絶対 path</param>
    /// <param name="violations">違反の追加先</param>
    internal static void AddViolations(
        IReadOnlyList<RepoAuthoredMsBuildFile> authoredFiles,
        IReadOnlyDictionary<string, XDocument> authoredDocuments,
        string projectPath,
        string analyzerPath,
        ISet<string> violations)
    {
        var analyzerIncludes = new List<(XElement Reference, bool IsConditionallyImported)>();
        foreach (var authoredFile in authoredFiles)
        {
            var document = authoredDocuments[authoredFile.Path];
            foreach (var reference in document.Descendants()
                         .Where(element => element.Name.LocalName == "ProjectReference"))
            {
                InspectReference(
                    reference,
                    authoredFile.IsConditionallyImported,
                    projectPath,
                    analyzerPath,
                    analyzerIncludes,
                    violations);
            }
        }

        if (!HasUnconditionalAnalyzerOnlyInclude(analyzerIncludes))
        {
            violations.Add(
                "MSBuild 構造: repo-authored graph に無条件 analyzer-only ProjectReference Include がない");
        }
    }

    /// <summary>ProjectReference の analyzer 接続要件検証</summary>
    /// <param name="reference">検証対象 ProjectReference</param>
    /// <param name="isConditionallyImported">条件付き Import 配下か</param>
    /// <param name="projectPath">参照元 project path</param>
    /// <param name="analyzerPath">companion analyzer project path</param>
    /// <param name="analyzerIncludes">検出済み analyzer Include</param>
    /// <param name="violations">違反の格納先</param>
    private static void InspectReference(
        XElement reference,
        bool isConditionallyImported,
        string projectPath,
        string analyzerPath,
        List<(XElement Reference, bool IsConditionallyImported)> analyzerIncludes,
        ISet<string> violations)
    {
        var operation = AnalyzerProjectReferenceMatcher.ProjectReferenceOperation(reference);
        if (operation is null)
            return;

        var itemSpec = reference.Attribute(operation)?.Value ?? "";
        if (!AnalyzerProjectReferenceMatcher.PotentiallyTargetsAnalyzer(
                itemSpec,
                projectPath,
                analyzerPath,
                operation is "Remove" or "Update"))
        {
            return;
        }

        if (operation != "Include")
        {
            violations.Add(
                "MSBuild 構造: companion analyzer を変更できる ProjectReference " +
                operation + " がある");
            return;
        }

        analyzerIncludes.Add((reference, isConditionallyImported));
        if (isConditionallyImported)
        {
            violations.Add(
                "MSBuild 構造: 条件付き Import 配下の companion analyzer ProjectReference Include がある");
        }
        if (RepoAuthoredMsBuildElement.HasCondition(reference) ||
            RepoAuthoredMsBuildElement.IsInsideTarget(reference))
        {
            violations.Add(
                "MSBuild 構造: 条件付きまたは target-time の companion analyzer ProjectReference Include がある");
        }
    }

    /// <summary>無条件 analyzer-only Include の存在判定</summary>
    /// <param name="analyzerIncludes">検出済み analyzer Include</param>
    /// <returns>要件を満たす Include があれば true</returns>
    private static bool HasUnconditionalAnalyzerOnlyInclude(
        IEnumerable<(XElement Reference, bool IsConditionallyImported)> analyzerIncludes) =>
        analyzerIncludes.Any(candidate =>
            !candidate.IsConditionallyImported &&
            !RepoAuthoredMsBuildElement.HasCondition(candidate.Reference) &&
            !RepoAuthoredMsBuildElement.IsInsideTarget(candidate.Reference) &&
            string.Equals(
                AnalyzerProjectReferenceMatcher.ProjectReferenceMetadata(
                    candidate.Reference,
                    "OutputItemType"),
                "Analyzer",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                AnalyzerProjectReferenceMatcher.ProjectReferenceMetadata(
                    candidate.Reference,
                    "ReferenceOutputAssembly"),
                "false",
                StringComparison.OrdinalIgnoreCase));
}
