namespace TypeModeling.Testing.Attach;

/// <summary>未解決 Import の候補展開に使う property 宣言の記録</summary>
/// <param name="Value">property 値</param>
/// <param name="DefiningFile">定義元 file</param>
internal sealed record RepoAuthoredPropertyValue(string Value, string DefiningFile);
