namespace TypeModeling.Testing.Closure;

/// <summary>MSBuild 評価済みの managed project 出力</summary>
/// <param name="ProjectPath">repository root からの project 相対パス</param>
/// <param name="AssemblyName">MSBuild が評価した assembly 名</param>
/// <param name="TargetPath">MSBuild が評価した出力 assembly の絶対パス</param>
internal sealed record EvaluatedSolutionProject(
    string ProjectPath,
    string AssemblyName,
    string TargetPath);
