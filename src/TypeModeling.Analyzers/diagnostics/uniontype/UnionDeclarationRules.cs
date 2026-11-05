using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers.Facts;
using TypeModeling.Analyzers.Generation.Union;

namespace TypeModeling.Analyzers.Diagnostics.UnionType;

/// <summary>union 宣言の形が C# 15 の union 宣言へ書き換えられるための制約</summary>
internal static class UnionDeclarationRules
{
    /// <summary>union 宣言の形の制約の検査</summary>
    /// <param name="context">symbol 解析 context</param>
    /// <param name="type">union 宣言の形の型</param>
    /// <param name="location">報告する位置</param>
    internal static void Check(SymbolAnalysisContext context, INamedTypeSymbol type, Location? location)
    {
        if (type.TypeKind != TypeKind.Struct)
            context.ReportDiagnostic(Diagnostic.Create(UnionTypeRules.NotStruct, location, type.Name));

        if (!UnionDeclarationSource.AllContainersArePartial(type))
            context.ReportDiagnostic(Diagnostic.Create(UnionTypeRules.NonPartialContainer, location, type.Name));

        if (UnionTypeSet.MemberProvider(type) is not null)
            context.ReportDiagnostic(Diagnostic.Create(UnionTypeRules.MemberProvider, location, type.Name));

        foreach (var member in type.GetMembers())
            CheckMember(context, type, member);
    }

    /// <summary>member の制約の検査</summary>
    /// <param name="context">symbol 解析 context</param>
    /// <param name="type">union 宣言の形の型</param>
    /// <param name="member">検査する member</param>
    /// <remarks>
    /// 自動実装 property と field キーワードの property は backing field を持つので、instance field として数える。
    /// generator が足した Value の backing field は生成 source の位置にあり、生成 code を解析しない設定が報告から外す。
    /// </remarks>
    private static void CheckMember(SymbolAnalysisContext context, INamedTypeSymbol type, ISymbol member)
    {
        var location = member.Locations.FirstOrDefault();
        switch (member)
        {
            case IFieldSymbol { IsStatic: false }:
            case IEventSymbol { IsStatic: false } @event when (@event.PartialImplementationPart ?? @event).AddMethod is { IsImplicitlyDeclared: true }:
                context.ReportDiagnostic(Diagnostic.Create(UnionTypeRules.InstanceField, location, type.Name));
                break;
            case IMethodSymbol { MethodKind: MethodKind.Constructor, IsImplicitlyDeclared: false } constructor
                when !UnionDeclarationShape.IsCaseConstructor(constructor):
                CheckConstructor(context, type, constructor, location);
                break;
        }
    }

    /// <summary>partial でないコンストラクタの制約の検査</summary>
    /// <param name="context">symbol 解析 context</param>
    /// <param name="type">union 宣言の形の型</param>
    /// <param name="constructor">検査するコンストラクタ</param>
    /// <param name="location">報告する位置</param>
    private static void CheckConstructor(SymbolAnalysisContext context, INamedTypeSymbol type, IMethodSymbol constructor, Location? location)
    {
        if (constructor is { DeclaredAccessibility: Accessibility.Public, Parameters.Length: 1 })
        {
            context.ReportDiagnostic(Diagnostic.Create(UnionTypeRules.SingleParameterConstructor, location, type.Name));
            return;
        }

        var implementation = constructor.PartialImplementationPart ?? constructor;
        var callsThis = implementation.DeclaringSyntaxReferences[0].GetSyntax(context.CancellationToken) is ConstructorDeclarationSyntax
        {
            Initializer.ThisOrBaseKeyword.RawKind: (int)SyntaxKind.ThisKeyword,
        };
        if (!callsThis)
            context.ReportDiagnostic(Diagnostic.Create(UnionTypeRules.ConstructorWithoutThis, location, type.Name));
    }
}
