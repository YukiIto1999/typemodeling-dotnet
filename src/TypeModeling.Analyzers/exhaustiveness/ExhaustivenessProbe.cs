using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TypeModeling.Analyzers.Exhaustiveness;

/// <summary>存在しない値を覆う arm を足した switch 式を、同じ位置で compiler に再判定させる試験</summary>
/// <remarks>pattern の網羅の判定そのものは compiler が行うので、位置、プロパティ、タプル、入れ子、when の扱いが C# と一致する</remarks>
internal static class ExhaustivenessProbe
{
    /// <summary>試験用の switch 式を探す annotation の種類</summary>
    private const string ProbeAnnotationKind = "TypeModeling.ExhaustivenessProbe";

    /// <summary>tree の switch 式ごとに arm を足した compilation での診断</summary>
    /// <param name="compilation">元の compilation</param>
    /// <param name="tree">switch 式を含む tree</param>
    /// <param name="plans">switch 式ごとに足す pattern</param>
    /// <param name="cancellationToken">解析の cancellation token</param>
    /// <remarks>足す arm は元の arm より前に置き、網羅の判定と、閉じた型で到達できなくなった元の arm の判定を一度で行う</remarks>
    /// <returns>元の switch 式ごとの、試験の switch 式とその範囲の診断</returns>
    internal static ImmutableDictionary<SwitchExpressionSyntax, ProbeOutcome> Run(
        Compilation compilation,
        SyntaxTree tree,
        IReadOnlyDictionary<SwitchExpressionSyntax, ImmutableArray<string>> plans,
        CancellationToken cancellationToken)
    {
        var annotations = new Dictionary<SwitchExpressionSyntax, SyntaxAnnotation>();
        var root = tree.GetRoot(cancellationToken).ReplaceNodes(
            plans.Keys,
            (original, rewritten) =>
            {
                var annotation = new SyntaxAnnotation(ProbeAnnotationKind);
                annotations.Add(original, annotation);
                return WithFillers(rewritten, plans[original]).WithAdditionalAnnotations(annotation);
            });
        var probeTree = tree.WithRootAndOptions(root, tree.Options);
        var probeModel = compilation.ReplaceSyntaxTree(tree, probeTree).GetSemanticModel(probeTree);
        var probeRoot = probeTree.GetRoot(cancellationToken);

        var outcomes = ImmutableDictionary.CreateBuilder<SwitchExpressionSyntax, ProbeOutcome>();
        foreach (var (original, annotation) in annotations.Select(pair => (pair.Key, pair.Value)))
        {
            var probe = (SwitchExpressionSyntax)probeRoot.GetAnnotatedNodes(annotation).Single();
            var diagnostics = probeModel.GetDiagnostics(probe.Span, cancellationToken);
            outcomes.Add(original, new ProbeOutcome(probe, diagnostics, plans[original].Length));
        }

        return outcomes.ToImmutable();
    }

    /// <summary>pattern を throw の arm として足した switch 式</summary>
    /// <param name="switchExpression">arm を足す switch 式</param>
    /// <param name="fillers">足す arm の pattern</param>
    /// <returns>足した arm を先頭に置いた switch 式</returns>
    private static SwitchExpressionSyntax WithFillers(SwitchExpressionSyntax switchExpression, ImmutableArray<string> fillers)
    {
        // arm だけを取り出すので、仮の switch 式の閉じ括弧は書かない
        var parsed = (SwitchExpressionSyntax)SyntaxFactory.ParseExpression(
            "_ switch { " + string.Join(", ", fillers.Select(filler => filler + " => throw null!")));
        return switchExpression.WithArms(SeparatedByCommas([.. parsed.Arms, .. switchExpression.Arms]));
    }

    /// <summary>arm を comma で区切った並び</summary>
    /// <param name="arms">並べる arm</param>
    /// <returns>comma で区切った arm の並び</returns>
    private static SeparatedSyntaxList<SwitchExpressionArmSyntax> SeparatedByCommas(
        ImmutableArray<SwitchExpressionArmSyntax> arms) =>
        SyntaxFactory.SeparatedList(
            arms,
            Enumerable.Repeat(SyntaxFactory.Token(SyntaxKind.CommaToken), arms.Length - 1));
}
