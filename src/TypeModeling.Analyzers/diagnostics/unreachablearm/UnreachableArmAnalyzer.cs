using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers.Exhaustiveness;

namespace TypeModeling.Analyzers.Diagnostics.UnreachableArm;

/// <summary>閉じた型の網羅によって到達できなくなった switch 式の arm の検出</summary>
/// <remarks>C# 15 が閉じた型の全ての派生の後の基底型の arm に出す CS8510 と同じ判定</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnreachableArmAnalyzer : DiagnosticAnalyzer
{
    /// <summary>compiler 自身が出す到達できない arm の診断の ID</summary>
    private const string CompilerUnreachableArmId = "CS8510";

    /// <summary>閉じた型の網羅で到達できない arm の診断</summary>
    private static readonly DiagnosticDescriptor Rule = new(
        id: "TYPMOD006",
        title: "閉じた型の網羅で到達できない arm",
        messageFormat: "前の arm が閉じた型の全ての値を扱うので、この arm には到達できない。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>対応する診断規則</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <summary>tree ごとの解析の登録</summary>
    /// <param name="context">解析登録 context</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSemanticModelAction(Analyze);
    }

    /// <summary>tree の switch 式の到達できない arm の報告</summary>
    /// <param name="context">semantic model 解析 context</param>
    private static void Analyze(SemanticModelAnalysisContext context)
    {
        var model = context.SemanticModel;
        var switchExpressions = model.SyntaxTree.GetRoot(context.CancellationToken)
            .DescendantNodes()
            .OfType<SwitchExpressionSyntax>();
        var plans = ExhaustivenessPlans.For(switchExpressions, model, context.CancellationToken);
        var outcomes = ExhaustivenessProbe.Run(model.Compilation, model.SyntaxTree, plans, context.CancellationToken);
        foreach (var (switchExpression, outcome) in outcomes.Select(pair => (pair.Key, pair.Value)))
        {
            for (var index = 0; index < switchExpression.Arms.Count; index++)
            {
                var pattern = switchExpression.Arms[index].Pattern;
                if (outcome.IsUnreachable(index) && !IsUnreachableWithoutClosedTypes(model, pattern, context))
                    context.ReportDiagnostic(Diagnostic.Create(Rule, pattern.GetLocation()));
            }
        }
    }

    /// <summary>閉じた型を考えなくても compiler が到達できないと判定する arm かの判定</summary>
    /// <param name="model">元の semantic model</param>
    /// <param name="pattern">判定する arm の pattern</param>
    /// <param name="context">semantic model 解析 context</param>
    /// <returns>compiler が既に CS8510 を出している場合に true</returns>
    private static bool IsUnreachableWithoutClosedTypes(
        SemanticModel model,
        PatternSyntax pattern,
        SemanticModelAnalysisContext context) =>
        model.GetDiagnostics(pattern.Span, context.CancellationToken)
            .Any(diagnostic => diagnostic.Id == CompilerUnreachableArmId);
}
