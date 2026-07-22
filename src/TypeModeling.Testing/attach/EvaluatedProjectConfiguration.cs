using Microsoft.CodeAnalysis.CSharp;

namespace TypeModeling.Testing.Attach;

/// <summary>MSBuild 評価済み project 構成</summary>
/// <param name="Configuration">Configuration 値</param>
/// <param name="Platform">Platform 値</param>
/// <param name="TargetFramework">TargetFramework 値</param>
/// <param name="RuntimeIdentifier">RuntimeIdentifier 値</param>
/// <param name="Label">違反表示用の構成 label</param>
/// <param name="NoWarn">評価済み NoWarn 値</param>
/// <param name="RunAnalyzers">評価済み RunAnalyzers 値</param>
/// <param name="RunAnalyzersDuringBuild">評価済み RunAnalyzersDuringBuild 値</param>
/// <param name="ProjectReferences">評価済み ProjectReference 集合</param>
/// <param name="CompilePaths">評価済み Compile path 集合</param>
/// <param name="EditorConfigPaths">評価済み EditorConfigFiles path 集合</param>
/// <param name="ParseOptions">評価済み C# parse option</param>
internal sealed record EvaluatedProjectConfiguration(
    string Configuration,
    string Platform,
    string TargetFramework,
    string RuntimeIdentifier,
    string Label,
    string NoWarn,
    string RunAnalyzers,
    string RunAnalyzersDuringBuild,
    IReadOnlyList<EvaluatedProjectReference> ProjectReferences,
    IReadOnlyList<string> CompilePaths,
    IReadOnlyList<string> EditorConfigPaths,
    CSharpParseOptions ParseOptions);
