using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Facts;

/// <summary>閉じた型集合の定義規則</summary>
internal static class ClosedTypeSet
{
    /// <summary>ClosedUnion attribute の metadata 名</summary>
    private const string ClosedUnionAttributeMetadataName =
        "TypeModeling.Domain.ClosedUnionAttribute";

    /// <summary>suppressor が閉集合として扱う型の判定</summary>
    /// <param name="type">検査対象の型</param>
    /// <returns>suppressor の閉集合として扱う場合に true</returns>
    internal static bool IsClosed(ITypeSymbol type) =>
        TryGetClosedUnionDefinition(type, out _);

    /// <summary>閉集合に属する variant 定義</summary>
    /// <param name="type">検査対象の型</param>
    /// <returns>閉集合に属する variant 定義</returns>
    internal static ImmutableArray<INamedTypeSymbol> Variants(ITypeSymbol type)
    {
        if (!TryGetClosedUnionDefinition(type, out var unionDefinition))
            return [];

        return unionDefinition
            .GetTypeMembers()
            .Where(nested =>
                nested.TypeKind == TypeKind.Class &&
                nested.IsSealed &&
                InheritsFrom(nested, unionDefinition))
            .Select(variant => variant.OriginalDefinition)
            .ToImmutableArray();
    }

    /// <summary>ClosedUnion attribute を持つ基底定義の取得</summary>
    /// <param name="type">検査対象の型</param>
    /// <param name="definition">ClosedUnion attribute を持つ基底定義</param>
    /// <returns>基底定義が見つかった場合に true</returns>
    private static bool TryGetClosedUnionDefinition(
        ITypeSymbol type,
        out INamedTypeSymbol definition)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (current.OriginalDefinition.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == ClosedUnionAttributeMetadataName))
            {
                definition = current.OriginalDefinition;
                return true;
            }
        }

        definition = null!;
        return false;
    }

    /// <summary>候補型が基底定義を継承するかの判定</summary>
    /// <param name="candidate">継承関係の検査対象</param>
    /// <param name="baseDefinition">継承元の型定義</param>
    /// <returns>基底定義を継承する場合に true</returns>
    private static bool InheritsFrom(ITypeSymbol candidate, ITypeSymbol baseDefinition)
    {
        for (ITypeSymbol? current = candidate.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseDefinition))
                return true;
        }

        return false;
    }
}
