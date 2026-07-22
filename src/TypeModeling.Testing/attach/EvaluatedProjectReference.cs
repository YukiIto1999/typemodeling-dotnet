namespace TypeModeling.Testing.Attach;

/// <summary>MSBuild 評価済み ProjectReference</summary>
/// <param name="RelativePath">repository root からの相対 path</param>
/// <param name="FullPath">参照先の絶対 path</param>
/// <param name="OutputItemType">出力 item 種別</param>
/// <param name="ReferenceOutputAssembly">assembly 参照の有効値</param>
internal sealed record EvaluatedProjectReference(
    string RelativePath,
    string FullPath,
    string OutputItemType,
    string ReferenceOutputAssembly);
