using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Exhaustiveness;

namespace TypeModeling.Analyzers.Diagnostics.ExplicitCase;

/// <summary>閉じた型への switch が、後から足す case を受け取る arm を持つことの検出</summary>
/// <remarks>
/// 既定では無効で、利用者の診断の設定で有効にしたときだけ、C# が許す catch-all の arm を拒否する。
/// 閉じた class、閉じた enum、union 型の Value への switch 式と switch 文が対象になる。
/// switch 式で compiler が網羅に数えない when 付きの arm は、足した case の欠落が compiler に報されるので拒否しない。
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExplicitCaseAnalyzer : DiagnosticAnalyzer
{
    /// <summary>後から足す case を受け取る arm の診断</summary>
    private static readonly DiagnosticDescriptor Rule = new(
        id: "TYPMOD021",
        title: "閉じた型の case を挙げない arm",
        messageFormat: "この pattern は、'{0}' に後から足す case を受け取るか、受け取らないと確かめられない。case を一つずつ挙げ、not や list pattern の内側に置かない。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: false);

    /// <summary>対応する診断規則</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <summary>operation 解析の登録</summary>
    /// <param name="context">解析登録 context</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(AnalyzeSwitchExpression, OperationKind.SwitchExpression);
        context.RegisterOperationAction(AnalyzeSwitch, OperationKind.Switch);
    }

    /// <summary>switch 式の arm の報告</summary>
    /// <param name="context">operation 解析 context</param>
    private static void AnalyzeSwitchExpression(OperationAnalysisContext context)
    {
        var switchExpression = (ISwitchExpressionOperation)context.Operation;
        Report(context, switchExpression.Value, [.. switchExpression.Arms.Select(ExpressionArm)]);
    }

    /// <summary>switch 文の case と default の label の報告</summary>
    /// <param name="context">operation 解析 context</param>
    private static void AnalyzeSwitch(OperationAnalysisContext context)
    {
        var switchStatement = (ISwitchOperation)context.Operation;
        Report(context, switchStatement.Value, [.. switchStatement.Cases.SelectMany(section => section.Clauses).Select(StatementArm).OfType<MatchArm>()]);
    }

    /// <summary>探索で見つけた arm の報告</summary>
    /// <param name="context">operation 解析 context</param>
    /// <param name="value">switch の入力の式</param>
    /// <param name="arms">switch の全ての arm</param>
    private static void Report(OperationAnalysisContext context, IOperation value, ImmutableArray<MatchArm> arms)
    {
        // operation 解析が渡す operation は、semantic model から作ったものである
        var typeText = new TypeText(context.Operation.SemanticModel!, context.Operation.Syntax.SpanStart);
        foreach (var catchAll in CatchAllArms.Find(value, arms, typeText))
            context.ReportDiagnostic(Diagnostic.Create(Rule, catchAll.Node.GetLocation(), catchAll.Family));
    }

    /// <summary>switch 式の arm</summary>
    /// <param name="arm">switch 式の arm の operation</param>
    /// <returns>when が定数 true でなければ報告しない arm。compiler は網羅に数えない arm の欠落を報せる</returns>
    private static MatchArm ExpressionArm(ISwitchExpressionArmOperation arm) =>
        new(arm.Pattern, arm.Pattern.Syntax, Exempt: arm.Guard is not null && arm.Guard.ConstantValue is not { HasValue: true, Value: true });

    /// <summary>switch 文の case の label</summary>
    /// <param name="clause">case の label の operation</param>
    /// <returns>pattern の label と default の label。定数の label は case を挙げるだけなので null</returns>
    /// <remarks>switch 文は compiler が case の欠落を報せないので、when があっても arm を数える</remarks>
    private static MatchArm? StatementArm(ICaseClauseOperation clause) => clause switch
    {
        IPatternCaseClauseOperation pattern => new MatchArm(pattern.Pattern, pattern.Pattern.Syntax, Exempt: false),
        IDefaultCaseClauseOperation => new MatchArm(null, clause.Syntax, Exempt: false),
        _ => null,
    };
}
