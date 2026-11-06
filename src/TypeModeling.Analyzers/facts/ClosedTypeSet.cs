using System.Linq;
using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Facts;

/// <summary>閉じた型の判定規則</summary>
/// <remarks>
/// TypeModeling の Closed 属性を持つ class と enum を閉じた型とする。
/// C# 15 の compiler が closed に付ける IsClosedType 属性は、closed を理解する Roslyn が属性の一覧から外すので、analyzer の判定には使えない。
/// </remarks>
internal static class ClosedTypeSet
{
    /// <summary>TypeModeling の Closed 属性の metadata 名</summary>
    internal const string ClosedAttributeMetadataName = "TypeModeling.Domain.ClosedAttribute";

    /// <summary>閉じた class の判定</summary>
    /// <param name="type">判定対象の型</param>
    /// <returns>閉じた class なら true</returns>
    internal static bool IsClosedClass(ITypeSymbol? type) =>
        type is INamedTypeSymbol { TypeKind: TypeKind.Class } named && HasClosedAttribute(named.OriginalDefinition);

    /// <summary>閉じた enum の判定</summary>
    /// <param name="type">判定対象の型</param>
    /// <returns>閉じた enum なら true</returns>
    internal static bool IsClosedEnum(ITypeSymbol? type) =>
        type is INamedTypeSymbol { TypeKind: TypeKind.Enum } named && HasClosedAttribute(named.OriginalDefinition);

    /// <summary>網羅の判定で閉じた型として扱う型</summary>
    /// <param name="type">pattern が受け取る型。型のない式なら null</param>
    /// <returns>閉じた型そのもの、閉じた class に制約された型引数ならその class、それ以外は null</returns>
    /// <remarks>
    /// C# 15 の「Exhaustiveness of type parameters constrained to closed type」に従う。
    /// 型引数が複数の閉じた class に制約される場合、それらは一つの派生の連鎖に並び、外側の class に足す pattern も内側の class の派生を扱うので、最初に見つかった制約を取る。
    /// </remarks>
    internal static INamedTypeSymbol? EffectiveClosedType(ITypeSymbol? type)
    {
        if (type is not ITypeParameterSymbol parameter)
            return type is INamedTypeSymbol named && HasClosedAttribute(named.OriginalDefinition) ? named : null;

        foreach (var constraint in parameter.ConstraintTypes)
        {
            if (EffectiveClosedType(constraint) is { } closed)
                return closed;
        }

        return null;
    }

    /// <summary>TypeModeling の Closed 属性を持つかの判定</summary>
    /// <param name="definition">判定対象の元定義</param>
    /// <returns>Closed 属性を持つなら true</returns>
    private static bool HasClosedAttribute(INamedTypeSymbol definition) =>
        definition.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == ClosedAttributeMetadataName);
}
