namespace TypeModeling.Testing.Attach;

/// <summary>repository authored Import の解決結果</summary>
/// <param name="Paths">解決済み file path 集合</param>
/// <param name="UnresolvedPatterns">未解決 import pattern 集合</param>
internal sealed record RepoAuthoredImportResolution(
    IReadOnlyList<string> Paths,
    IReadOnlyList<string> UnresolvedPatterns);
