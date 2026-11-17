using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.Diagnostics.ValueObject;

/// <summary>ValueObject の型を new で構築する式の検出</summary>
/// <remarks>別の generator の出力と生成 code の印を付けた手書きの code を含めて解析し生成する Create の中の構築だけを署名で一意に許す検査範囲</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValueObjectConstructionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>ValueObject 属性の metadata 名</summary>
    private const string AttributeMetadataName = "TypeModeling.Domain.ValueObjectAttribute`1";

    /// <summary>Result 型の metadata 名</summary>
    private const string ResultMetadataName = "TypeModeling.Domain.Result`2";

    /// <summary>生成する構築 method の名前</summary>
    private const string CreateName = "Create";

    /// <summary>値オブジェクトの new による構築の診断</summary>
    private static readonly DiagnosticDescriptor Rule = new(
        id: "TYPMOD024",
        title: "値オブジェクトの new による構築",
        messageFormat: "値オブジェクト '{0}' を new で構築すると Parse の検証を迂回する。Create を呼び、複製は with で作る。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>対応する診断規則</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <summary>operation 解析の登録</summary>
    /// <param name="context">解析登録 context</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics
        );
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(AttributeMetadataName) is not { } attribute)
                return;

            var result = start.Compilation.GetTypeByMetadataName(ResultMetadataName);
            start.RegisterOperationAction(
                operation => AnalyzeCreation(operation, attribute, result),
                OperationKind.ObjectCreation
            );
        });
    }

    /// <summary>生成する Create の外で値オブジェクトを構築する new 式の報告</summary>
    /// <param name="context">operation 解析 context</param>
    /// <param name="attribute">ValueObject 属性の定義</param>
    /// <param name="result">参照に無ければ null の Result 型の定義</param>
    private static void AnalyzeCreation(
        OperationAnalysisContext context,
        INamedTypeSymbol attribute,
        INamedTypeSymbol? result
    )
    {
        if (
            context.Operation
                is not IObjectCreationOperation { Type: INamedTypeSymbol type } creation
            || Underlying(type, attribute) is not { } underlying
            || IsGeneratedCreate(
                EnclosingMethod(context.ContainingSymbol),
                type,
                underlying,
                result
            )
        )
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.Syntax.GetLocation(), type.Name));
    }

    /// <summary>ValueObject 属性が指す基底型の取得</summary>
    /// <param name="type">構築する型</param>
    /// <param name="attribute">ValueObject 属性の定義</param>
    /// <returns>ValueObject 属性を持たない型では null の基底型</returns>
    private static ITypeSymbol? Underlying(INamedTypeSymbol type, INamedTypeSymbol attribute) =>
        type.GetAttributes()
            .Select(data => data.AttributeClass)
            .FirstOrDefault(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate?.OriginalDefinition, attribute)
            )
            ?.TypeArguments[0];

    /// <summary>lambda と local function を抜けた、構築を含む method の取得</summary>
    /// <param name="symbol">構築を含む symbol</param>
    /// <returns>field 初期化子や property のように method の中でなければ null の構築を含む method</returns>
    private static IMethodSymbol? EnclosingMethod(ISymbol? symbol)
    {
        while (
            symbol
                is IMethodSymbol
                {
                    MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction
                } function
        )
            symbol = function.ContainingSymbol;

        return symbol as IMethodSymbol;
    }

    /// <summary>generator が出力する Create と同じ署名の method かの判定</summary>
    /// <param name="method">構築を含む method</param>
    /// <param name="type">構築する値オブジェクトの型</param>
    /// <param name="underlying">値オブジェクトの基底型</param>
    /// <param name="result">参照に無ければ null の Result 型の定義</param>
    /// <returns>値オブジェクト自身の public static Create(基底型) が Result の値オブジェクトを返す場合に true</returns>
    private static bool IsGeneratedCreate(
        IMethodSymbol? method,
        INamedTypeSymbol type,
        ITypeSymbol underlying,
        INamedTypeSymbol? result
    ) =>
        method
            is {
                Name: CreateName,
                MethodKind: MethodKind.Ordinary,
                IsStatic: true,
                Arity: 0,
                DeclaredAccessibility: Accessibility.Public,
                Parameters.Length: 1,
            }
        && SymbolEqualityComparer.Default.Equals(method.ContainingType, type)
        && method.Parameters[0].RefKind == RefKind.None
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, underlying)
        && ReturnsResultOf(method, type, result);

    /// <summary>method が値オブジェクトを成功の値に持つ Result を返すかの判定</summary>
    /// <param name="method">判定対象の method</param>
    /// <param name="type">成功の値の型</param>
    /// <param name="result">参照に無ければ null の Result 型の定義</param>
    /// <returns>Result の値の型引数が値オブジェクトの場合に true</returns>
    private static bool ReturnsResultOf(
        IMethodSymbol method,
        INamedTypeSymbol type,
        INamedTypeSymbol? result
    ) =>
        result is not null
        && method.ReturnType is INamedTypeSymbol returned
        && SymbolEqualityComparer.Default.Equals(returned.OriginalDefinition, result)
        && SymbolEqualityComparer.Default.Equals(returned.TypeArguments[0], type);
}
