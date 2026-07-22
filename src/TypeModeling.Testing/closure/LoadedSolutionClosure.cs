namespace TypeModeling.Testing.Closure;

/// <summary>solution の推移 ProjectReference 閉包のロード結果</summary>
/// <param name="ExpectedProjectPaths">静的 ProjectReference 閉包の project 相対パス</param>
/// <param name="Projects">MSBuild 評価後にロードした managed project</param>
/// <param name="ReflectionTypes">ロードした managed project が宣言する CLR 型</param>
public sealed record LoadedSolutionClosure(
    IReadOnlyList<string> ExpectedProjectPaths,
    IReadOnlyList<LoadedSolutionProject> Projects,
    IReadOnlyList<Type> ReflectionTypes);
