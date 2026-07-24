using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers.Facts;
using RoslynSuppression = Microsoft.CodeAnalysis.Diagnostics.Suppression;

namespace TypeModeling.Analyzers.Suppression.ClosedUnionExhaustiveness;

/// <summary>閉じた型集合に対する switch 式診断の抑止器</summary>
/// <remarks>全てを扱う switch 式に限る抑止による網羅の機械保証</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClosedUnionExhaustivenessSuppressor : DiagnosticSuppressor
{
    /// <summary>網羅できない switch 式の diagnostic ID</summary>
    private const string SwitchExpressionNotExhaustiveId = "CS8509";

    /// <summary>unnamed enum 値を扱わない switch 式の diagnostic ID</summary>
    private const string SwitchExpressionUnnamedEnumValueId = "CS8524";

    /// <summary>全 variant を扱う switch 式の抑止規則</summary>
    private static readonly SuppressionDescriptor SwitchExpressionRule = new(
        id: "TCSSUP001",
        suppressedDiagnosticId: SwitchExpressionNotExhaustiveId,
        justification: "閉じた判別共用体の全 variant を扱う switch 式は網羅である。");

    /// <summary>全 named member を扱う enum switch 式の抑止規則</summary>
    private static readonly SuppressionDescriptor EnumSwitchExpressionRule = new(
        id: "TCSSUP002",
        suppressedDiagnosticId: SwitchExpressionUnnamedEnumValueId,
        justification: "[ClosedUnion] enum の全 named メンバを扱う switch 式は網羅である。");

    /// <summary>対応する抑止規則</summary>
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } =
        ImmutableArray.Create(SwitchExpressionRule, EnumSwitchExpressionRule);

    /// <summary>閉じた型集合に対する switch 式診断の抑止報告</summary>
    /// <param name="context">抑止解析 context</param>
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Id != SwitchExpressionNotExhaustiveId && diagnostic.Id != SwitchExpressionUnnamedEnumValueId)
                continue;

            var tree = diagnostic.Location.SourceTree;
            if (tree is null)
                continue;

            var root = tree.GetRoot(context.CancellationToken);
            var diagnosticNode = root.FindNode(diagnostic.Location.SourceSpan);
            var switchExpression = diagnosticNode.FirstAncestorOrSelf<SwitchExpressionSyntax>() ??
                diagnosticNode.DescendantNodesAndSelf().OfType<SwitchExpressionSyntax>().FirstOrDefault();
            if (switchExpression is null)
                continue;

            var model = context.GetSemanticModel(tree);
            var governingType = model
                .GetTypeInfo(switchExpression.GoverningExpression, context.CancellationToken)
                .Type;
            if (governingType is null || !ClosedTypeSet.IsClosed(governingType))
                continue;

            if (diagnostic.Id == SwitchExpressionNotExhaustiveId
                && ClosedVariantSwitchCoverage.CoversEveryVariant(
                    governingType,
                    switchExpression,
                    model,
                    context.CancellationToken))
            {
                context.ReportSuppression(RoslynSuppression.Create(SwitchExpressionRule, diagnostic));
            }
            else if (diagnostic.Id == SwitchExpressionUnnamedEnumValueId
                && ClosedEnumSwitchCoverage.CoversEveryNamedMember(
                    governingType,
                    switchExpression,
                    model,
                    context.CancellationToken))
            {
                context.ReportSuppression(RoslynSuppression.Create(EnumSwitchExpressionRule, diagnostic));
            }
        }
    }
}
