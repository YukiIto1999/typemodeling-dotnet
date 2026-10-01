using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Diagnostics.ClosedUnionSwitchStatement;

/// <summary>switch case に現れる閉じた variant の検出</summary>
internal static class ClosedVariantCaseDetector
{
    /// <summary>case clause が判別する閉集合</summary>
    /// <param name="operation">検査対象の switch</param>
    /// <returns>case clause から検出した閉集合</returns>
    [SuppressMessage("Critical Code Smell", "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "S3776 の導入前からある複雑度 19 の既存違反。基線台帳 S3776-002 に記録し、15 以下へ分割した時点で抑止を外す")]
    internal static ITypeSymbol? Find(ISwitchOperation operation)
    {
        foreach (var switchCase in operation.Cases)
        {
            foreach (var clause in switchCase.Clauses)
            {
                if (clause is ISingleValueCaseClauseOperation singleValue &&
                    ClosedTypeSet.Canonical(singleValue.Value.Type) is
                    { TypeKind: TypeKind.Enum } closedEnum)
                {
                    return closedEnum;
                }

                if (clause is ISingleValueCaseClauseOperation convertedValue &&
                    ClosedVariantValueDetector.Find(convertedValue.Value) is { } convertedVariant)
                {
                    return convertedVariant;
                }

                if (clause is IPatternCaseClauseOperation patternClause &&
                    ClosedVariantPatternDetector.Find(patternClause.Pattern) is { } closedVariant)
                {
                    return closedVariant;
                }

                if (clause is IPatternCaseClauseOperation { Guard: { } guard } &&
                    ClosedVariantGuardDetector.Find(guard) is { } guardedVariant)
                {
                    return guardedVariant;
                }
            }
        }

        return null;
    }
}
