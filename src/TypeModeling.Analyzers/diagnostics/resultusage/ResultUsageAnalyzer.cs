using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.Diagnostics.ResultUsage;

/// <summary>Result の未初期化の構築と、case 型への cast による部分的な取り出しの検出</summary>
/// <remarks>architecture-standard の connection と inspection が求める Result の規律</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResultUsageAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Result 型の metadata 名</summary>
    private const string ResultMetadataName = "TypeModeling.Domain.Result`2";

    /// <summary>未初期化の Result の構築の診断</summary>
    private static readonly DiagnosticDescriptor UninitializedRule = new(
        id: "TYPMOD015",
        title: "未初期化の Result の構築",
        messageFormat: "default の Result は成功でも失敗でもない未初期化の値になる。Result.Succeeded か Result.Failed で構築する。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>case 型への cast による取り出しの診断</summary>
    private static readonly DiagnosticDescriptor CaseCastRule = new(
        id: "TYPMOD016",
        title: "case 型への cast による Result の取り出し",
        messageFormat: "Result の Value を case 型へ cast すると、他の case で例外になる。網羅した switch 式、IsFailed、Match で取り出す。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>対応する診断規則</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [UninitializedRule, CaseCastRule];

    /// <summary>operation 解析の登録</summary>
    /// <param name="context">解析登録 context</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(ResultMetadataName) is not { } result)
                return;

            start.RegisterOperationAction(operation => AnalyzeDefault(operation, result), OperationKind.DefaultValue, OperationKind.ObjectCreation);
            start.RegisterOperationAction(operation => AnalyzeCast(operation, result), OperationKind.Conversion);
        });
    }

    /// <summary>default と引数なしの構築による Result の報告</summary>
    /// <param name="context">operation 解析 context</param>
    /// <param name="result">Result 型の定義</param>
    private static void AnalyzeDefault(OperationAnalysisContext context, INamedTypeSymbol result)
    {
        var uninitialized = context.Operation switch
        {
            IDefaultValueOperation operation => IsResult(operation.Type, result),
            IObjectCreationOperation { Constructor.Parameters.IsEmpty: true } operation => IsResult(operation.Type, result),
            _ => false,
        };
        if (uninitialized)
            context.ReportDiagnostic(Diagnostic.Create(UninitializedRule, context.Operation.Syntax.GetLocation()));
    }

    /// <summary>Result の Value から case 型への明示的な変換の報告</summary>
    /// <param name="context">operation 解析 context</param>
    /// <param name="result">Result 型の定義</param>
    private static void AnalyzeCast(OperationAnalysisContext context, INamedTypeSymbol result)
    {
        var conversion = (IConversionOperation)context.Operation;
        if (conversion.IsImplicit || conversion.Operand is not IPropertyReferenceOperation { Property.Name: "Value" } value)
            return;

        if (IsResult(value.Property.ContainingType, result))
            context.ReportDiagnostic(Diagnostic.Create(CaseCastRule, conversion.Syntax.GetLocation()));
    }

    /// <summary>Result 型かの判定</summary>
    /// <param name="type">判定対象の型</param>
    /// <param name="result">Result 型の定義</param>
    /// <returns>Result の構築形なら true</returns>
    private static bool IsResult(ITypeSymbol? type, INamedTypeSymbol result) =>
        type is INamedTypeSymbol named && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, result);
}
