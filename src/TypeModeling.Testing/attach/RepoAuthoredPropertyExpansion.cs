namespace TypeModeling.Testing.Attach;

/// <summary>MSBuild property expression の展開結果</summary>
/// <param name="Values">解決済み値集合</param>
/// <param name="UnresolvedValues">未解決値集合</param>
internal sealed record RepoAuthoredPropertyExpansion(
    IReadOnlyList<string> Values,
    IReadOnlyList<string> UnresolvedValues);
