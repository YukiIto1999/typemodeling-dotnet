using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TypeModeling.Analyzers.Diagnostics.Guard;

/// <summary>file の終わりまで restore されない #pragma warning disable の検出</summary>
/// <remarks>同じ file の中で disable と restore を対にした局所の抑止は許す</remarks>
internal static class PragmaGuard
{
    /// <summary>restore のない disable の報告</summary>
    /// <param name="tree">検査する syntax tree</param>
    /// <param name="report">位置と message を受け取る処理</param>
    /// <param name="cancellationToken">中止 token</param>
    internal static void Scan(
        SyntaxTree tree,
        Action<Location, string> report,
        CancellationToken cancellationToken
    )
    {
        var disabled = new Dictionary<string, Location>(StringComparer.OrdinalIgnoreCase);
        Location? all = null;
        var root = tree.GetCompilationUnitRoot(cancellationToken);
        for (
            var directive = root.GetFirstDirective();
            directive is not null;
            directive = directive.GetNextDirective()
        )
        {
            if (directive is PragmaWarningDirectiveTriviaSyntax { IsActive: true } pragma)
                Apply(pragma, disabled, ref all);
        }

        var file = Path.GetFileName(tree.FilePath);
        foreach (var pair in disabled.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            report(
                pair.Value,
                $"{file} の #pragma warning disable が '{pair.Key}' を restore なしに無効にしている"
            );
        if (all is not null)
            report(
                all,
                $"{file} の #pragma warning disable に restore が無く、全ての診断が無効になる"
            );
    }

    /// <summary>directive を無効にしている診断の状態へ反映する</summary>
    /// <param name="pragma">反映する directive</param>
    /// <param name="disabled">無効にしている TypeModeling の診断と、その directive の位置</param>
    /// <param name="all">全ての診断を無効にしている directive の位置</param>
    private static void Apply(
        PragmaWarningDirectiveTriviaSyntax pragma,
        Dictionary<string, Location> disabled,
        ref Location? all
    )
    {
        var isDisable = pragma.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword);
        var ids = Ids(pragma, isDisable);
        if (ids.Length == 0)
        {
            all = isDisable ? pragma.GetLocation() : null;
            if (!isDisable)
                disabled.Clear();
            return;
        }

        foreach (var id in ids.Where(GuardedDiagnostics.IsGuarded))
        {
            if (isDisable)
                disabled[id] = pragma.GetLocation();
            else
                disabled.Remove(id);
        }
    }

    /// <summary>directive が指定する診断の ID の取り出し</summary>
    /// <param name="pragma">取り出す directive</param>
    /// <param name="isDisable">disable の directive の場合に true</param>
    /// <returns>指定された ID。全ての診断が対象なら空</returns>
    private static string[] Ids(PragmaWarningDirectiveTriviaSyntax pragma, bool isDisable)
    {
        var keyword = isDisable ? "disable" : "restore";
        var text = pragma.ToString();
        var at = text.IndexOf(keyword, StringComparison.Ordinal);
        if (at < 0)
            return [];

        var values = text.Substring(at + keyword.Length);
        var comment = values.IndexOf("//", StringComparison.Ordinal);
        if (comment >= 0)
            values = values.Substring(0, comment);
        return values.Split([',', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }
}
