using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.ClosedUnion;

namespace TypeModeling.Analyzers;

/// <summary>閉じた型を switch statement で分岐する経路の禁止</summary>
/// <remarks>variant 追加時の CS8509 を保持できる switch expression への限定</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClosedUnionSwitchStatementAnalyzer : DiagnosticAnalyzer
{
    /// <summary>閉じた型の switch statement 診断</summary>
    private static readonly DiagnosticDescriptor Rule = new(
        id: "TYPMOD001",
        title: "閉じた型を switch statement で分岐している",
        messageFormat: "閉じた型 {0} は全 variant を列挙した switch expression で扱う。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>対応する診断規則</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <summary>switch statement 解析の登録</summary>
    /// <param name="context">解析登録 context</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(AnalyzeSwitchStatement, OperationKind.Switch);
    }

    /// <summary>switch statement の閉じた由来に対する診断</summary>
    /// <param name="context">operation 解析 context</param>
    private static void AnalyzeSwitchStatement(OperationAnalysisContext context)
    {
        var operation = (ISwitchOperation)context.Operation;
        var closedOrigin = ClosedVariantCaseDetector.Find(operation) ??
            ClosedTypeSet.Canonical(operation.Value.Type) ??
            ClosedOriginFinder.Find(operation.Value, context.GetControlFlowGraph());
        if (closedOrigin is null)
            return;

        var location = operation.Syntax is SwitchStatementSyntax statement
            ? statement.SwitchKeyword.GetLocation()
            : operation.Syntax.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            location,
            closedOrigin.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }
}
