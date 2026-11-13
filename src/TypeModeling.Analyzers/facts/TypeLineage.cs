using System.Linq;
using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Facts;

/// <summary>型の継承と実装の関係の判定規則</summary>
internal static class TypeLineage
{
    /// <summary>型が、ある型か、その派生か、その実装であるかの判定</summary>
    /// <param name="type">判定対象の型</param>
    /// <param name="ancestor">基底の class、object、または interface</param>
    /// <returns>type が ancestor と同じ型か、ancestor から派生するか、ancestor を実装するなら true</returns>
    internal static bool IsAtOrBelow(ITypeSymbol type, ITypeSymbol ancestor)
    {
        if (SymbolEqualityComparer.Default.Equals(type, ancestor))
            return true;

        if (ancestor.TypeKind == TypeKind.Interface)
            return type.AllInterfaces.Any(implemented =>
                SymbolEqualityComparer.Default.Equals(implemented, ancestor)
            );

        for (var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType, ancestor))
                return true;
        }

        return false;
    }

    /// <summary>nullable の注釈と Nullable の包みを除いた、同じ型かの判定</summary>
    /// <param name="left">比べる型</param>
    /// <param name="right">比べる型</param>
    /// <returns>nullable を除いて同じ型なら true</returns>
    internal static bool Same(ITypeSymbol left, ITypeSymbol right) =>
        SymbolEqualityComparer.Default.Equals(WithoutNullable(left), WithoutNullable(right));

    /// <summary>Nullable の包みを除いた型</summary>
    /// <param name="type">対象の型。型がなければ null</param>
    /// <returns>値型の Nullable なら包まれた型。それ以外は type そのもの</returns>
    internal static ITypeSymbol? WithoutNullable(ITypeSymbol? type) =>
        type
            is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T
            } nullable
            ? nullable.TypeArguments[0]
            : type;
}
