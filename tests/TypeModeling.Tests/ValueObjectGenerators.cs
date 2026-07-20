using CsCheck;

namespace TypeModeling.Tests.Support;

/// <summary>テスト専用の値オブジェクトの規則に沿う入力と反する入力の生成器</summary>
internal static class ValueObjectGenerators
{
    private static readonly Gen<char> Lower = Gen.Int['a', 'z'].Select(code => (char)code);
    private static readonly Gen<string> Padding = Gen.OneOf(
        Gen.Const(string.Empty),
        Gen.Const(" "),
        Gen.Const("  "),
        Gen.Const("\t"));

    /// <summary>空でないテスト専用の識別子</summary>
    public static readonly Gen<Guid> ValidIdentifier = Gen.Guid.Where(value => value != Guid.Empty);

    /// <summary>空のテスト専用の識別子</summary>
    public static readonly Gen<Guid> InvalidIdentifier = Gen.Const(Guid.Empty);

    /// <summary>互いに異なる空でないテスト専用の識別子</summary>
    public static readonly Gen<(Guid First, Guid Second)> DistinctIdentifiers = Gen.Select(
        ValidIdentifier,
        ValidIdentifier,
        (first, second) => (First: first, Second: second))
        .Where(pair => pair.First != pair.Second);

    /// <summary>前後の空白を除くと英小文字が残るテスト専用の名前</summary>
    public static readonly Gen<string> ValidName = Gen.Select(
        Padding,
        Lower.Array[1, 32],
        Padding,
        (prefix, value, suffix) => prefix + new string(value) + suffix);

    /// <summary>空文字または空白だけで構成されたテスト専用の名前</summary>
    public static readonly Gen<string> InvalidName = Gen.OneOf(
        Gen.Const(string.Empty),
        Gen.Const(" "),
        Gen.Const("  "),
        Gen.Const("\t"),
        Gen.Const("\r\n"));

    /// <summary>正のテスト専用の数量</summary>
    public static readonly Gen<int> ValidQuantity = Gen.Int.Where(value => value > 0);

    /// <summary>零または負のテスト専用の数量</summary>
    public static readonly Gen<int> InvalidQuantity = Gen.Int.Where(value => value <= 0);

    /// <summary>加算しても正の範囲に収まるテスト専用の数量</summary>
    public static readonly Gen<int> IncrementableQuantity = Gen.Int.Where(value => value is > 0 and < int.MaxValue);
}
