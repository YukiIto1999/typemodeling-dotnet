using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace TypeModeling.Analyzers.Diagnostics.Guard;

/// <summary>TypeModeling の診断を設定で弱めることの検出</summary>
/// <remarks>
/// この診断は NotConfigurable で、NoWarn、editorconfig、ruleset、#pragma では抑止も重大度の変更もできない。
/// 利用側が診断を無効にしても build は成功しない。assembly 全体を対象にする SuppressMessage だけは、compiler が
/// NotConfigurable の診断にも適用するので、この診断の ID を明示した場合に限り抑止できる
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiagnosticGuardAnalyzer : DiagnosticAnalyzer
{
    /// <summary>診断を弱める設定の診断の ID</summary>
    public const string DiagnosticId = "TYPMOD022";

    /// <summary>診断を弱める設定の診断</summary>
    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "TypeModeling の診断を弱める設定",
        messageFormat: "{0}",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: WellKnownDiagnosticTags.NotConfigurable
    );

    /// <summary>対応する診断規則</summary>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [Rule];

    /// <summary>設定の検査の登録</summary>
    /// <param name="context">解析登録 context</param>
    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterCompilationStartAction(start =>
        {
            var reported = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
            start.RegisterSyntaxTreeAction(tree => ScanTree(tree, start.Compilation, reported));
            start.RegisterCompilationEndAction(end => ScanCompilation(end, reported));
        });
    }

    /// <summary>source ごとの設定の検査</summary>
    /// <param name="context">syntax tree 解析 context</param>
    /// <param name="compilation">検査する compilation</param>
    /// <param name="reported">報告した message の集合</param>
    private static void ScanTree(
        SyntaxTreeAnalysisContext context,
        Compilation compilation,
        ConcurrentDictionary<string, byte> reported
    )
    {
        var top = Location.Create(context.Tree, default);
        void Report(Location location, string message) =>
            Emit(context.ReportDiagnostic, reported, location, message);

        var treeOptions = compilation.Options.SyntaxTreeOptionsProvider;
        if (treeOptions is not null)
        {
            foreach (var id in GuardedDiagnostics.Ids)
            {
                if (
                    treeOptions.TryGetDiagnosticValue(
                        context.Tree,
                        id,
                        context.CancellationToken,
                        out var severity
                    ) && GuardedDiagnostics.IsLowered(id, severity)
                )
                    Report(top, LoweredMessage(id, ".editorconfig の設定", severity));
            }
        }

        BulkSeverity(
            context.Options.AnalyzerConfigOptionsProvider.GetOptions(context.Tree),
            top,
            Report
        );
        PragmaGuard.Scan(context.Tree, Report, context.CancellationToken);
    }

    /// <summary>compilation 全体の設定の検査</summary>
    /// <param name="context">compilation 解析 context</param>
    /// <param name="reported">報告した message の集合</param>
    private static void ScanCompilation(
        CompilationAnalysisContext context,
        ConcurrentDictionary<string, byte> reported
    )
    {
        void Report(Location location, string message) =>
            Emit(context.ReportDiagnostic, reported, location, message);

        foreach (
            var pair in context.Compilation.Options.SpecificDiagnosticOptions.Where(pair =>
                GuardedDiagnostics.IsLowered(pair.Key, pair.Value)
            )
        )
        {
            Report(
                Location.None,
                LoweredMessage(pair.Key, "NoWarn など compiler の設定", pair.Value)
            );
        }

        var treeOptions = context.Compilation.Options.SyntaxTreeOptionsProvider;
        if (treeOptions is not null)
        {
            foreach (var id in GuardedDiagnostics.Ids)
            {
                if (
                    treeOptions.TryGetGlobalDiagnosticValue(
                        id,
                        context.CancellationToken,
                        out var severity
                    ) && GuardedDiagnostics.IsLowered(id, severity)
                )
                    Report(Location.None, LoweredMessage(id, "global config の設定", severity));
            }
        }

        BulkSeverity(
            context.Options.AnalyzerConfigOptionsProvider.GlobalOptions,
            Location.None,
            Report
        );
        foreach (
            var attribute in AssemblyWideSuppressions.Find(
                context.Compilation,
                context.CancellationToken
            )
        )
            Report(
                attribute.Location,
                $"assembly の SuppressMessage が '{attribute.Id}' を抑止している"
            );
    }

    /// <summary>診断を隠す一括の重大度の検査</summary>
    /// <param name="options">検査する設定</param>
    /// <param name="location">報告する位置</param>
    /// <param name="report">位置と message を受け取る処理</param>
    private static void BulkSeverity(
        AnalyzerConfigOptions options,
        Location location,
        Action<Location, string> report
    )
    {
        foreach (
            var key in new[]
            {
                "dotnet_analyzer_diagnostic.severity",
                "dotnet_analyzer_diagnostic.category-TypeModeling.severity",
            }
        )
        {
            if (
                options.TryGetValue(key, out var value)
                && GuardedDiagnostics.HidesDiagnostics(value)
            )
                report(
                    location,
                    $"{key} が {value.Trim().ToLowerInvariant()} なので、TypeModeling の診断が隠れる"
                );
        }
    }

    /// <summary>診断を既定より下げた設定の message</summary>
    /// <param name="id">診断の ID</param>
    /// <param name="source">設定の出どころ</param>
    /// <param name="severity">設定された報告</param>
    /// <returns>報告する message</returns>
    private static string LoweredMessage(string id, string source, ReportDiagnostic severity) =>
        $"'{id}' の重大度が {source}により {GuardedDiagnostics.Display(severity)} に下げられている";

    /// <summary>同じ message を一度だけ報告する</summary>
    /// <param name="report">診断の報告</param>
    /// <param name="reported">報告した message の集合</param>
    /// <param name="location">報告する位置</param>
    /// <param name="message">報告する message</param>
    private static void Emit(
        Action<Diagnostic> report,
        ConcurrentDictionary<string, byte> reported,
        Location location,
        string message
    )
    {
        if (reported.TryAdd(message, 0))
            report(Diagnostic.Create(Rule, location, message));
    }
}
