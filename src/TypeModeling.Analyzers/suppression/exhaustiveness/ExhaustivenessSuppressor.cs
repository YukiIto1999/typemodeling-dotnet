using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers.Exhaustiveness;
using RoslynSuppression = Microsoft.CodeAnalysis.Diagnostics.Suppression;

namespace TypeModeling.Analyzers.Suppression.Exhaustiveness;

/// <summary>閉じた型を C# と同じく網羅として扱う switch 式の診断の抑止</summary>
/// <remarks>存在しない値を覆う arm を足した switch 式を compiler が網羅と判定した場合だけ、元の診断を抑止する</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExhaustivenessSuppressor : DiagnosticSuppressor
{
    /// <summary>網羅していない switch 式の抑止規則</summary>
    private static readonly SuppressionDescriptor NotExhaustiveRule = new(
        id: "TYPMOD101",
        suppressedDiagnosticId: "CS8509",
        justification: "閉じた型の全ての値を扱う switch 式は網羅である。");

    /// <summary>名前のない enum 値を扱わない switch 式の抑止規則</summary>
    private static readonly SuppressionDescriptor UnnamedEnumValueRule = new(
        id: "TYPMOD102",
        suppressedDiagnosticId: "CS8524",
        justification: "閉じた enum は名前のない値を取らないので、全ての member を扱う switch 式は網羅である。");

    /// <summary>when 付きの arm だけが残りを扱う switch 式の抑止規則</summary>
    private static readonly SuppressionDescriptor NotExhaustiveWithWhenRule = new(
        id: "TYPMOD103",
        suppressedDiagnosticId: "CS8846",
        justification: "閉じた型の全ての値を when のない arm が扱う switch 式は網羅である。");

    /// <summary>抑止する診断の ID から抑止規則への対応</summary>
    private static readonly ImmutableDictionary<string, SuppressionDescriptor> RulesByDiagnosticId =
        new[] { NotExhaustiveRule, UnnamedEnumValueRule, NotExhaustiveWithWhenRule }
            .ToImmutableDictionary(rule => rule.SuppressedDiagnosticId);

    /// <summary>対応する抑止規則</summary>
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } =
        [NotExhaustiveRule, UnnamedEnumValueRule, NotExhaustiveWithWhenRule];

    /// <summary>閉じた型を網羅した switch 式の診断の抑止</summary>
    /// <param name="context">抑止解析 context</param>
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        var byTree = context.ReportedDiagnostics
            .Where(diagnostic => RulesByDiagnosticId.ContainsKey(diagnostic.Id))
            .GroupBy(diagnostic => diagnostic.Location.SourceTree!);
        foreach (var group in byTree)
        {
            var root = group.Key.GetRoot(context.CancellationToken);
            var targets = group
                .Select(diagnostic => (Diagnostic: diagnostic, Switch: SwitchAt(root, diagnostic)))
                .Where(target => target.Switch is not null)
                .ToImmutableArray();
            var model = context.GetSemanticModel(group.Key);
            var plans = ExhaustivenessPlans.For(targets.Select(target => target.Switch!), model, context.CancellationToken);
            var outcomes = ExhaustivenessProbe.Run(context.Compilation, group.Key, plans, context.CancellationToken);
            foreach (var (diagnostic, switchExpression) in targets)
            {
                if (outcomes.TryGetValue(switchExpression!, out var outcome) && outcome.IsExhaustive)
                    context.ReportSuppression(RoslynSuppression.Create(RulesByDiagnosticId[diagnostic.Id], diagnostic));
            }
        }
    }

    /// <summary>診断が switch keyword に付いた switch 式</summary>
    /// <param name="root">診断のある tree の root</param>
    /// <param name="diagnostic">網羅に関する compiler の診断</param>
    /// <returns>診断の付いた switch 式。switch 文の診断なら null</returns>
    private static SwitchExpressionSyntax? SwitchAt(SyntaxNode root, Diagnostic diagnostic) =>
        root.FindToken(diagnostic.Location.SourceSpan.Start).Parent as SwitchExpressionSyntax;
}
