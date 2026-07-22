using Microsoft.CodeAnalysis;

namespace TypeModeling.Testing.Attach;

/// <summary>closed type の semantic 利用検出器</summary>
public sealed class ClosedTypeUsageDetector : IGuardedFeatureDetector
{
    /// <summary>closed union 属性の metadata 名</summary>
    private const string ClosedUnionAttributeMetadataName =
        "TypeModeling.Domain.ClosedUnionAttribute";

    /// <summary>JSON polymorphic 属性の metadata 名</summary>
    private const string JsonPolymorphicAttributeMetadataName =
        "System.Text.Json.Serialization.JsonPolymorphicAttribute";

    /// <summary>JSON derived type 属性の metadata 名</summary>
    private const string JsonDerivedTypeAttributeMetadataName =
        "System.Text.Json.Serialization.JsonDerivedTypeAttribute";

    /// <summary>closed type 利用の semantic 走査</summary>
    private static readonly GuardedTypeUsageWalk UsageWalk = new(
        leafPredicate: IsClosedTypeOrVariant,
        includeDeclaredTypes: true,
        includeDelegateSignatures: false);

    /// <summary>closed type の利用判定</summary>
    /// <param name="compilation">検査対象 compilation</param>
    /// <returns>closed type を使う場合に true</returns>
    public bool UsesGuardedFeature(Compilation compilation) =>
        UsageWalk.UsesGuardedFeature(compilation);

    /// <summary>closed set 所属型の判定</summary>
    /// <param name="type">検査対象の型</param>
    /// <returns>closed set 所属型の場合に true</returns>
    private static bool IsClosedTypeOrVariant(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
            return false;

        for (var current = named; current is not null; current = current.BaseType)
        {
            if (DefinesClosedSet(current))
                return true;
        }

        return named.AllInterfaces.Any(DefinesClosedSet);
    }

    /// <summary>閉じた集合を宣言する型の判定</summary>
    /// <param name="type">検査対象の名前付き型</param>
    /// <returns>閉じた集合を宣言する場合に true</returns>
    private static bool DefinesClosedSet(INamedTypeSymbol type)
    {
        var attributes = type.GetAttributes();
        if (attributes.Any(attribute =>
                attribute.AttributeClass?.ToDisplayString() == ClosedUnionAttributeMetadataName))
        {
            return true;
        }

        return attributes.Any(attribute =>
                   attribute.AttributeClass?.ToDisplayString() == JsonPolymorphicAttributeMetadataName) &&
               attributes.Any(attribute =>
                   attribute.AttributeClass?.ToDisplayString() == JsonDerivedTypeAttributeMetadataName);
    }
}
