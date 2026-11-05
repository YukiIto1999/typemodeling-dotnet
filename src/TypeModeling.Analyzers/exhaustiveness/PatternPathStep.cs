using System.Linq;

namespace TypeModeling.Analyzers.Exhaustiveness;

/// <summary>pattern 上の経路の一段</summary>
internal sealed class PatternPathStep
{
    /// <summary>段の書き方による初期化</summary>
    /// <param name="narrowing">pattern に書く型。書かないなら null</param>
    /// <param name="member">property pattern の member の名前。positional なら null</param>
    /// <param name="index">positional pattern の要素の位置</param>
    /// <param name="arity">positional pattern の要素数</param>
    private PatternPathStep(string? narrowing, string? member, int index, int arity)
    {
        Narrowing = narrowing;
        Member = member;
        Index = index;
        Arity = arity;
    }

    /// <summary>pattern に書く型。書かないなら null</summary>
    private string? Narrowing { get; }

    /// <summary>property pattern の member の名前。positional なら null</summary>
    private string? Member { get; }

    /// <summary>positional pattern の要素の位置</summary>
    private int Index { get; }

    /// <summary>positional pattern の要素数</summary>
    private int Arity { get; }

    /// <summary>property pattern の段</summary>
    /// <param name="narrowing">pattern に書く型。書かないなら null</param>
    /// <param name="member">部分値を取り出す member の名前</param>
    /// <returns>property pattern の段</returns>
    internal static PatternPathStep Property(string? narrowing, string member) => new(narrowing, member, 0, 0);

    /// <summary>positional pattern の一要素の段</summary>
    /// <param name="narrowing">pattern に書く型。書かないなら null</param>
    /// <param name="index">要素の位置</param>
    /// <param name="arity">要素数</param>
    /// <returns>positional pattern の段</returns>
    internal static PatternPathStep Element(string? narrowing, int index, int arity) => new(narrowing, null, index, arity);

    /// <summary>内側の pattern をこの段で包んだ pattern</summary>
    /// <param name="inner">この段の部分値に当てる pattern の文字列</param>
    /// <returns>この段の入力に当てる pattern の文字列</returns>
    internal string Wrap(string inner)
    {
        if (Member is { } member)
            return $"{Narrowing} {{ {member}: {inner} }}";

        var elements = Enumerable.Range(0, Arity).Select(position => position == Index ? inner : "_");
        return $"{Narrowing}({string.Join(", ", elements)})";
    }
}
