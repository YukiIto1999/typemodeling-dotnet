using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.Exhaustiveness;

/// <summary>switch 式ごとに足す、存在しない値を覆う pattern の計画</summary>
internal static class ExhaustivenessPlans
{
    /// <summary>覆う pattern を持つ switch 式だけの計画</summary>
    /// <param name="switchExpressions">計画を立てる switch 式</param>
    /// <param name="model">switch 式を含む tree の semantic model</param>
    /// <param name="cancellationToken">解析の cancellation token</param>
    /// <returns>switch 式から足す pattern への対応</returns>
    internal static ImmutableDictionary<SwitchExpressionSyntax, ImmutableArray<string>> For(
        IEnumerable<SwitchExpressionSyntax> switchExpressions,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        var plans = ImmutableDictionary.CreateBuilder<SwitchExpressionSyntax, ImmutableArray<string>>();
        foreach (var switchExpression in switchExpressions.Distinct())
        {
            var fillers = For(switchExpression, model, cancellationToken);
            if (!fillers.IsEmpty)
                plans.Add(switchExpression, fillers);
        }

        return plans.ToImmutable();
    }

    /// <summary>switch 式に足す、存在しない値を覆う pattern</summary>
    /// <param name="switchExpression">計画を立てる switch 式</param>
    /// <param name="model">switch 式を含む tree の semantic model</param>
    /// <param name="cancellationToken">解析の cancellation token</param>
    /// <returns>足す pattern。閉じた型を調べない switch 式なら空</returns>
    private static ImmutableArray<string> For(
        SwitchExpressionSyntax switchExpression,
        SemanticModel model,
        CancellationToken cancellationToken)
    {
        if (model.GetOperation(switchExpression, cancellationToken) is not ISwitchExpressionOperation operation)
            return [];

        var typeText = new TypeText(model, switchExpression.SpanStart);
        var positions = PositionCollector.Collect(operation, typeText);
        return FillerPatterns.For(positions, typeText, model.Compilation);
    }
}
