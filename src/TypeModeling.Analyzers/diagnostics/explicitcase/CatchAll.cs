using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Diagnostics.ExplicitCase;

/// <summary>後から足す case を受け取る、または受け取らないと確かめられない arm の報告</summary>
/// <param name="Node">報告する位置の構文</param>
/// <param name="Family">受け取る case を持つ型の名前</param>
internal readonly record struct CatchAll(SyntaxNode Node, string Family);
