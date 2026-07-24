using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TypeModeling.Analyzers.Suppression.ClosedUnionExhaustiveness;

/// <summary>閉じた enum の switch 式包含判定</summary>
internal static class ClosedEnumSwitchCoverage
{
    /// <summary>switch 式が全 named member を含むかの判定</summary>
    /// <param name="governingType">switch 式の governing type</param>
    /// <param name="switchExpression">検査対象の switch 式</param>
    /// <param name="model">switch 式の semantic model</param>
    /// <param name="cancellationToken">解析の cancellation token</param>
    /// <returns>全 named member を含む場合に true</returns>
    internal static bool CoversEveryNamedMember(
        ITypeSymbol governingType,
        SwitchExpressionSyntax switchExpression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (governingType.TypeKind != TypeKind.Enum)
            return false;

        var members = governingType.OriginalDefinition
            .GetMembers()
            .OfType<IFieldSymbol>()
            .Where(field => field.ConstantValue is not null)
            .ToImmutableArray();
        if (members.IsEmpty)
            return false;

        var matched = MatchedEnumMembers(
            switchExpression,
            model,
            cancellationToken);
        return members.All(member =>
            matched.Contains(member, SymbolEqualityComparer.Default));
    }

    /// <summary>guard のない arm が照合する enum field</summary>
    /// <param name="switchExpression">検査対象の switch 式</param>
    /// <param name="model">switch 式の semantic model</param>
    /// <param name="cancellationToken">解析の cancellation token</param>
    /// <returns>guard のない arm が照合する enum field</returns>
    private static ImmutableHashSet<IFieldSymbol> MatchedEnumMembers(
        SwitchExpressionSyntax switchExpression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var matched =
            ImmutableHashSet.CreateBuilder<IFieldSymbol>(SymbolEqualityComparer.Default);

        foreach (var arm in switchExpression.Arms)
        {
            if (arm.WhenClause is not null)
                continue;

            foreach (var field in PatternMatchedFields(
                arm.Pattern,
                model,
                cancellationToken))
            {
                matched.Add(field);
            }
        }

        return matched.ToImmutable();
    }

    /// <summary>pattern が照合する enum field</summary>
    /// <param name="pattern">検査対象の pattern</param>
    /// <param name="model">pattern の semantic model</param>
    /// <param name="cancellationToken">解析の cancellation token</param>
    /// <returns>pattern が照合する enum field</returns>
    private static IEnumerable<IFieldSymbol> PatternMatchedFields(
        PatternSyntax pattern,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        switch (pattern)
        {
            case ConstantPatternSyntax constant
                when model.GetSymbolInfo(constant.Expression, cancellationToken).Symbol is
                    IFieldSymbol field:
                yield return field;
                break;
            case BinaryPatternSyntax binary:
                foreach (var left in PatternMatchedFields(
                    binary.Left,
                    model,
                    cancellationToken))
                {
                    yield return left;
                }
                foreach (var right in PatternMatchedFields(
                    binary.Right,
                    model,
                    cancellationToken))
                {
                    yield return right;
                }
                break;
            case ParenthesizedPatternSyntax parenthesized:
                foreach (var inner in PatternMatchedFields(
                    parenthesized.Pattern,
                    model,
                    cancellationToken))
                {
                    yield return inner;
                }
                break;
        }
    }
}
