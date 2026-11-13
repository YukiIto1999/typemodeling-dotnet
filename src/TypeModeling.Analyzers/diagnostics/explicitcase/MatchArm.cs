using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.Diagnostics.ExplicitCase;

/// <summary>switch の arm</summary>
/// <param name="Pattern">arm の pattern。switch 文の default の label なら null</param>
/// <param name="Syntax">arm を報告する位置の構文</param>
internal readonly record struct MatchArm(IPatternOperation? Pattern, SyntaxNode Syntax)
{
    /// <summary>compiler の欠落診断を残す switch 式の guard による免除</summary>
    /// <value>定数 true でない guard を持つ switch 式の arm なら true</value>
    internal bool Exempt =>
        Pattern?.Parent is ISwitchExpressionArmOperation { Guard: { } guard }
        && guard.ConstantValue is not { HasValue: true, Value: true };
}
