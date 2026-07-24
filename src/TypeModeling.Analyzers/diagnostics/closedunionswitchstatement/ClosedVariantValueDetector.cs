using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Diagnostics.ClosedUnionSwitchStatement;

/// <summary>case 値に現れる閉じた variant の検出</summary>
internal static class ClosedVariantValueDetector
{
    /// <summary>値が表す閉集合</summary>
    /// <param name="operation">検査対象の値</param>
    /// <returns>値から検出した閉集合</returns>
    internal static ITypeSymbol? Find(IOperation operation)
    {
        while (operation is IParenthesizedOperation parenthesized)
            operation = parenthesized.Operand;

        if (operation is IFieldReferenceOperation field &&
            ClosedTypeSet.Canonical(field.Field.ContainingType) is
                { TypeKind: TypeKind.Enum } fieldEnum)
        {
            return fieldEnum;
        }

        if (ClosedTypeSet.Canonical(operation.Type) is { } closed &&
            !SymbolEqualityComparer.Default.Equals(closed, operation.Type))
        {
            return closed;
        }

        return operation is IConversionOperation conversion &&
            ValuePreservingWidening.Is(conversion)
                ? Find(conversion.Operand)
                : null;
    }
}
