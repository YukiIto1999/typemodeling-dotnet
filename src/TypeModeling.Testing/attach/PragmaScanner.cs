using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TypeModeling.Testing.Attach;

/// <summary>active pragma warning の診断保護検査器</summary>
internal static class PragmaScanner
{
    /// <summary>restore のない pragma disable 違反追加</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="evaluation">評価済み project 構成</param>
    /// <param name="matcher">保護対象診断の照合器</param>
    /// <param name="violations">違反の追加先</param>
    internal static void AddViolations(
        string repoRoot,
        EvaluatedProjectConfiguration evaluation,
        DiagnosticMatcher matcher,
        ISet<string> violations)
    {
        foreach (var sourcePath in evaluation.CompilePaths)
        {
            var syntax = CSharpSyntaxTree.ParseText(
                File.ReadAllText(sourcePath),
                evaluation.ParseOptions,
                sourcePath).GetRoot();
            var disabledIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var disablesAllWarnings = false;
            foreach (var directive in syntax.DescendantTrivia(descendIntoTrivia: true)
                         .Select(trivia => trivia.GetStructure())
                         .OfType<PragmaWarningDirectiveTriviaSyntax>()
                         .Where(directive => directive.IsActive))
            {
                ApplyDirective(
                    directive,
                    matcher,
                    disabledIds,
                    ref disablesAllWarnings);
            }

            var relativeSource = Path.GetRelativePath(repoRoot, sourcePath).Replace('\\', '/');
            if (disablesAllWarnings)
            {
                violations.Add(
                    evaluation.Label + ": " + relativeSource +
                    " に restore のない全 warning 対象 #pragma warning disable がある");
            }
            foreach (var diagnosticId in disabledIds.Order(StringComparer.OrdinalIgnoreCase))
            {
                violations.Add(
                    evaluation.Label + ": " + relativeSource +
                    " に restore のない #pragma warning disable " + diagnosticId + " がある");
            }
        }
    }

    /// <summary>pragma warning directive を無効化状態へ反映</summary>
    /// <param name="directive">反映対象の directive</param>
    /// <param name="matcher">保護対象診断の照合器</param>
    /// <param name="disabledIds">無効化中の診断 ID</param>
    /// <param name="disablesAllWarnings">全 warning の無効化状態</param>
    private static void ApplyDirective(
        PragmaWarningDirectiveTriviaSyntax directive,
        DiagnosticMatcher matcher,
        HashSet<string> disabledIds,
        ref bool disablesAllWarnings)
    {
        var isDisable = directive.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword);
        var warningIds = WarningIds(directive, isDisable);
        if (warningIds.Length == 0)
        {
            disablesAllWarnings = isDisable;
            if (!isDisable)
                disabledIds.Clear();
            return;
        }

        foreach (var diagnosticId in warningIds.Where(matcher.Matches))
        {
            if (isDisable)
                disabledIds.Add(diagnosticId);
            else
                disabledIds.Remove(diagnosticId);
        }
    }

    /// <summary>directive に指定された診断 ID の抽出</summary>
    /// <param name="directive">解析対象の pragma warning directive</param>
    /// <param name="isDisable">disable directive の判定</param>
    /// <returns>指定された診断 ID 集合</returns>
    private static string[] WarningIds(
        PragmaWarningDirectiveTriviaSyntax directive,
        bool isDisable)
    {
        var marker = isDisable ? "disable" : "restore";
        var source = directive.ToString();
        var markerIndex = source.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return [];
        var values = source[(markerIndex + marker.Length)..];
        var commentIndex = values.IndexOf("//", StringComparison.Ordinal);
        if (commentIndex >= 0)
            values = values[..commentIndex];
        return values.Split(
            [',', ' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
