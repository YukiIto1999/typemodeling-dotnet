using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers.Diagnostics.Guard;

namespace TypeModeling.Analyzers.Tests;

/// <summary>診断の設定を compiler と同じ解析で与えて、診断を弱める設定の検出を走らせる実行基盤</summary>
internal static class DiagnosticGuardFixture
{
    /// <summary>検査対象の source の path。editorconfig の section はこの path に適用される</summary>
    private const string SourcePath = "/project/Source.cs";

    /// <summary>設定を与えた compilation に診断を弱める設定の検出を当てた、その検出の診断の message</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <param name="settings">compiler に与える診断の設定</param>
    /// <returns>検出した診断の message</returns>
    internal static async Task<string[]> Messages(string source, GuardSettings? settings = null)
    {
        var diagnostics = await Run(source, settings ?? new GuardSettings());
        return
        [
            .. diagnostics
                .Where(diagnostic => diagnostic.Id == DiagnosticGuardAnalyzer.DiagnosticId)
                .Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture))
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>設定を与えた compilation に診断を弱める設定の検出を当てた全ての診断</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <param name="settings">compiler に与える診断の設定</param>
    /// <returns>検出器が報告した診断</returns>
    internal static async Task<ImmutableArray<Diagnostic>> Run(
        string source,
        GuardSettings settings
    )
    {
        var configs = new List<AnalyzerConfig>
        {
            AnalyzerConfig.Parse(settings.EditorConfig ?? "", "/project/.editorconfig"),
        };
        if (settings.GlobalConfig is not null)
            configs.Add(AnalyzerConfig.Parse(settings.GlobalConfig, "/project/.globalconfig"));
        var set = AnalyzerConfigSet.Create(configs);
        var provider = new SetOptionsProvider(set);
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            .WithSyntaxTreeOptionsProvider(new SetTreeOptionsProvider(set))
            .WithSpecificDiagnosticOptions(settings.Specific);
        var tree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(LanguageVersion.Latest),
            SourcePath
        );
        var compilation = CSharpCompilation.Create(
            "guard-probe",
            [tree],
            CompilationReferences.Platform,
            options
        );
        var withAnalyzers = compilation.WithAnalyzers(
            [new DiagnosticGuardAnalyzer()],
            new CompilationWithAnalyzersOptions(
                new AnalyzerOptions([], provider),
                onAnalyzerException: null,
                concurrentAnalysis: false,
                logAnalyzerExecutionTime: false,
                reportSuppressedDiagnostics: false
            )
        );
        return await withAnalyzers.GetAnalyzerDiagnosticsAsync();
    }

    /// <summary>compiler に与える診断の設定</summary>
    internal sealed record GuardSettings
    {
        /// <summary>source に適用する .editorconfig の内容</summary>
        internal string? EditorConfig { get; init; }

        /// <summary>全ての source に適用する global config の内容。is_global = true を含める</summary>
        internal string? GlobalConfig { get; init; }

        /// <summary>NoWarn や WarningsAsErrors が compiler へ渡す診断ごとの報告</summary>
        internal ImmutableDictionary<string, ReportDiagnostic> Specific { get; init; } =
            ImmutableDictionary<string, ReportDiagnostic>.Empty;
    }

    /// <summary>AnalyzerConfigSet の解析結果を、compiler が analyzer へ渡す形で返す提供元</summary>
    private sealed class SetOptionsProvider : AnalyzerConfigOptionsProvider
    {
        /// <summary>解析済みの設定</summary>
        private readonly AnalyzerConfigSet _set;

        /// <summary>設定の提供元の構築</summary>
        /// <param name="set">解析済みの設定</param>
        internal SetOptionsProvider(AnalyzerConfigSet set) => _set = set;

        /// <summary>全ての source に共通の設定</summary>
        public override AnalyzerConfigOptions GlobalOptions =>
            new DictionaryOptions(_set.GlobalConfigOptions.AnalyzerOptions);

        /// <summary>source ごとの設定</summary>
        /// <param name="tree">対象の syntax tree</param>
        /// <returns>tree に適用する設定</returns>
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) =>
            new DictionaryOptions(_set.GetOptionsForSourcePath(tree.FilePath).AnalyzerOptions);

        /// <summary>additional file ごとの設定</summary>
        /// <param name="textFile">対象の additional file</param>
        /// <returns>空の設定</returns>
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
            new DictionaryOptions(ImmutableDictionary<string, string>.Empty);
    }

    /// <summary>key と値の対応から作る設定</summary>
    private sealed class DictionaryOptions : AnalyzerConfigOptions
    {
        /// <summary>設定の key と値</summary>
        private readonly ImmutableDictionary<string, string> _values;

        /// <summary>設定の構築</summary>
        /// <param name="values">設定の key と値</param>
        internal DictionaryOptions(ImmutableDictionary<string, string> values) => _values = values;

        /// <summary>key に対応する値の取得</summary>
        /// <param name="key">設定の key</param>
        /// <param name="value">見つけた値</param>
        /// <returns>見つけた場合に true</returns>
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) =>
            _values.TryGetValue(key, out value);
    }

    /// <summary>AnalyzerConfigSet の診断の重大度を、compiler が syntax tree ごとに渡す形で返す提供元</summary>
    private sealed class SetTreeOptionsProvider : SyntaxTreeOptionsProvider
    {
        /// <summary>解析済みの設定</summary>
        private readonly AnalyzerConfigSet _set;

        /// <summary>設定の提供元の構築</summary>
        /// <param name="set">解析済みの設定</param>
        internal SetTreeOptionsProvider(AnalyzerConfigSet set) => _set = set;

        /// <summary>生成 code の判定</summary>
        /// <param name="tree">対象の syntax tree</param>
        /// <param name="cancellationToken">中止 token</param>
        /// <returns>常に不明</returns>
        public override GeneratedKind IsGenerated(
            SyntaxTree tree,
            CancellationToken cancellationToken
        ) => GeneratedKind.Unknown;

        /// <summary>source に適用する診断の重大度の取得</summary>
        /// <param name="tree">対象の syntax tree</param>
        /// <param name="diagnosticId">診断の ID</param>
        /// <param name="cancellationToken">中止 token</param>
        /// <param name="severity">見つけた重大度</param>
        /// <returns>設定がある場合に true</returns>
        public override bool TryGetDiagnosticValue(
            SyntaxTree tree,
            string diagnosticId,
            CancellationToken cancellationToken,
            out ReportDiagnostic severity
        ) =>
            _set.GetOptionsForSourcePath(tree.FilePath)
                .TreeOptions.TryGetValue(diagnosticId, out severity);

        /// <summary>全ての source に共通の診断の重大度の取得</summary>
        /// <param name="diagnosticId">診断の ID</param>
        /// <param name="cancellationToken">中止 token</param>
        /// <param name="severity">見つけた重大度</param>
        /// <returns>設定がある場合に true</returns>
        public override bool TryGetGlobalDiagnosticValue(
            string diagnosticId,
            CancellationToken cancellationToken,
            out ReportDiagnostic severity
        ) => _set.GlobalConfigOptions.TreeOptions.TryGetValue(diagnosticId, out severity);
    }
}
