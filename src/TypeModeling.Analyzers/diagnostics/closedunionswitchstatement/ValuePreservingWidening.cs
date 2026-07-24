using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.Diagnostics.ClosedUnionSwitchStatement;

/// <summary>閉じた値の由来を保つ拡大変換規則</summary>
internal static class ValuePreservingWidening
{
    /// <summary>閉じた値の由来を保つ拡大変換</summary>
    /// <param name="conversion">検査対象の変換</param>
    /// <returns>由来を保つ変換の場合に true</returns>
    internal static bool Is(IConversionOperation conversion)
    {
        if (!conversion.Conversion.Exists || conversion.OperatorMethod is not null)
            return false;

        var source = conversion.Operand.Type;
        var target = conversion.Type;
        if (source is null || target is null)
            return false;
        if (SymbolEqualityComparer.Default.Equals(source, target))
            return true;

        if (source is INamedTypeSymbol
            {
                TypeKind: TypeKind.Enum,
                EnumUnderlyingType: { } underlyingType,
            } &&
            SymbolEqualityComparer.Default.Equals(underlyingType, target))
        {
            return true;
        }

        if (target.SpecialType == SpecialType.System_Object ||
            target.TypeKind is TypeKind.Dynamic or TypeKind.Interface)
        {
            return true;
        }

        if (target is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType: SpecialType.System_Nullable_T,
                TypeArguments.Length: 1,
            } nullable &&
            SymbolEqualityComparer.Default.Equals(nullable.TypeArguments[0], source))
        {
            return true;
        }

        for (var current = source.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, target))
                return true;
        }

        return false;
    }
}
