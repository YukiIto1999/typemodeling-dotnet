using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.ClosedUnion;

/// <summary>case pattern に現れる閉じた variant の検出</summary>
internal static class ClosedVariantPatternDetector
{
    /// <summary>pattern が判別する閉集合</summary>
    /// <param name="operation">検査対象の pattern</param>
    /// <returns>pattern から検出した閉集合</returns>
    internal static ITypeSymbol? Find(IOperation operation)
    {
        if (operation is ITypePatternOperation typePattern &&
            ClosedTypeSet.Canonical(typePattern.MatchedType) is { } matchedClosed &&
            !SymbolEqualityComparer.Default.Equals(matchedClosed, typePattern.MatchedType))
        {
            return matchedClosed;
        }

        if (operation is IConstantPatternOperation constantPattern &&
            ClosedVariantValueDetector.Find(constantPattern.Value) is { } constantVariant)
        {
            return constantVariant;
        }

        if (operation is IPatternOperation pattern &&
            ClosedTypeSet.Canonical(pattern.NarrowedType) is { } closed &&
            !SymbolEqualityComparer.Default.Equals(closed, pattern.NarrowedType))
        {
            return closed;
        }

        if (operation is not IPatternOperation and not IPropertySubpatternOperation)
            return null;

        foreach (var child in operation.ChildOperations)
        {
            if (Find(child) is { } nested)
                return nested;
        }

        return null;
    }
}
