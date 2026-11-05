using System.Collections.Immutable;

namespace TypeModeling.Analyzers.Exhaustiveness;

/// <summary>switch の入力から部分値へ至る pattern 上の経路</summary>
/// <remarks>経路の各段は、部分値を取り出す property pattern か positional pattern の一要素</remarks>
internal sealed class PatternPath
{
    /// <summary>入力そのものを指す空の経路</summary>
    internal static PatternPath Root { get; } = new([]);

    /// <summary>入力側から並べた経路の段</summary>
    private readonly ImmutableArray<PatternPathStep> _steps;

    /// <summary>経路の段による初期化</summary>
    /// <param name="steps">入力側から並べた経路の段</param>
    private PatternPath(ImmutableArray<PatternPathStep> steps) => _steps = steps;

    /// <summary>property pattern の段を足した経路</summary>
    /// <param name="narrowing">property pattern に書く型。入力の型のままなら null</param>
    /// <param name="member">部分値を取り出す member の名前</param>
    /// <returns>一段深い経路</returns>
    internal PatternPath Property(string? narrowing, string member) =>
        new(_steps.Add(PatternPathStep.Property(narrowing, member)));

    /// <summary>positional pattern の一要素の段を足した経路</summary>
    /// <param name="narrowing">positional pattern に書く型。書かないなら null</param>
    /// <param name="index">要素の位置</param>
    /// <param name="arity">positional pattern の要素数</param>
    /// <returns>一段深い経路</returns>
    internal PatternPath Element(string? narrowing, int index, int arity) =>
        new(_steps.Add(PatternPathStep.Element(narrowing, index, arity)));

    /// <summary>経路の先の部分値に leaf の pattern を当てる、入力全体への pattern</summary>
    /// <param name="leaf">部分値に当てる pattern の文字列</param>
    /// <returns>入力全体に当てる pattern の文字列</returns>
    internal string Wrap(string leaf)
    {
        var pattern = leaf;
        for (var index = _steps.Length - 1; index >= 0; index--)
            pattern = _steps[index].Wrap(pattern);

        return pattern;
    }
}
