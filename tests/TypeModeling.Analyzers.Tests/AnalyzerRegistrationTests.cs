using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using TypeModeling.Analyzers.Diagnostics.ClosedEnum;
using TypeModeling.Analyzers.Diagnostics.ClosedType;
using TypeModeling.Analyzers.Diagnostics.MustUse;
using TypeModeling.Analyzers.Diagnostics.ResultUsage;
using TypeModeling.Analyzers.Diagnostics.UnionType;
using TypeModeling.Analyzers.Diagnostics.UnreachableArm;

namespace TypeModeling.Analyzers.Tests;

/// <summary>analyzer の並行実行と生成 code の扱いの登録</summary>
public sealed class AnalyzerRegistrationTests
{
    /// <summary>全ての analyzer の、並行実行の許可と生成 code を解析しない設定</summary>
    /// <returns>検証の完了</returns>
    [Test]
    public async Task Every_analyzer_runs_concurrently_and_skips_generated_code()
    {
        DiagnosticAnalyzer[] analyzers =
        [
            new ClosedEnumAnalyzer(),
            new ClosedTypeDeclarationAnalyzer(),
            new MustUseAnalyzer(),
            new ResultUsageAnalyzer(),
            new UnionTypeAnalyzer(),
            new UnreachableArmAnalyzer(),
        ];

        var registrations = analyzers.Select(analyzer =>
        {
            var context = new RecordingAnalysisContext();
            analyzer.Initialize(context);
            return $"{analyzer.GetType().Name} | {context.Concurrent} | {context.GeneratedCode}";
        });

        await Assert.That(registrations).IsEquivalentTo(
            analyzers.Select(analyzer => $"{analyzer.GetType().Name} | True | {GeneratedCodeAnalysisFlags.None}"));
    }

    /// <summary>登録された設定だけを記録し、解析の action を捨てる context</summary>
    private sealed class RecordingAnalysisContext : AnalysisContext
    {
        /// <summary>並行実行を許したか</summary>
        internal bool Concurrent { get; private set; }

        /// <summary>生成 code の扱いの設定。設定しなければ null</summary>
        internal GeneratedCodeAnalysisFlags? GeneratedCode { get; private set; }

        /// <inheritdoc/>
        public override void EnableConcurrentExecution() => Concurrent = true;

        /// <inheritdoc/>
        public override void ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags analysisMode) => GeneratedCode = analysisMode;

        /// <inheritdoc/>
        public override void RegisterCompilationStartAction(Action<CompilationStartAnalysisContext> action)
        {
            // 解析の action は実行しないので記録しない
        }

        /// <inheritdoc/>
        public override void RegisterCompilationAction(Action<CompilationAnalysisContext> action)
        {
            // 解析の action は実行しないので記録しない
        }

        /// <inheritdoc/>
        public override void RegisterSemanticModelAction(Action<SemanticModelAnalysisContext> action)
        {
            // 解析の action は実行しないので記録しない
        }

        /// <inheritdoc/>
        public override void RegisterSymbolAction(Action<SymbolAnalysisContext> action, params ImmutableArray<SymbolKind> symbolKinds)
        {
            // 解析の action は実行しないので記録しない
        }

        /// <inheritdoc/>
        public override void RegisterCodeBlockStartAction<TLanguageKindEnum>(Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action)
        {
            // 解析の action は実行しないので記録しない
        }

        /// <inheritdoc/>
        public override void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext> action)
        {
            // 解析の action は実行しないので記録しない
        }

        /// <inheritdoc/>
        public override void RegisterSyntaxTreeAction(Action<SyntaxTreeAnalysisContext> action)
        {
            // 解析の action は実行しないので記録しない
        }

        /// <inheritdoc/>
        public override void RegisterSyntaxNodeAction<TLanguageKindEnum>(
            Action<SyntaxNodeAnalysisContext> action,
            params ImmutableArray<TLanguageKindEnum> syntaxKinds)
        {
            // 解析の action は実行しないので記録しない
        }

        /// <inheritdoc/>
        public override void RegisterOperationAction(Action<OperationAnalysisContext> action, params ImmutableArray<OperationKind> operationKinds)
        {
            // 解析の action は実行しないので記録しない
        }
    }
}
