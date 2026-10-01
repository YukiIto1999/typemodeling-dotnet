using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace TypeModeling.Testing.Attach;

/// <summary>source 適用済み editorconfig の診断保護検査器</summary>
internal static class EditorConfigScanner
{
    /// <summary>診断 severity 低下の違反追加</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="evaluation">評価済み project 構成</param>
    /// <param name="matcher">保護対象診断の照合器</param>
    /// <param name="violations">違反の追加先</param>
    [SuppressMessage("Critical Code Smell", "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "S3776 の導入前からある複雑度 22 の既存違反。基線台帳 S3776-005 に記録し、15 以下へ分割した時点で抑止を外す")]
    internal static void AddViolations(
        string repoRoot,
        EvaluatedProjectConfiguration evaluation,
        DiagnosticMatcher matcher,
        ISet<string> violations)
    {
        if (evaluation.EditorConfigPaths.Count == 0)
            return;

        var displays = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var analyzerConfigs = new List<AnalyzerConfig>(evaluation.EditorConfigPaths.Count);
        for (var index = 0; index < evaluation.EditorConfigPaths.Count; index++)
        {
            var path = evaluation.EditorConfigPaths[index];
            analyzerConfigs.Add(AnalyzerConfig.Parse(
                NormalizeWildcardKeys(File.ReadAllText(path), matcher, displays, index),
                path));
        }
        var configSet = AnalyzerConfigSet.Create(analyzerConfigs);
        foreach (var sourcePath in evaluation.CompilePaths)
        {
            var options = configSet.GetOptionsForSourcePath(sourcePath);
            foreach (var option in options.TreeOptions)
            {
                var diagnosticId = displays.TryGetValue(option.Key, out var display)
                    ? display
                    : option.Key.ToUpperInvariant();
                if (!matcher.Matches(diagnosticId) ||
                    option.Value is not (
                        ReportDiagnostic.Suppress or
                        ReportDiagnostic.Hidden or
                        ReportDiagnostic.Info))
                {
                    continue;
                }

                violations.Add(ViolationMessage(
                    repoRoot,
                    evaluation,
                    sourcePath,
                    diagnosticId,
                    Severity(option.Value)));
            }
            foreach (var option in options.AnalyzerOptions)
            {
                if (!IsProtectedBulkSeverityKey(option.Key, matcher) ||
                    !IsSuppressedSeverity(option.Value))
                    continue;
                var display = displays.TryGetValue(option.Key, out var originalKey)
                    ? originalKey
                    : option.Key;
                violations.Add(ViolationMessage(
                    repoRoot,
                    evaluation,
                    sourcePath,
                    display,
                    option.Value.ToLowerInvariant()));
            }
        }
    }

    /// <summary>wildcard 診断 key の解析用正規化</summary>
    /// <param name="source">editorconfig の内容</param>
    /// <param name="matcher">保護対象診断の照合器</param>
    /// <param name="displays">正規化前 key の記録先</param>
    /// <param name="fileIndex">editorconfig の識別番号</param>
    /// <returns>wildcard 診断 key を正規化した内容</returns>
    private static string NormalizeWildcardKeys(
        string source,
        DiagnosticMatcher matcher,
        Dictionary<string, string> displays,
        int fileIndex)
    {
        var lines = source.Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            var trimmedLine = line.TrimStart();
            var equalsIndex = trimmedLine.IndexOf('=');
            if (equalsIndex < 0)
                continue;
            var key = trimmedLine[..equalsIndex].TrimEnd();
            if (IsProtectedBulkSeverityKey(key, matcher))
                displays[key] = key;
            const string prefix = "dotnet_diagnostic.";
            const string suffix = ".severity";
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var diagnostic = key[prefix.Length..^suffix.Length];
            if (!diagnostic.EndsWith('*') || !matcher.Matches(diagnostic))
                continue;
            var sentinel = "TYPEMODELINGWILDCARD" + fileIndex + "X" + (lineIndex + 1);
            displays[sentinel] = diagnostic;
            var indentationLength = line.Length - trimmedLine.Length;
            lines[lineIndex] = line[..indentationLength] + prefix + sentinel + suffix +
                trimmedLine[key.Length..];
        }

        return string.Join('\n', lines);
    }

    /// <summary>editorconfig 違反メッセージの生成</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="evaluation">評価済み project 構成</param>
    /// <param name="sourcePath">違反が適用される source path</param>
    /// <param name="key">違反した editorconfig key</param>
    /// <param name="severity">違反した severity</param>
    /// <returns>editorconfig 違反メッセージ</returns>
    private static string ViolationMessage(
        string repoRoot,
        EvaluatedProjectConfiguration evaluation,
        string sourcePath,
        string key,
        string severity) =>
        evaluation.Label + ": " +
        Path.GetRelativePath(repoRoot, sourcePath).Replace('\\', '/') +
        " に適用される .editorconfig が " + key +
        " を " + severity + " にしている";

    /// <summary>保護対象の一括 severity key 判定</summary>
    /// <param name="key">editorconfig key</param>
    /// <param name="matcher">保護対象診断の照合器</param>
    /// <returns>保護対象の一括 severity key の場合に true</returns>
    private static bool IsProtectedBulkSeverityKey(
        string key,
        DiagnosticMatcher matcher)
    {
        const string globalKey = "dotnet_analyzer_diagnostic.severity";
        if (string.Equals(key, globalKey, StringComparison.OrdinalIgnoreCase))
            return true;

        const string categoryPrefix = "dotnet_analyzer_diagnostic.category-";
        const string severitySuffix = ".severity";
        if (!key.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase) ||
            !key.EndsWith(severitySuffix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var category = key[categoryPrefix.Length..^severitySuffix.Length];
        return category.Length > 0 && matcher.MatchesCategory(category);
    }

    /// <summary>抑止に相当する severity の判定</summary>
    /// <param name="severity">editorconfig の severity</param>
    /// <returns>抑止に相当する場合に true</returns>
    private static bool IsSuppressedSeverity(string severity) =>
        severity.Equals("none", StringComparison.OrdinalIgnoreCase) ||
        severity.Equals("silent", StringComparison.OrdinalIgnoreCase) ||
        severity.Equals("suggestion", StringComparison.OrdinalIgnoreCase);

    /// <summary>診断報告区分の editorconfig severity 変換</summary>
    /// <param name="severity">診断報告区分</param>
    /// <returns>editorconfig の severity</returns>
    private static string Severity(ReportDiagnostic severity) => severity switch
    {
        ReportDiagnostic.Suppress => "none",
        ReportDiagnostic.Hidden => "silent",
        ReportDiagnostic.Info => "suggestion",
        _ => severity.ToString(),
    };
}
