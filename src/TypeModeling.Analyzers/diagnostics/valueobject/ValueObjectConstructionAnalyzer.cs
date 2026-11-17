using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Diagnostics.ValueObject;

/// <summary>ValueObject の型を new で構築する式の検出</summary>
/// <remarks>別の generator の出力と生成 code の印を付けた手書きの code を含めて解析し生成する Create の中の構築だけを署名で一意に許す検査範囲。new を使わない構築の経路はコンストラクタの宣言の禁止が塞ぐ</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValueObjectConstructionAnalyzer : DiagnosticAnalyzer
{
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
            if (
                start.Compilation.GetTypeByMetadataName(ValueObjectContract.AttributeMetadataName)
                is not { } attribute
            )
            {
                return;
            }

            var result = start.Compilation.GetTypeByMetadataName(
                ValueObjectContract.ResultMetadataName
            );
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
            || ValueObjectContract.Marker(type, attribute) is not { } marker
            || ValueObjectContract.IsGeneratedCreate(
                EnclosingMethod(context.ContainingSymbol),
                type,
                marker.Underlying,
                result
            )
        )
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.Syntax.GetLocation(), type.Name));
    }

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
}
