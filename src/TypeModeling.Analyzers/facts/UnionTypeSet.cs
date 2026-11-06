using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Facts;

/// <summary>C# 15 の union 型の判定規則</summary>
/// <remarks>C# 15 と同じく、UnionAttribute を持つ class と struct を union 型とし、union 構築子の引数の型を case 型とする</remarks>
internal static class UnionTypeSet
{
    /// <summary>union 型を示す属性の metadata 名</summary>
    internal const string UnionAttributeMetadataName = "System.Runtime.CompilerServices.UnionAttribute";

    /// <summary>union の内容を参照する property の名前</summary>
    internal const string ValuePropertyName = "Value";

    /// <summary>union 構築子の代わりを提供する入れ子の interface の名前</summary>
    internal const string MemberProviderName = "IUnionMembers";

    /// <summary>union member provider の union 構築子の名前</summary>
    private const string FactoryMethodName = "Create";

    /// <summary>union 型の判定</summary>
    /// <param name="type">判定対象の型</param>
    /// <returns>UnionAttribute を持つ class か struct なら true</returns>
    internal static bool IsUnion(ITypeSymbol? type) =>
        type is INamedTypeSymbol { TypeKind: TypeKind.Struct or TypeKind.Class } named &&
        named.OriginalDefinition.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == UnionAttributeMetadataName);

    /// <summary>union 型の case 型</summary>
    /// <param name="union">union 型</param>
    /// <returns>union 構築子の引数の型</returns>
    internal static ImmutableArray<ITypeSymbol> CaseTypes(INamedTypeSymbol union)
    {
        if (MemberProvider(union) is { } provider)
        {
            return [.. provider.GetMembers(FactoryMethodName)
                .OfType<IMethodSymbol>()
                .Where(method => method.IsStatic && IsCreationMember(method) &&
                    SymbolEqualityComparer.Default.Equals(method.ReturnType, union))
                .Select(method => method.Parameters[0].Type)];
        }

        return [.. union.InstanceConstructors
            .Where(IsCreationMember)
            .Select(constructor => constructor.Parameters[0].Type)];
    }

    /// <summary>union の内容を参照する property</summary>
    /// <param name="union">union 型</param>
    /// <returns>object 型で公開の get を持つ Value。なければ null</returns>
    internal static IPropertySymbol? ValueProperty(INamedTypeSymbol union)
    {
        var definingType = (ITypeSymbol?)MemberProvider(union) ?? union;
        var members = definingType.GetMembers(ValuePropertyName);
        return members.Length == 1 &&
            members[0] is IPropertySymbol
            {
                IsStatic: false,
                Type.SpecialType: SpecialType.System_Object,
                GetMethod.DeclaredAccessibility: Accessibility.Public,
            } property
            ? property
            : null;
    }

    /// <summary>union 型が直接含み、実装する union member provider</summary>
    /// <param name="union">union 型</param>
    /// <returns>union member provider の interface。なければ null</returns>
    internal static INamedTypeSymbol? MemberProvider(INamedTypeSymbol union) =>
        union.GetTypeMembers(MemberProviderName)
            .FirstOrDefault(nested => nested.TypeKind == TypeKind.Interface &&
                union.AllInterfaces.Contains(nested, SymbolEqualityComparer.Default));

    /// <summary>union 構築子の判定</summary>
    /// <param name="method">判定対象の構築子か factory method</param>
    /// <returns>公開で、値渡しか in の引数を一つだけ持つ場合に true</returns>
    private static bool IsCreationMember(IMethodSymbol method) =>
        method.DeclaredAccessibility == Accessibility.Public &&
        method.Parameters.Length == 1 &&
        method.Parameters[0].RefKind is RefKind.None or RefKind.In;
}
