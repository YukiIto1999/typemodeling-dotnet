using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Exhaustiveness;

/// <summary>switch の位置で解決できる型と member の名前</summary>
/// <param name="model">switch を含む tree の semantic model</param>
/// <param name="position">名前を解決する switch の位置</param>
internal sealed class TypeText(SemanticModel model, int position)
{
    /// <summary>型 pattern に書ける、nullable の注釈を除いた名前の書式</summary>
    private static readonly SymbolDisplayFormat PatternTypeFormat = SymbolDisplayFormat.MinimallyQualifiedFormat
        .WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <summary>switch の位置で symbol が参照できるかの判定</summary>
    /// <param name="symbol">参照の可否を調べる symbol</param>
    /// <returns>switch の位置から参照できる場合に true</returns>
    internal bool IsAccessible(ISymbol symbol) => model.IsAccessible(position, symbol);

    /// <summary>型 pattern に書く型の名前</summary>
    /// <param name="type">名前にする型</param>
    /// <returns>switch の位置で解決できる名前</returns>
    /// <remarks>名前で書けない型の名前は足した arm の compile のエラーになり、試験はその switch 式を網羅とみなさない</remarks>
    internal string Render(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            return Render(nullable.TypeArguments[0]);

        return type
            .WithNullableAnnotation(NullableAnnotation.NotAnnotated)
            .ToMinimalDisplayString(model, position, PatternTypeFormat);
    }

    /// <summary>enum の member を指す定数 pattern</summary>
    /// <param name="member">enum の名前付きの member</param>
    /// <returns>switch の位置で解決できる member の参照</returns>
    /// <remarks>@ はどの識別子にも付けられるので、keyword と同じ名前かを問わず付ける</remarks>
    internal string RenderEnumMember(IFieldSymbol member) => Render(member.ContainingType) + ".@" + member.Name;
}
