using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TypeModeling.Analyzers.Generation.ValueObject;

/// <summary>検証を経ない構築と値の書込の経路を持たない値オブジェクト宣言の形の検査</summary>
/// <remarks>検査範囲は宣言の外から見える経路であり宣言の内側の member が呼べる生成コンストラクタの利用は対象外</remarks>
internal static class ValueObjectShape
{
    /// <summary>生成する検証済みの基底値を公開する property の名前</summary>
    private const string GeneratedValueName = "Value";

    /// <summary>宣言の形が禁じる経路の列挙</summary>
    /// <param name="type">値オブジェクト型</param>
    /// <param name="declaration">属性を付けた宣言</param>
    /// <returns>禁じる経路ごとの理由の連結であり適合する宣言では空文字列</returns>
    internal static string Violations(INamedTypeSymbol type, TypeDeclarationSyntax declaration) =>
        string.Concat(
            KindViolations(type, declaration)
                .Concat(InheritanceViolations(type))
                .Concat(PrimaryConstructorViolations(type))
                .Concat(ConstructorViolations(type))
                .Concat(GeneratedValueViolations(type))
                .Concat(WritableStateViolations(type))
        );

    /// <summary>partial の sealed な record でない宣言の理由</summary>
    /// <param name="type">値オブジェクト型</param>
    /// <param name="declaration">属性を付けた宣言</param>
    /// <returns>宣言の種類に関する理由</returns>
    private static IEnumerable<string> KindViolations(
        INamedTypeSymbol type,
        TypeDeclarationSyntax declaration
    )
    {
        if (!type.IsRecord)
            yield return "record ではない。";

        if (!declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
            yield return "partial ではない。";

        if (!type.IsSealed)
            yield return "sealed ではない。";
    }

    /// <summary>基底型を持つ宣言の理由</summary>
    /// <param name="type">値オブジェクト型</param>
    /// <returns>基底型に関する理由</returns>
    private static IEnumerable<string> InheritanceViolations(INamedTypeSymbol type)
    {
        if (type.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
        {
            yield return "基底型 "
                + baseType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
                + " を持つ。";
        }
    }

    /// <summary>いずれかの宣言に引数並びがある record の理由</summary>
    /// <param name="type">値オブジェクト型</param>
    /// <returns>primary コンストラクタに関する理由</returns>
    private static IEnumerable<string> PrimaryConstructorViolations(INamedTypeSymbol type)
    {
        if (
            type
                .DeclaringSyntaxReferences.Select(reference => reference.GetSyntax())
                .OfType<RecordDeclarationSyntax>()
                .Any(recordDeclaration => recordDeclaration.ParameterList is not null)
        )
        {
            yield return "primary コンストラクタを宣言している。";
        }
    }

    /// <summary>copy を含む手書きのコンストラクタの理由</summary>
    /// <param name="type">値オブジェクト型</param>
    /// <returns>宣言したコンストラクタごとの理由</returns>
    private static IEnumerable<string> ConstructorViolations(INamedTypeSymbol type) =>
        type
            .InstanceConstructors.Where(IsDeclaredConstructor)
            .Select(constructor =>
                "コンストラクタ " + ConstructorSignature(constructor) + " を宣言している。"
            );

    /// <summary>生成する Value と同名の member を持つ宣言の理由</summary>
    /// <param name="type">値オブジェクト型</param>
    /// <returns>Value の宣言に関する理由</returns>
    private static IEnumerable<string> GeneratedValueViolations(INamedTypeSymbol type)
    {
        if (type.GetMembers(GeneratedValueName).Length > 0)
            yield return "生成する member の Value を宣言している。";
    }

    /// <summary>書き込める instance の field と property の理由</summary>
    /// <param name="type">値オブジェクト型</param>
    /// <returns>書き込める状態ごとの理由</returns>
    private static IEnumerable<string> WritableStateViolations(INamedTypeSymbol type) =>
        type.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(IsWritableField)
            .Select(field => "書き込める field " + field.Name + " を宣言している。")
            .Concat(
                type.GetMembers()
                    .OfType<IPropertySymbol>()
                    .Where(IsWritableProperty)
                    .Select(property =>
                        "set か init を持つ property " + property.Name + " を宣言している。"
                    )
            );

    /// <summary>source に書かれたコンストラクタの判定</summary>
    /// <param name="constructor">instance コンストラクタ</param>
    /// <returns>コンストラクタ宣言を持つ場合に true</returns>
    private static bool IsDeclaredConstructor(IMethodSymbol constructor) =>
        constructor.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is ConstructorDeclarationSyntax
        );

    /// <summary>コンストラクタの型名と引数型の表記</summary>
    /// <param name="constructor">表記するコンストラクタ</param>
    /// <returns>型名に引数型を括弧で続けた表記</returns>
    private static string ConstructorSignature(IMethodSymbol constructor) =>
        constructor.ContainingType.Name
        + "("
        + string.Join(
            ", ",
            constructor.Parameters.Select(parameter =>
                parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)
            )
        )
        + ")";

    /// <summary>readonly でない instance field の判定</summary>
    /// <param name="field">検査対象の field</param>
    /// <returns>手書きの書き込める instance field の場合に true</returns>
    private static bool IsWritableField(IFieldSymbol field) =>
        !field.IsStatic && !field.IsConst && !field.IsReadOnly && field.AssociatedSymbol is null;

    /// <summary>set か init の accessor を持つ instance property の判定</summary>
    /// <param name="property">検査対象の property</param>
    /// <returns>property 宣言を持つ書き込める instance property の場合に true</returns>
    private static bool IsWritableProperty(IPropertySymbol property) =>
        !property.IsStatic
        && property.SetMethod is not null
        && property.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is PropertyDeclarationSyntax
        );
}
