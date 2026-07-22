namespace TypeModeling.Testing.Attach;

/// <summary>project から到達した検査対象 MSBuild file の記録</summary>
/// <param name="Path">MSBuild file の絶対 path</param>
/// <param name="IsConditionallyImported">条件付き import 配下の判定</param>
internal sealed record RepoAuthoredMsBuildFile(string Path, bool IsConditionallyImported);
