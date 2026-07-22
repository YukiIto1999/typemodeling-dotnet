namespace TypeModeling.Testing.Closure;

/// <summary>solution 閉包検査の宣言</summary>
/// <param name="RepoRoot">検査対象 repository の絶対パス</param>
/// <param name="SolutionFileName">repository root からの solution ファイル名</param>
/// <param name="Configuration">build と MSBuild 評価に使う構成名</param>
/// <param name="ExcludedProjects">閉包外に置く project の相対パスと除外根拠</param>
public sealed record SolutionClosureScan(
    string RepoRoot,
    string SolutionFileName,
    string Configuration,
    IReadOnlyDictionary<string, string> ExcludedProjects);
