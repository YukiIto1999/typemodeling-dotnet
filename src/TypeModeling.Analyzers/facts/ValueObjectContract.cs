using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Facts;

/// <summary>ValueObject の generator が出力する member の署名と、その署名を生成物として認める規則</summary>
/// <remarks>analyzer が見る最終の compilation では、generator の出力と、手書きや別の generator が足した宣言を source の出どころで区別できない。区別の根拠は署名だけであり、同じ署名の member は同じ型に二つ宣言できず compile error になるので、署名に当たる宣言を認めても同じ経路をもう一つ足す宣言は残らない</remarks>
internal static class ValueObjectContract
{
    /// <summary>ValueObject 属性の metadata 名</summary>
    internal const string AttributeMetadataName = "TypeModeling.Domain.ValueObjectAttribute`1";

    /// <summary>Result 型の metadata 名</summary>
    internal const string ResultMetadataName = "TypeModeling.Domain.Result`2";

    /// <summary>生成する検証済みの基底値を公開する property の名前</summary>
    internal const string ValueName = "Value";

    /// <summary>生成する構築 method の名前</summary>
    private const string CreateName = "Create";

    /// <summary>型に付いた ValueObject 属性の適用と、その属性が指す基底型の取得</summary>
    /// <param name="type">属性を探す型</param>
    /// <param name="attribute">ValueObject 属性の定義</param>
    /// <returns>ValueObject 属性を持たない型では null の、属性の適用と基底型</returns>
    internal static (AttributeData Application, ITypeSymbol Underlying)? Marker(
        INamedTypeSymbol type,
        INamedTypeSymbol attribute
    )
    {
        foreach (var data in type.GetAttributes())
        {
            if (
                data.AttributeClass is { TypeArguments.Length: 1 } candidate
                && SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, attribute)
            )
            {
                return (data, candidate.TypeArguments[0]);
            }
        }

        return null;
    }

    /// <summary>generator が出力する Create と同じ署名の method かの判定</summary>
    /// <param name="method">判定対象の method</param>
    /// <param name="type">構築する値オブジェクトの型</param>
    /// <param name="underlying">値オブジェクトの基底型</param>
    /// <param name="result">参照に無ければ null の Result 型の定義</param>
    /// <returns>値オブジェクト自身の public static Create(基底型) が Result の値オブジェクトを返す場合に true</returns>
    internal static bool IsGeneratedCreate(
        IMethodSymbol? method,
        INamedTypeSymbol type,
        ITypeSymbol underlying,
        INamedTypeSymbol? result
    ) =>
        method
            is {
                Name: CreateName,
                MethodKind: MethodKind.Ordinary,
                IsStatic: true,
                Arity: 0,
                DeclaredAccessibility: Accessibility.Public,
                Parameters.Length: 1,
            }
        && SymbolEqualityComparer.Default.Equals(method.ContainingType, type)
        && method.Parameters[0].RefKind == RefKind.None
        && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, underlying)
        && ReturnsResultOf(method, type, result);

    /// <summary>generator が出力する private コンストラクタと同じ署名かの判定</summary>
    /// <param name="constructor">判定対象の instance コンストラクタ</param>
    /// <param name="underlying">値オブジェクトの基底型</param>
    /// <returns>基底型を値で受け取る private コンストラクタの場合に true</returns>
    internal static bool IsGeneratedConstructor(
        IMethodSymbol constructor,
        ITypeSymbol underlying
    ) =>
        constructor
            is {
                MethodKind: MethodKind.Constructor,
                IsStatic: false,
                DeclaredAccessibility: Accessibility.Private,
                Parameters.Length: 1,
            }
        && constructor.Parameters[0].RefKind == RefKind.None
        && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, underlying);

    /// <summary>method が値オブジェクトを成功の値に持つ Result を返すかの判定</summary>
    /// <param name="method">判定対象の method</param>
    /// <param name="type">成功の値の型</param>
    /// <param name="result">参照に無ければ null の Result 型の定義</param>
    /// <returns>Result の値の型引数が値オブジェクトの場合に true</returns>
    private static bool ReturnsResultOf(
        IMethodSymbol method,
        INamedTypeSymbol type,
        INamedTypeSymbol? result
    ) =>
        result is not null
        && method.ReturnType is INamedTypeSymbol returned
        && SymbolEqualityComparer.Default.Equals(returned.OriginalDefinition, result)
        && SymbolEqualityComparer.Default.Equals(returned.TypeArguments[0], type);
}
