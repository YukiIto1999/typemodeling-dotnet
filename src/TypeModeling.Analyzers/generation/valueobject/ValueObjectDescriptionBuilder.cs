using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TypeModeling.Analyzers.Generation.ValueObject;

/// <summary>診断を含む値オブジェクト記述への変換</summary>
internal static class ValueObjectDescriptionBuilder
{
    /// <summary>ValueObject 属性の metadata 名</summary>
    internal const string AttributeMetadataName =
        "TypeModeling.Domain.ValueObjectAttribute`1";

    /// <summary>Result 型の metadata 名</summary>
    private const string ResultMetadataName =
        "TypeModeling.Domain.Result`2";

    /// <summary>ValueObject の Parse 署名診断</summary>
    private static readonly DiagnosticDescriptor ParseRule = new(
        id: "TYPMOD002",
        title: "ValueObject の Parse 署名が不適合",
        messageFormat:
            "型 {0} には private static Result<{1}, TFailure> Parse({1} raw) が必要。{2}",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>属性付き宣言からの値オブジェクト記述構築</summary>
    /// <param name="context">属性付き宣言の生成 context</param>
    /// <returns>値オブジェクトの生成記述</returns>
    internal static ValueObjectDescription? Describe(
        GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetNode is not RecordDeclarationSyntax declaration ||
            !declaration.Modifiers.Any(SyntaxKind.PartialKeyword) ||
            context.TargetSymbol is not INamedTypeSymbol
            { IsRecord: true, TypeKind: TypeKind.Class } type)
        {
            return null;
        }

        if (context.Attributes.Length == 0 ||
            context.Attributes[0].AttributeClass is not
            { TypeArguments.Length: 1 } attributeClass)
        {
            return null;
        }

        var underlying = attributeClass.TypeArguments[0];
        var resultDefinition = context.SemanticModel.Compilation
            .GetTypeByMetadataName(ResultMetadataName);
        var parseMethods = type.GetMembers("Parse")
            .OfType<IMethodSymbol>()
            .Where(method => method.MethodKind == MethodKind.Ordinary)
            .ToArray();
        foreach (var method in parseMethods)
        {
            if (!HasParseShape(method, underlying))
                continue;

            var returnType = ResultReturnType(
                method,
                underlying,
                resultDefinition);
            if (returnType is null)
                continue;

            return ValueObjectDescription.Succeeded(CreateModel(
                type,
                underlying,
                returnType));
        }

        return ValueObjectDescription.Failed(
            ParseRule,
            declaration.Identifier.GetLocation(),
            type.Name,
            underlying.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            ParseFailureReason(parseMethods, underlying));
    }

    /// <summary>適合済み宣言からの生成 model 構築</summary>
    /// <param name="type">値オブジェクト型</param>
    /// <param name="underlying">基底型</param>
    /// <param name="returnType">Parse の返却型</param>
    /// <returns>値オブジェクト生成 model</returns>
    private static ValueObjectModel CreateModel(
        INamedTypeSymbol type,
        ITypeSymbol underlying,
        INamedTypeSymbol returnType) =>
        new(
            type.ContainingNamespace.IsGlobalNamespace
                ? null
                : type.ContainingNamespace.ToDisplayString(),
            type.Name,
            underlying.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            FailureType(returnType),
            underlying.IsValueType);

    /// <summary>Parse method の形状判定</summary>
    /// <param name="method">検査対象 method</param>
    /// <param name="underlying">値オブジェクトの基底型</param>
    /// <returns>要求する Parse 形状の場合に true</returns>
    private static bool HasParseShape(
        IMethodSymbol method,
        ITypeSymbol underlying) =>
        method.IsStatic &&
        method.Arity == 0 &&
        method.DeclaredAccessibility == Accessibility.Private &&
        method.Parameters.Length == 1 &&
        method.Parameters[0].RefKind == RefKind.None &&
        SymbolEqualityComparer.Default.Equals(
            method.Parameters[0].Type,
            underlying);

    /// <summary>Parse の Result 返却型検証</summary>
    /// <param name="method">形状検証済み Parse method</param>
    /// <param name="underlying">値オブジェクトの基底型</param>
    /// <param name="resultDefinition">正規の Result 型定義</param>
    /// <returns>適合する Result 返却型</returns>
    private static INamedTypeSymbol? ResultReturnType(
        IMethodSymbol method,
        ITypeSymbol underlying,
        INamedTypeSymbol? resultDefinition)
    {
        if (method.ReturnType is not INamedTypeSymbol returnType ||
            resultDefinition is null ||
            !SymbolEqualityComparer.Default.Equals(
                returnType.OriginalDefinition,
                resultDefinition) ||
            !SymbolEqualityComparer.Default.Equals(
                returnType.TypeArguments[0],
                underlying))
        {
            return null;
        }

        return returnType;
    }

    /// <summary>不適合な Parse の理由構築</summary>
    /// <param name="methods">Parse method の候補</param>
    /// <param name="underlying">値オブジェクトの基底型</param>
    /// <returns>Parse 署名の不適合理由</returns>
    private static string ParseFailureReason(
        IMethodSymbol[] methods,
        ITypeSymbol underlying)
    {
        if (methods.Length == 0)
            return "Parse が見つからない。";

        var method = methods[0];
        if (!method.IsStatic)
            return "Parse は static method ではない。";

        if (method.Arity != 0)
            return "Parse は generic method ではない。";

        if (method.DeclaredAccessibility != Accessibility.Private)
            return "Parse のアクセシビリティは private ではない。";

        if (method.Parameters.Length != 1 ||
            method.Parameters[0].RefKind != RefKind.None)
        {
            return "Parse は値引数を一つだけ受け取らない。";
        }

        var parameterType = method.Parameters[0].Type;
        if (!SymbolEqualityComparer.Default.Equals(parameterType, underlying))
        {
            return "Parse の引数型 " +
                parameterType.ToDisplayString(
                    SymbolDisplayFormat.MinimallyQualifiedFormat) +
                " は基底型 " +
                underlying.ToDisplayString(
                    SymbolDisplayFormat.MinimallyQualifiedFormat) +
                " と一致しない。";
        }

        return "Parse の返却型 " +
            method.ReturnType.ToDisplayString(
                SymbolDisplayFormat.CSharpErrorMessageFormat) +
            " は Result<" +
            underlying.ToDisplayString(
                SymbolDisplayFormat.MinimallyQualifiedFormat) +
            ", TFailure> ではない。";
    }

    /// <summary>Result 返却型からの失敗型抽出</summary>
    /// <param name="returnType">適合済み Result 返却型</param>
    /// <returns>完全修飾した失敗型名</returns>
    private static string FailureType(INamedTypeSymbol returnType) =>
        returnType.TypeArguments[1]
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
}
