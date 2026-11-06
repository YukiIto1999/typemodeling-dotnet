using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Diagnostics.ClosedType;

/// <summary>閉じた class の宣言と派生の C# 15 の規則の検査</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClosedTypeDeclarationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>abstract でない閉じた class の診断</summary>
    /// <remarks>C# 15 の closed は暗黙に abstract であり、C# 14 以前ではそれを abstract の明示で表す</remarks>
    private static readonly DiagnosticDescriptor NotAbstractRule = new(
        id: "TYPMOD003",
        title: "abstract でない閉じた class",
        messageFormat: "閉じた class '{0}' は abstract で宣言する。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>別の assembly の閉じた class からの直接の派生の診断</summary>
    /// <remarks>C# 15 の CS9382 に対応する</remarks>
    private static readonly DiagnosticDescriptor ForeignBaseRule = new(
        id: "TYPMOD004",
        title: "別の assembly の閉じた class からの派生",
        messageFormat: "'{0}' は、別の assembly の閉じた class '{1}' を基底型にできない。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>閉じた基底型で参照しない型引数の診断</summary>
    /// <remarks>C# 15 の CS9383 に対応する</remarks>
    private static readonly DiagnosticDescriptor UnreferencedTypeParameterRule = new(
        id: "TYPMOD005",
        title: "閉じた基底型で参照しない型引数",
        messageFormat: "'{0}' の型引数 '{1}' は、閉じた基底型 '{2}' で参照する。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>対応する診断規則</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        [NotAbstractRule, ForeignBaseRule, UnreferencedTypeParameterRule];

    /// <summary>型の宣言の解析の登録</summary>
    /// <param name="context">解析登録 context</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    /// <summary>閉じた class の宣言と、閉じた class を基底にする宣言の検査</summary>
    /// <param name="context">symbol 解析 context</param>
    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var location = type.Locations.FirstOrDefault();
        if (ClosedTypeSet.IsClosedClass(type) && (!type.IsAbstract || type.IsStatic))
            context.ReportDiagnostic(Diagnostic.Create(NotAbstractRule, location, type.Name));

        if (type.BaseType is not { } baseType || !ClosedTypeSet.IsClosedClass(baseType))
            return;

        if (!SymbolEqualityComparer.Default.Equals(baseType.ContainingAssembly, type.ContainingAssembly))
            context.ReportDiagnostic(Diagnostic.Create(ForeignBaseRule, location, type.Name, baseType.Name));

        foreach (var parameter in type.TypeParameters.Where(parameter => !References(baseType, parameter)))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                UnreferencedTypeParameterRule,
                parameter.Locations.FirstOrDefault(),
                type.Name,
                parameter.Name,
                baseType.Name));
        }
    }

    /// <summary>型が型引数を参照するかの判定</summary>
    /// <param name="type">参照を探す型</param>
    /// <param name="parameter">探す型引数</param>
    /// <returns>型の構成のどこかで型引数を参照する場合に true</returns>
    private static bool References(ITypeSymbol type, ITypeParameterSymbol parameter) => type switch
    {
        ITypeParameterSymbol candidate => SymbolEqualityComparer.Default.Equals(candidate, parameter),
        IArrayTypeSymbol array => References(array.ElementType, parameter),
        IPointerTypeSymbol pointer => References(pointer.PointedAtType, parameter),
        INamedTypeSymbol named => named.TypeArguments.Any(argument => References(argument, parameter)) ||
            (named.ContainingType is { } container && References(container, parameter)),
        _ => false,
    };
}
