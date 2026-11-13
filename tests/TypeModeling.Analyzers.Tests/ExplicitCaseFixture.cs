using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers.Diagnostics.ExplicitCase;
using TypeModeling.Analyzers.Suppression.Exhaustiveness;

namespace TypeModeling.Analyzers.Tests;

/// <summary>閉じた型の case を挙げる要求の診断の実行基盤と、検査対象の source</summary>
internal static class ExplicitCaseFixture
{
    /// <summary>診断の ID</summary>
    internal const string Id = "TYPMOD021";

    /// <summary>網羅していない switch 式の compiler 診断の ID</summary>
    private static readonly string[] MissingCaseIds = ["CS8509", "CS8524", "CS8846"];

    /// <summary>閉じた型を宣言する source への診断</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <param name="enabled">診断を compilation の設定で有効にする場合に true</param>
    /// <param name="references">追加で参照する assembly</param>
    /// <returns>抑止された診断を含む全ての診断</returns>
    internal static Task<ImmutableArray<Diagnostic>> RunClosed(
        string source,
        bool enabled = true,
        params MetadataReference[] references) =>
        Analyze(ClosedTypeDiagnosticFixture.ClosedCompilation(source, references), enabled);

    /// <summary>union の generator を通した source への診断</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <returns>抑止された診断を含む全ての診断</returns>
    internal static Task<ImmutableArray<Diagnostic>> RunUnion(string source) =>
        Analyze(UnionFixture.Generate(source, LanguageVersion.CSharp14), enabled: true);

    /// <summary>診断が指す source の文字列</summary>
    /// <param name="diagnostics">検査結果の診断</param>
    /// <returns>抑止されていない診断の位置の source の文字列</returns>
    internal static IEnumerable<string> Flagged(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics
            .Where(diagnostic => diagnostic.Id == Id && !diagnostic.IsSuppressed)
            .Select(diagnostic => diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));

    /// <summary>診断が報せた message</summary>
    /// <param name="diagnostics">検査結果の診断</param>
    /// <returns>抑止されていない診断の message</returns>
    internal static IEnumerable<string> ReportedMessages(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics
            .Where(diagnostic => diagnostic.Id == Id && !diagnostic.IsSuppressed)
            .Select(diagnostic => diagnostic.GetMessage(CultureInfo.InvariantCulture));

    /// <summary>診断が case を持つ型として挙げるはずの message</summary>
    /// <param name="diagnostics">検査結果の診断。一つ以上の診断を含む</param>
    /// <param name="families">診断が挙げる case を持つ型の名前</param>
    /// <returns>診断の書式に型の名前を当てた message</returns>
    internal static IEnumerable<string> MessagesFor(ImmutableArray<Diagnostic> diagnostics, params string[] families)
    {
        var format = diagnostics.First(diagnostic => diagnostic.Id == Id).Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture);
        return families.Select(family => string.Format(CultureInfo.InvariantCulture, format, family));
    }

    /// <summary>この診断以外の、エラーの重大度の診断</summary>
    /// <param name="diagnostics">検査結果の診断</param>
    /// <returns>compiler と他の analyzer のエラー</returns>
    internal static IEnumerable<Diagnostic> CompilerErrors(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id != Id);

    /// <summary>compiler が出す、抑止されていない case の欠落の診断</summary>
    /// <param name="diagnostics">検査結果の診断</param>
    /// <returns>case の欠落を報せる診断</returns>
    internal static IEnumerable<Diagnostic> MissingCases(ImmutableArray<Diagnostic> diagnostics) =>
        diagnostics.Where(diagnostic => MissingCaseIds.Contains(diagnostic.Id) && !diagnostic.IsSuppressed);

    /// <summary>三つの派生と、派生を足せる位置を持つ閉じた record の宣言</summary>
    /// <param name="extraVariants">足す派生の宣言</param>
    /// <returns>file 全体の C# source の前半</returns>
    internal static string Payment(string extraVariants = "") => $$"""
        using TypeModeling.Domain;

        namespace Probe;

        [Closed]
        public abstract record PaymentState
        {
            private PaymentState() { }
            public string Label => GetType().Name;
            public sealed record Pending : PaymentState;
            public sealed record Settled(bool Refunded) : PaymentState;
            public sealed record Failed(string Reason) : PaymentState;
            {{extraVariants}}
        }
        """;

    /// <summary>決済の状態への switch 式を持つ source</summary>
    /// <param name="arms">switch 式の arm</param>
    /// <param name="extraVariants">足す派生の宣言</param>
    /// <returns>検査対象の C# source</returns>
    internal static string PaymentSwitch(string arms, string extraVariants = "") => Payment(extraVariants) + $$"""

        internal static class Handler
        {
            internal static int Describe(PaymentState state, bool flag) => state switch
            {
                {{arms}}
            };
        }
        """;

    /// <summary>決済の状態への switch 文を持つ source</summary>
    /// <param name="sections">switch 文の section</param>
    /// <param name="extraVariants">足す派生の宣言</param>
    /// <returns>検査対象の C# source</returns>
    internal static string PaymentStatement(string sections, string extraVariants = "") => Payment(extraVariants) + $$"""

        internal static class Handler
        {
            internal static int Describe(PaymentState state, bool flag)
            {
                var result = 0;
                switch (state)
                {
                    {{sections}}
                }

                return result;
            }
        }
        """;

    /// <summary>診断を有効にした設定と、既定のままの設定での analyzer の適用</summary>
    /// <param name="compilation">検査対象の compilation</param>
    /// <param name="enabled">診断を compilation の設定で有効にする場合に true</param>
    /// <returns>抑止された診断を含む全ての診断</returns>
    private static async Task<ImmutableArray<Diagnostic>> Analyze(Compilation compilation, bool enabled)
    {
        var configured = enabled
            ? compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
                new Dictionary<string, ReportDiagnostic> { [Id] = ReportDiagnostic.Error }))
            : compilation;
        var options = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: null,
            concurrentAnalysis: false,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: true);

        return await configured
            .WithAnalyzers([new ExhaustivenessSuppressor(), new ExplicitCaseAnalyzer()], options)
            .GetAllDiagnosticsAsync();
    }
}
