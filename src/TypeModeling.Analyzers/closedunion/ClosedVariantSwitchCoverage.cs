using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TypeModeling.Analyzers.ClosedUnion;

/// <summary>閉じた variant の switch 式包含判定</summary>
internal static class ClosedVariantSwitchCoverage
{
    /// <summary>switch 式が全 variant を含むかの判定</summary>
    /// <param name="governingType">switch 式の governing type</param>
    /// <param name="switchExpression">検査対象の switch 式</param>
    /// <param name="model">switch 式の semantic model</param>
    /// <param name="cancellationToken">解析の cancellation token</param>
    /// <returns>全 variant を含む場合に true</returns>
    internal static bool CoversEveryVariant(
        ITypeSymbol governingType,
        SwitchExpressionSyntax switchExpression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var variants = ClosedTypeSet.Variants(governingType);
        if (variants.IsEmpty)
            return false;

        var matched = MatchedVariantDefinitions(
            switchExpression,
            model,
            cancellationToken);
        return variants.All(variant =>
            matched.Contains(variant, SymbolEqualityComparer.Default));
    }

    /// <summary>guard のない arm が照合する variant 定義</summary>
    /// <param name="switchExpression">検査対象の switch 式</param>
    /// <param name="model">switch 式の semantic model</param>
    /// <param name="cancellationToken">解析の cancellation token</param>
    /// <returns>guard のない arm が照合する variant 定義</returns>
    private static ImmutableHashSet<INamedTypeSymbol> MatchedVariantDefinitions(
        SwitchExpressionSyntax switchExpression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var matched =
            ImmutableHashSet.CreateBuilder<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var arm in switchExpression.Arms)
        {
            if (arm.WhenClause is not null)
                continue;

            foreach (var matchedType in PatternMatchedTypes(
                arm.Pattern,
                model,
                cancellationToken))
            {
                matched.Add((INamedTypeSymbol)matchedType.OriginalDefinition);
            }
        }

        return matched.ToImmutable();
    }

    /// <summary>pattern が照合する型</summary>
    /// <param name="pattern">検査対象の pattern</param>
    /// <param name="model">pattern の semantic model</param>
    /// <param name="cancellationToken">解析の cancellation token</param>
    /// <returns>pattern が照合する型</returns>
    private static IEnumerable<ITypeSymbol> PatternMatchedTypes(
        PatternSyntax pattern,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        switch (pattern)
        {
            case DeclarationPatternSyntax declaration
                when model.GetTypeInfo(declaration.Type, cancellationToken).Type is
                    { } declarationType:
                yield return declarationType;
                break;
            case TypePatternSyntax typePattern
                when model.GetTypeInfo(typePattern.Type, cancellationToken).Type is
                    { } typePatternType:
                yield return typePatternType;
                break;
            case RecursivePatternSyntax
                {
                    Type: { } type,
                    PropertyPatternClause: null,
                    PositionalPatternClause: null,
                }
                when model.GetTypeInfo(type, cancellationToken).Type is { } recursiveType:
                yield return recursiveType;
                break;
            case ConstantPatternSyntax constant
                when model.GetSymbolInfo(constant.Expression, cancellationToken).Symbol is
                    ITypeSymbol constantType:
                yield return constantType;
                break;
            case BinaryPatternSyntax binary:
                foreach (var left in PatternMatchedTypes(
                    binary.Left,
                    model,
                    cancellationToken))
                {
                    yield return left;
                }
                foreach (var right in PatternMatchedTypes(
                    binary.Right,
                    model,
                    cancellationToken))
                {
                    yield return right;
                }
                break;
            case ParenthesizedPatternSyntax parenthesized:
                foreach (var inner in PatternMatchedTypes(
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
