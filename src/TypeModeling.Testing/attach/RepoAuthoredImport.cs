namespace TypeModeling.Testing.Attach;

/// <summary>repository authored Import 宣言</summary>
/// <param name="Project">Import Project expression</param>
/// <param name="ImportingFile">宣言元 file</param>
/// <param name="IsConditional">条件付き import の判定</param>
internal sealed record RepoAuthoredImport(
    string Project,
    string ImportingFile,
    bool IsConditional);
