using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers.Facts;
using TypeModeling.Analyzers.Generation.Union;

namespace TypeModeling.Analyzers.Diagnostics.UnionType;

/// <summary>union 型と union 宣言の形の C# 15 の規則の検査</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnionTypeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>対応する診断規則</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = UnionTypeRules.All;

    /// <summary>型の宣言の解析の登録</summary>
    /// <param name="context">解析登録 context</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    /// <summary>union 型の基本の形と、union 宣言の形の制約の検査</summary>
    /// <param name="context">symbol 解析 context</param>
    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!UnionTypeSet.IsUnion(type))
            return;

        var location = type.Locations.FirstOrDefault();
        if (UnionDeclarationShape.IsDeclaration(type))
        {
            UnionDeclarationRules.Check(context, type, location);
            return;
        }

        if (location?.SourceTree is { } tree && UnionDeclarationShape.LanguageHasUnions(tree))
            return;

        if (UnionTypeSet.CaseTypes(type).IsEmpty)
            context.ReportDiagnostic(Diagnostic.Create(UnionTypeRules.MissingCaseTypes, location, type.Name));

        if (UnionTypeSet.ValueProperty(type) is null)
            context.ReportDiagnostic(Diagnostic.Create(UnionTypeRules.MissingValueProperty, location, type.Name));
    }
}
