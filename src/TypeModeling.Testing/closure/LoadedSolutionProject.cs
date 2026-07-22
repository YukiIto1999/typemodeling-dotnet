using System.Reflection;

namespace TypeModeling.Testing.Closure;

/// <summary>solution 閉包からロードした managed project</summary>
/// <param name="ProjectPath">repository root からの project 相対パス</param>
/// <param name="AssemblyName">MSBuild が評価した assembly 名</param>
/// <param name="TargetPath">MSBuild が評価した出力 assembly の絶対パス</param>
/// <param name="TargetMvid">ロード時に照合した出力 assembly の MVID</param>
/// <param name="Assembly">専用 load context へロードした assembly</param>
public sealed record LoadedSolutionProject(
    string ProjectPath,
    string AssemblyName,
    string TargetPath,
    Guid TargetMvid,
    Assembly Assembly);
