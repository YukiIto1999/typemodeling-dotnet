using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Exhaustiveness;

/// <summary>switch の pattern が型を調べる部分値の位置</summary>
/// <param name="Path">入力から部分値への経路</param>
/// <param name="Type">その位置で pattern が受け取る型。型のない式なら null</param>
/// <param name="Union">union 型の Value の位置なら、その union 型</param>
internal sealed record PatternPosition(PatternPath Path, ITypeSymbol? Type, INamedTypeSymbol? Union = null);
