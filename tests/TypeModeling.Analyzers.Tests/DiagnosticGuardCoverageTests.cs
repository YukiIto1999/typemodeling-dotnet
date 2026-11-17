using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using TypeModeling.Analyzers.Diagnostics.ClosedEnum;
using TypeModeling.Analyzers.Diagnostics.ClosedType;
using TypeModeling.Analyzers.Diagnostics.ExplicitCase;
using TypeModeling.Analyzers.Diagnostics.Guard;
using TypeModeling.Analyzers.Diagnostics.MustUse;
using TypeModeling.Analyzers.Diagnostics.ResultUsage;
using TypeModeling.Analyzers.Diagnostics.UnionType;
using TypeModeling.Analyzers.Diagnostics.UnreachableArm;
using TypeModeling.Analyzers.Diagnostics.ValueObject;
using TypeModeling.Analyzers.Suppression.Exhaustiveness;
using static TypeModeling.Analyzers.Tests.DiagnosticGuardFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>診断を弱める設定の検出が、公開する診断の全てを覆うことの確認</summary>
public sealed class DiagnosticGuardCoverageTests
{
    /// <summary>ValueObject の生成が報告する診断。analyzer の SupportedDiagnostics には現れない</summary>
    private static readonly string[] GeneratorDiagnosticIds = ["TYPMOD002"];

    /// <summary>既定で無効な診断。利用側が有効にした時にだけ働くので守らない</summary>
    private const string OptInDiagnosticId = "TYPMOD021";

    /// <summary>診断を弱める設定の診断が公開する契約</summary>
    [Test]
    public async Task The_guard_publishes_its_diagnostic()
    {
        var rule = new DiagnosticGuardAnalyzer().SupportedDiagnostics.Single();

        await Assert
            .That(
                $"{rule.Id} | {rule.DefaultSeverity} | {rule.Title} | {rule.MessageFormat} | {rule.Category}"
            )
            .IsEqualTo("TYPMOD022 | Error | TypeModeling の診断を弱める設定 | {0} | TypeModeling");
    }

    /// <summary>公開する診断と抑止の全てを、設定で none にすると検出する</summary>
    [Test]
    public async Task Every_published_diagnostic_is_guarded_except_the_opt_in_one()
    {
        DiagnosticAnalyzer[] analyzers =
        [
            new ClosedEnumAnalyzer(),
            new ClosedTypeDeclarationAnalyzer(),
            new DiagnosticGuardAnalyzer(),
            new ExplicitCaseAnalyzer(),
            new MustUseAnalyzer(),
            new ResultUsageAnalyzer(),
            new UnionTypeAnalyzer(),
            new UnreachableArmAnalyzer(),
            new ValueObjectConstructionAnalyzer(),
            new ValueObjectShapeAnalyzer(),
        ];
        var published = analyzers
            .SelectMany(analyzer => analyzer.SupportedDiagnostics.Select(rule => rule.Id))
            .Concat(new ExhaustivenessSuppressor().SupportedSuppressions.Select(rule => rule.Id))
            .Concat(GeneratorDiagnosticIds)
            .Where(id => id != OptInDiagnosticId)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var unguarded = new List<string>();
        foreach (var id in published)
        {
            var messages = await Messages(
                "class C;",
                new GuardSettings
                {
                    GlobalConfig = $"is_global = true\ndotnet_diagnostic.{id}.severity = none\n",
                }
            );
            if (messages.Length != 1)
                unguarded.Add(id);
        }

        await Assert.That(unguarded).IsEmpty();
        await Assert
            .That(published)
            .Contains("TYPMOD002")
            .And.Contains("TYPMOD023")
            .And.Contains("TYPMOD024")
            .And.Contains("TYPMOD022")
            .And.Contains("TYPMOD101");
    }
}
