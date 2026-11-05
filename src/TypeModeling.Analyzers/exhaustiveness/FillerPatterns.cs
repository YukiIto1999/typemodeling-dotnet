using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Exhaustiveness;

/// <summary>閉じた型が取りえない値の領域を覆う pattern の生成</summary>
/// <remarks>閉じた型の全ての派生や member を扱った arm に、この pattern を足すと compiler が網羅と判定する</remarks>
internal static class FillerPatterns
{
    /// <summary>位置ごとの、存在しない値の領域を覆う pattern</summary>
    /// <param name="positions">pattern が型を調べる部分値の位置</param>
    /// <param name="typeText">型名を解決する switch の位置の文脈</param>
    /// <param name="compilation">派生を探す compilation</param>
    /// <returns>入力全体に当てる pattern の文字列</returns>
    internal static ImmutableArray<string> For(
        IEnumerable<PatternPosition> positions,
        TypeText typeText,
        Compilation compilation) =>
        [.. positions
            .SelectMany(position => Leaves(position.Type, typeText, compilation).Select(position.Path.Wrap))
            .Distinct()];

    /// <summary>部分値の位置に当てる、存在しない値の領域を覆う pattern</summary>
    /// <param name="type">その位置で pattern が受け取る型</param>
    /// <param name="typeText">型名を解決する switch の位置の文脈</param>
    /// <param name="compilation">派生を探す compilation</param>
    /// <returns>部分値に当てる pattern の文字列</returns>
    private static IEnumerable<string> Leaves(ITypeSymbol? type, TypeText typeText, Compilation compilation)
    {
        if (ClosedTypeSet.EffectiveClosedType(type) is not { } closedType)
            return [];

        var typeName = typeText.Render(type!);
        if (closedType.TypeKind == TypeKind.Enum)
            return EnumLeaf(typeName, closedType, typeText);

        return ClassLeaves(typeName, closedType, typeText, compilation);
    }

    /// <summary>閉じた enum の名前のない値を覆う pattern</summary>
    /// <param name="typeName">その位置の型の名前</param>
    /// <param name="closedEnum">閉じた enum</param>
    /// <param name="typeText">型名を解決する switch の位置の文脈</param>
    /// <returns>名前のない値を覆う pattern</returns>
    private static IEnumerable<string> EnumLeaf(string typeName, INamedTypeSymbol closedEnum, TypeText typeText)
    {
        var members = closedEnum.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => field.HasConstantValue)
            .Select(typeText.RenderEnumMember);
        return [$"{typeName} and not ({string.Join(" or ", members)})"];
    }

    /// <summary>閉じた class の、名前の挙がらない派生を覆う pattern</summary>
    /// <param name="typeName">その位置の型の名前</param>
    /// <param name="closedClass">閉じた class</param>
    /// <param name="typeText">型名を解決する switch の位置の文脈</param>
    /// <param name="compilation">派生を探す compilation</param>
    /// <returns>この型と、閉じた派生の型ごとの pattern。派生を挙げられない、または派生を持たないなら空</returns>
    private static IEnumerable<string> ClassLeaves(
        string typeName,
        INamedTypeSymbol closedClass,
        TypeText typeText,
        Compilation compilation)
    {
        var subtypes = PossibleSubtypes(closedClass, typeText, compilation);
        if (subtypes.IsEmpty)
            return [];

        var names = subtypes.Select(subtype => typeText.Render(subtype));
        var own = $"{typeName} and not ({string.Join(" or ", names)})";
        return subtypes
            .Where(ClosedTypeSet.IsClosedClass)
            .SelectMany(subtype => Leaves(subtype, typeText, compilation))
            .Prepend(own);
    }

    /// <summary>switch の位置で取りうる、閉じた class の直接の派生</summary>
    /// <param name="closedClass">閉じた class</param>
    /// <param name="typeText">参照の可否を判定する switch の位置の文脈</param>
    /// <param name="compilation">派生を探す compilation</param>
    /// <returns>取りうる派生の構築形。全ての派生を名前で挙げられなければ空</returns>
    private static ImmutableArray<INamedTypeSymbol> PossibleSubtypes(
        INamedTypeSymbol closedClass,
        TypeText typeText,
        Compilation compilation)
    {
        if (!ClosedSubtypes.TryGetDirectSubtypeDefinitions(closedClass.OriginalDefinition, compilation, out var definitions))
            return [];

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        foreach (var definition in definitions)
        {
            var candidate = SubtypeConstruction.Construct(definition, closedClass);
            if (candidate.Kind == SubtypeCandidateKind.Impossible)
                continue;

            if (candidate.Type is not { } subtype || !typeText.IsAccessible(subtype))
                return [];

            builder.Add(subtype);
        }

        return builder.ToImmutable();
    }
}
