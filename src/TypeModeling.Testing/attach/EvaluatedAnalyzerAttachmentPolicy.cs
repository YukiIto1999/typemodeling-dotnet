namespace TypeModeling.Testing.Attach;

/// <summary>評価済み構成の analyzer attachment policy</summary>
internal static class EvaluatedAnalyzerAttachmentPolicy
{
    /// <summary>評価済み構成の analyzer attachment 違反収集</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="analyzerPath">analyzer project の絶対 path</param>
    /// <param name="evaluation">評価済み project 構成</param>
    /// <param name="matcher">保護対象診断の照合器</param>
    /// <param name="violations">違反の追加先</param>
    internal static void AddViolations(
        string repoRoot,
        string analyzerPath,
        EvaluatedProjectConfiguration evaluation,
        DiagnosticMatcher matcher,
        ISet<string> violations)
    {
        var analyzerReference = evaluation.ProjectReferences.FirstOrDefault(reference =>
            AnalyzerProjectReferenceMatcher.PathsEqual(reference.FullPath, analyzerPath));
        if (analyzerReference is null ||
            !string.Equals(
                analyzerReference.OutputItemType,
                "Analyzer",
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                analyzerReference.ReferenceOutputAssembly,
                "false",
                StringComparison.OrdinalIgnoreCase))
        {
            violations.Add(
                evaluation.Label + ": companion analyzer ProjectReference が有効でない");
        }

        foreach (var diagnosticId in MsBuildValueSplitter.SplitList(evaluation.NoWarn)
                     .Where(matcher.Matches))
        {
            violations.Add(
                evaluation.Label + ": NoWarn が " + diagnosticId + " を抑止している");
        }
        if (RepoAuthoredAnalyzerPropertyResolver.IsFalse(evaluation.RunAnalyzers))
        {
            violations.Add(
                evaluation.Label +
                ": RunAnalyzers が false のため analyzer が実行されない");
        }
        else if (string.IsNullOrWhiteSpace(evaluation.RunAnalyzers) &&
                 RepoAuthoredAnalyzerPropertyResolver.IsFalse(
                     evaluation.RunAnalyzersDuringBuild))
        {
            violations.Add(
                evaluation.Label +
                ": RunAnalyzersDuringBuild が false のため analyzer が実行されない");
        }
        EditorConfigScanner.AddViolations(repoRoot, evaluation, matcher, violations);
        PragmaScanner.AddViolations(repoRoot, evaluation, matcher, violations);
    }
}
