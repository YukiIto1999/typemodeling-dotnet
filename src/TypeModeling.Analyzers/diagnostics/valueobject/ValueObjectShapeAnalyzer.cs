using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Diagnostics.ValueObject;

/// <summary>ValueObject の型の宣言の形の検出</summary>
/// <remarks>generator は別の generator の出力を見られないので、別の generator の出力と生成 code の印を付けた手書きの code を含む最終の compilation を解析し、generator が出力する private コンストラクタだけを署名で一意に許す検査範囲</remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValueObjectShapeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>ValueObject の宣言の形の診断</summary>
    private static readonly DiagnosticDescriptor Rule = new(
        id: "TYPMOD023",
        title: "ValueObject の宣言の形が不適合",
        messageFormat: "型 {0} は検証を経ずに構築も書込もできない宣言にする。基底型のない sealed partial record とし、コンストラクタ、Value、書き込める field、property、indexer を宣言しない。{1}",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>対応する診断規則</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <summary>型の宣言の解析の登録</summary>
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

            start.RegisterSymbolAction(
                symbol => AnalyzeType(symbol, attribute),
                SymbolKind.NamedType
            );
        });
    }

    /// <summary>ValueObject 属性を付けた型の宣言の形の報告</summary>
    /// <param name="context">symbol 解析 context</param>
    /// <param name="attribute">ValueObject 属性の定義</param>
    private static void AnalyzeType(SymbolAnalysisContext context, INamedTypeSymbol attribute)
    {
        if (
            context.Symbol is not INamedTypeSymbol { TypeKind: TypeKind.Class } type
            || ValueObjectContract.Marker(type, attribute) is not { } marker
        )
        {
            return;
        }

        var violations = ValueObjectShape.Violations(type, marker.Underlying);
        if (violations.Length == 0)
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                DeclarationLocation(type, marker.Application, context.CancellationToken),
                type.Name,
                violations
            )
        );
    }

    /// <summary>ValueObject 属性を付けた宣言の型名の位置の取得</summary>
    /// <param name="type">値オブジェクト型</param>
    /// <param name="application">ValueObject 属性の適用</param>
    /// <param name="cancellationToken">取消の token</param>
    /// <returns>属性を付けた宣言の型名の位置であり、見つからなければ型の最初の宣言の位置</returns>
    private static Location? DeclarationLocation(
        INamedTypeSymbol type,
        AttributeData application,
        CancellationToken cancellationToken
    ) =>
        application.ApplicationSyntaxReference?.GetSyntax(cancellationToken).Parent?.Parent
            is TypeDeclarationSyntax declaration
            ? declaration.Identifier.GetLocation()
            : type.Locations.FirstOrDefault();
}
