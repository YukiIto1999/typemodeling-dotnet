namespace TypeModeling.Testing.Attach;

/// <summary>analyzer attachment 規約の要件</summary>
/// <param name="AnalyzerProjectPath">repository root から analyzer project への相対 path</param>
/// <param name="ProtectedDiagnosticIds">抑止を拒否する診断 ID 集合</param>
/// <param name="ProtectedDiagnosticPrefixes">抑止を拒否する診断 prefix 集合</param>
/// <param name="ExpectedProjectPaths">guarded feature を使う project の期待集合</param>
/// <param name="ProtectedDiagnosticCategories">抑止を拒否する診断 category 集合</param>
public sealed record AnalyzerAttachmentRequirement(
    string AnalyzerProjectPath,
    IReadOnlySet<string> ProtectedDiagnosticIds,
    IReadOnlySet<string> ProtectedDiagnosticPrefixes,
    IReadOnlySet<string> ExpectedProjectPaths,
    IReadOnlySet<string> ProtectedDiagnosticCategories);
