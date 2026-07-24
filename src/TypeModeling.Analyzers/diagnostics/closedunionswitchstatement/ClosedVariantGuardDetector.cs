using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Diagnostics.ClosedUnionSwitchStatement;

/// <summary>when guard に現れる閉じた variant の検出</summary>
internal static class ClosedVariantGuardDetector
{
    /// <summary>guard が判別する閉集合</summary>
    /// <param name="operation">検査対象の guard</param>
    /// <returns>guard から検出した閉集合</returns>
    internal static ITypeSymbol? Find(IOperation operation)
    {
        if (operation is IParenthesizedOperation parenthesized)
            return Find(parenthesized.Operand);

        if (operation is IConversionOperation conversion &&
            ValuePreservingWidening.Is(conversion))
        {
            return Find(conversion.Operand);
        }

        if (operation is IIsTypeOperation isType &&
            ClosedTypeSet.Canonical(isType.TypeOperand) is { } testedClosed &&
            !SymbolEqualityComparer.Default.Equals(testedClosed, isType.TypeOperand))
        {
            return testedClosed;
        }

        if (operation is IIsPatternOperation isPattern)
            return ClosedVariantPatternDetector.Find(isPattern.Pattern);

        if (operation is IBinaryOperation
            {
                OperatorKind: BinaryOperatorKind.Equals or BinaryOperatorKind.NotEquals,
                OperatorMethod: null,
            } equality)
        {
            return ClosedVariantValueDetector.Find(equality.LeftOperand) ??
                ClosedVariantValueDetector.Find(equality.RightOperand);
        }

        if (operation is IBinaryOperation
            {
                OperatorKind: BinaryOperatorKind.And or
                    BinaryOperatorKind.Or or
                    BinaryOperatorKind.ConditionalAnd or
                    BinaryOperatorKind.ConditionalOr,
                OperatorMethod: null,
            } logical)
        {
            return Find(logical.LeftOperand) ?? Find(logical.RightOperand);
        }

        return operation is IUnaryOperation
        {
            OperatorKind: UnaryOperatorKind.Not,
            OperatorMethod: null,
        } negation
            ? Find(negation.Operand)
            : null;
    }
}
