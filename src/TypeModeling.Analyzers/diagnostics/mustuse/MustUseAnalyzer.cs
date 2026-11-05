using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.Diagnostics.MustUse;

/// <summary>使わずに捨てた MustUse の値の検出</summary>
/// <remarks>Rust の must_use と同じく、型か method に MustUse を付けた値を式文で捨てると警告する。discard への代入は明示的な破棄として許す</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MustUseAnalyzer : DiagnosticAnalyzer
{
    /// <summary>MustUse 属性の metadata 名</summary>
    private const string MustUseAttributeMetadataName = "TypeModeling.Domain.MustUseAttribute";

    /// <summary>捨てた MustUse の値の診断</summary>
    private static readonly DiagnosticDescriptor Rule = new(
        id: "TYPMOD017",
        title: "使わずに捨てた値",
        messageFormat: "'{0}' の値は使う必要がある。失敗を無視する場合は discard に代入して明示する。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>対応する診断規則</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <summary>式文の解析の登録</summary>
    /// <param name="context">解析登録 context</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(Analyze, OperationKind.ExpressionStatement);
    }

    /// <summary>式文が捨てる値の MustUse の判定</summary>
    /// <param name="context">operation 解析 context</param>
    private static void Analyze(OperationAnalysisContext context)
    {
        var discarded = ((IExpressionStatementOperation)context.Operation).Operation;
        var produced = discarded is IAwaitOperation awaited ? awaited.Operation : discarded;
        if (produced is not IInvocationOperation invocation || discarded.Type is null)
            return;

        if (HasMustUse(discarded.Type) || HasMustUse(invocation.TargetMethod))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                Rule,
                discarded.Syntax.GetLocation(),
                invocation.TargetMethod.Name));
        }
    }

    /// <summary>MustUse 属性を持つかの判定</summary>
    /// <param name="symbol">判定対象の型か method</param>
    /// <returns>MustUse 属性を持つ場合に true</returns>
    private static bool HasMustUse(ISymbol symbol) =>
        (symbol is ITypeSymbol type ? type.OriginalDefinition : symbol).GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == MustUseAttributeMetadataName);
}
