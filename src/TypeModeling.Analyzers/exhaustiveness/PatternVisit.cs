using Microsoft.CodeAnalysis.Operations;

namespace TypeModeling.Analyzers.Exhaustiveness;

/// <summary>pattern の木の巡回で訪れた pattern</summary>
/// <param name="Path">pattern が当たる部分値への経路</param>
/// <param name="Pattern">訪れた pattern</param>
/// <param name="IsPosition">部分値に当てた pattern の最上位なら true。and と or の枝は同じ部分値の内側なので false</param>
/// <param name="Opaque">not か list pattern の内側にある部分値なら true</param>
internal readonly record struct PatternVisit(
    PatternPath Path,
    IPatternOperation Pattern,
    bool IsPosition,
    bool Opaque
);
