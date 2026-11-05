using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace TypeModeling.Analyzers.Exhaustiveness;

/// <summary>arm を足した switch 式に compiler が出した診断</summary>
/// <param name="Probe">arm を足した試験の switch 式</param>
/// <param name="Diagnostics">試験の switch 式の範囲の診断</param>
/// <param name="FillerCount">元の arm より前に足した arm の数</param>
internal sealed record ProbeOutcome(
    SwitchExpressionSyntax Probe,
    ImmutableArray<Diagnostic> Diagnostics,
    int FillerCount)
{
    /// <summary>網羅していない switch 式の compiler 診断の ID</summary>
    internal static ImmutableHashSet<string> NotExhaustiveIds { get; } =
        ImmutableHashSet.Create("CS8509", "CS8524", "CS8846");

    /// <summary>到達できない arm の compiler 診断の ID</summary>
    private const string UnreachableArmId = "CS8510";

    /// <summary>試験の switch 式が網羅しているかの判定</summary>
    /// <remarks>足した arm が compile できない場合、compiler は網羅を判定しないことがあるので網羅とみなさない</remarks>
    internal bool IsExhaustive => !Diagnostics.Any(diagnostic =>
        (NotExhaustiveIds.Contains(diagnostic.Id) && diagnostic.Location.SourceSpan.Start == Probe.SwitchKeyword.SpanStart) ||
        IsFillerError(diagnostic));

    /// <summary>元の arm の位置ごとの到達の可否</summary>
    /// <param name="originalArmIndex">元の switch 式での arm の位置</param>
    /// <returns>試験で到達できないと判定された場合に true</returns>
    internal bool IsUnreachable(int originalArmIndex)
    {
        var pattern = Probe.Arms[FillerCount + originalArmIndex].Pattern;
        return Diagnostics.Any(diagnostic =>
            diagnostic.Id == UnreachableArmId && pattern.Span.Contains(diagnostic.Location.SourceSpan));
    }

    /// <summary>足した arm の中のエラーかの判定</summary>
    /// <param name="diagnostic">試験の switch 式の範囲の診断</param>
    /// <returns>到達できないこと以外の理由で足した arm がエラーになる場合に true</returns>
    private bool IsFillerError(Diagnostic diagnostic) =>
        diagnostic.Severity == DiagnosticSeverity.Error &&
        diagnostic.Id != UnreachableArmId &&
        TextSpan.FromBounds(Probe.Arms[0].SpanStart, Probe.Arms[FillerCount - 1].Span.End).Contains(diagnostic.Location.SourceSpan);
}
