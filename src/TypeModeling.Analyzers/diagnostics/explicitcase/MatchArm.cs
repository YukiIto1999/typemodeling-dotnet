using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.Diagnostics.ExplicitCase;

/// <summary>switch の arm</summary>
/// <param name="Pattern">arm の pattern。switch 文の default の label なら null</param>
/// <param name="Syntax">arm を報告する位置の構文</param>
/// <param name="Exempt">compiler が網羅に数えないので、後から足す case を受け取っても報告しない arm なら true</param>
internal readonly record struct MatchArm(IPatternOperation? Pattern, SyntaxNode Syntax, bool Exempt);
