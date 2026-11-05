using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TypeModeling.Analyzers.Generation.Union;

/// <summary>C# 15 の union 宣言に当たる、partial コンストラクタで case 型を並べた union 型の形</summary>
/// <remarks><c>union Pet(Cat, Dog);</c> は <c>[Union] partial struct Pet</c> と <c>public partial Pet(Cat value);</c> の並びに対応する</remarks>
internal static class UnionDeclarationShape
{
    /// <summary>実装を持たない partial コンストラクタの宣言</summary>
    /// <param name="type">union 型</param>
    /// <returns>case 型を宣言する partial コンストラクタ</returns>
    internal static ImmutableArray<IMethodSymbol> CaseConstructors(INamedTypeSymbol type) =>
        [.. type.InstanceConstructors.Where(IsCaseConstructor)];

    /// <summary>union 宣言の形かの判定</summary>
    /// <param name="type">判定対象の union 型</param>
    /// <returns>case 型を宣言する partial コンストラクタを持つ場合に true</returns>
    internal static bool IsDeclaration(INamedTypeSymbol type) =>
        type.InstanceConstructors.Any(IsCaseConstructor);

    /// <summary>case 型を宣言する partial コンストラクタかの判定</summary>
    /// <param name="constructor">判定対象のコンストラクタ</param>
    /// <returns>引数を一つだけ持つ partial の宣言なら true</returns>
    internal static bool IsCaseConstructor(IMethodSymbol constructor) =>
        constructor is { Parameters.Length: 1, IsPartialDefinition: true };

    /// <summary>C# 15 の union 変換を言語が持つかの判定</summary>
    /// <param name="tree">union 型を宣言する tree</param>
    /// <returns>言語版が C# 14 より後なら true</returns>
    /// <remarks>analyzer が参照する Roslyn は C# 15 の版の名前を持たないので、C# 14 の版の値 1400 と比べる</remarks>
    internal static bool LanguageHasUnions(SyntaxTree tree) =>
        tree.Options is CSharpParseOptions options && (int)options.LanguageVersion > 1400;
}
