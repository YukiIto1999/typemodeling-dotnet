using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Diagnostics.ClosedEnum;

/// <summary>閉じた enum が名前付きの member 以外の値を取らないための、C# 16 の closed enum の規則</summary>
/// <remarks>csharplang の closed enum の提案の「Enforcement」と同じ三つの制約</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClosedEnumAnalyzer : DiagnosticAnalyzer
{
    /// <summary>0 に当たる member のない閉じた enum の診断</summary>
    private static readonly DiagnosticDescriptor MissingZeroRule = new(
        id: "TYPMOD018",
        title: "0 に当たる member のない閉じた enum",
        messageFormat: "閉じた enum '{0}' には、既定値 0 に当たる member が要る。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>member に当たらない値から閉じた enum への変換の診断</summary>
    private static readonly DiagnosticDescriptor ConversionRule = new(
        id: "TYPMOD019",
        title: "閉じた enum への member に当たらない変換",
        messageFormat: "閉じた enum '{0}' へは、名前付きの member に当たる定数だけを変換できる。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>member に当たらない値を作る閉じた enum の演算の診断</summary>
    private static readonly DiagnosticDescriptor OperatorRule = new(
        id: "TYPMOD020",
        title: "閉じた enum の member に当たらない演算",
        messageFormat: "閉じた enum '{0}' を返す演算は、定数どうしで名前付きの member に当たる結果だけを許す。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>対応する診断規則</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [MissingZeroRule, ConversionRule, OperatorRule];

    /// <summary>宣言と operation の解析の登録</summary>
    /// <param name="context">解析登録 context</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(AnalyzeDeclaration, SymbolKind.NamedType);
        context.RegisterOperationAction(AnalyzeConversion, OperationKind.Conversion);
        context.RegisterOperationAction(
            AnalyzeOperator,
            OperationKind.Binary, OperationKind.Unary, OperationKind.Increment, OperationKind.Decrement, OperationKind.CompoundAssignment);
    }

    /// <summary>0 に当たる member の有無の検査</summary>
    /// <param name="context">symbol 解析 context</param>
    private static void AnalyzeDeclaration(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!ClosedTypeSet.IsClosedEnum(type) || Members(type).Any(IsZero))
            return;

        context.ReportDiagnostic(Diagnostic.Create(MissingZeroRule, type.Locations.FirstOrDefault(), type.Name));
    }

    /// <summary>閉じた enum への明示的な変換の検査</summary>
    /// <param name="context">operation 解析 context</param>
    private static void AnalyzeConversion(OperationAnalysisContext context)
    {
        var conversion = (IConversionOperation)context.Operation;
        if (!ClosedTypeSet.IsClosedEnum(conversion.Type) ||
            !conversion.GetConversion().IsEnumeration ||
            IsMemberConstant(conversion, (INamedTypeSymbol)conversion.Type!))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(ConversionRule, conversion.Syntax.GetLocation(), conversion.Type!.Name));
    }

    /// <summary>閉じた enum を返す演算の検査</summary>
    /// <param name="context">operation 解析 context</param>
    private static void AnalyzeOperator(OperationAnalysisContext context)
    {
        var operation = context.Operation;
        if (!ClosedTypeSet.IsClosedEnum(operation.Type) || IsMemberConstant(operation, (INamedTypeSymbol)operation.Type!))
            return;

        context.ReportDiagnostic(Diagnostic.Create(OperatorRule, operation.Syntax.GetLocation(), operation.Type!.Name));
    }

    /// <summary>値が閉じた enum の名前付きの member に当たる定数かの判定</summary>
    /// <param name="operation">判定対象の operation</param>
    /// <param name="closedEnum">閉じた enum</param>
    /// <returns>定数で、いずれかの member の値に等しい場合に true</returns>
    private static bool IsMemberConstant(IOperation operation, INamedTypeSymbol closedEnum) =>
        operation.ConstantValue is { HasValue: true, Value: { } value } &&
        Members(closedEnum).Any(member => Equals(System.Convert.ToDecimal(member.ConstantValue, System.Globalization.CultureInfo.InvariantCulture),
            System.Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>名前付きの member</summary>
    /// <param name="closedEnum">閉じた enum</param>
    /// <returns>定数の値を持つ field</returns>
    private static ImmutableArray<IFieldSymbol> Members(INamedTypeSymbol closedEnum) =>
        [.. closedEnum.GetMembers().OfType<IFieldSymbol>().Where(field => field.HasConstantValue)];

    /// <summary>値が 0 の member かの判定</summary>
    /// <param name="member">判定対象の member</param>
    /// <returns>値が 0 なら true</returns>
    private static bool IsZero(IFieldSymbol member) =>
        System.Convert.ToDecimal(member.ConstantValue, System.Globalization.CultureInfo.InvariantCulture) == 0;
}
