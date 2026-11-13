using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TypeModeling.Analyzers.Facts;

/// <summary>C# の型変換規則に基づく実行時の型の重なりの判定</summary>
/// <param name="compilation">変換を分類する C# の compilation</param>
/// <remarks>数値変換とユーザー定義変換のような別の値を作る変換の除外</remarks>
internal readonly struct TypeConversions(Compilation compilation)
{
    /// <summary>変換を分類する C# の compilation</summary>
    private readonly CSharpCompilation _compilation = (CSharpCompilation)compilation;

    /// <summary>source の全ての値が destination の値でもあるかの判定</summary>
    /// <param name="source">値の型</param>
    /// <param name="destination">変換先の型</param>
    /// <returns>暗黙の同一変換か参照変換か boxing 変換があれば true</returns>
    internal bool IsImplicit(ITypeSymbol source, ITypeSymbol destination)
    {
        var conversion = _compilation.ClassifyConversion(source, destination);
        return conversion.IsImplicit && PreservesValue(conversion);
    }

    /// <summary>二つの型の値が同じ値になりうるかの判定</summary>
    /// <param name="left">判定対象の型</param>
    /// <param name="right">判定対象の型</param>
    /// <returns>型引数かいずれかの向きに同じ値のまま変換できる型なら true</returns>
    /// <remarks>型引数は制約から重なりを確かめない保守的な判定</remarks>
    internal bool MayShare(ITypeSymbol left, ITypeSymbol right) =>
        left.TypeKind == TypeKind.TypeParameter
        || right.TypeKind == TypeKind.TypeParameter
        || Reaches(left, right)
        || Reaches(right, left);

    /// <summary>source の値を同じ値のまま destination の値にできるかの判定</summary>
    /// <param name="source">値の型</param>
    /// <param name="destination">変換先の型</param>
    /// <returns>暗黙か明示の同一・参照・boxing・unboxing 変換があれば true</returns>
    private bool Reaches(ITypeSymbol source, ITypeSymbol destination)
    {
        var conversion = _compilation.ClassifyConversion(source, destination);
        return conversion.Exists && PreservesValue(conversion);
    }

    /// <summary>変換が値を作り替えないかの判定</summary>
    /// <param name="conversion">分類した変換</param>
    /// <returns>同一変換か参照変換か boxing 変換か unboxing 変換なら true</returns>
    private static bool PreservesValue(Conversion conversion) =>
        conversion.IsIdentity
        || conversion.IsReference
        || conversion.IsBoxing
        || conversion.IsUnboxing;
}
