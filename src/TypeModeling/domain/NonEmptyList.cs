using System.Collections;

namespace TypeModeling.Domain;

/// <summary>少なくとも一つの要素を持つことを型で保証する不変の並び</summary>
/// <remarks>空の並びを構築できないので、先頭の要素の取得は常に成功する</remarks>
/// <typeparam name="T">要素の型</typeparam>
public sealed class NonEmptyList<T> : IReadOnlyList<T>, IEquatable<NonEmptyList<T>>
{
    /// <summary>先頭を含む全ての要素</summary>
    private readonly T[] _items;

    /// <summary>空でない要素の配列による構築</summary>
    /// <param name="items">先頭を含む全ての要素</param>
    internal NonEmptyList(T[] items) => _items = items;

    /// <summary>先頭の要素</summary>
    public T Head => _items[0];

    /// <summary>要素の数</summary>
    public int Count => _items.Length;

    /// <summary>位置による要素の取得</summary>
    /// <param name="index">要素の位置</param>
    /// <returns>指定した位置の要素</returns>
    public T this[int index] => _items[index];

    /// <summary>要素ごとの変換</summary>
    /// <typeparam name="TResult">変換後の要素の型</typeparam>
    /// <param name="selector">要素の変換</param>
    /// <returns>同じ数の要素を持つ空でない並び</returns>
    public NonEmptyList<TResult> Select<TResult>(Func<T, TResult> selector) =>
        new([.. _items.Select(selector)]);

    /// <summary>後ろへの並びの連結</summary>
    /// <param name="other">後ろに連ねる並び</param>
    /// <returns>両方の要素を並べた空でない並び</returns>
    public NonEmptyList<T> Concat(IEnumerable<T> other) => new([.. _items, .. other]);

    /// <summary>要素の列挙</summary>
    /// <returns>先頭から順の列挙子</returns>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

    /// <summary>要素の型を問わない列挙</summary>
    /// <returns>先頭から順の列挙子</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>同じ要素を同じ順に持つかの判定</summary>
    /// <param name="other">比較する相手</param>
    /// <returns>同じ要素を同じ順に持つなら true</returns>
    public bool Equals(NonEmptyList<T>? other) => other is not null && _items.SequenceEqual(other._items);

    /// <summary>同じ要素を同じ順に持つ並びかの判定</summary>
    /// <param name="obj">比較する相手</param>
    /// <returns>同じ要素を同じ順に持つ並びなら true</returns>
    public override bool Equals(object? obj) => obj is NonEmptyList<T> other && Equals(other);

    /// <summary>要素の並びのハッシュ値</summary>
    /// <returns>全ての要素から求めたハッシュ値</returns>
    public override int GetHashCode() => _items.Aggregate(new HashCode(), (hash, item) => { hash.Add(item); return hash; }).ToHashCode();

    /// <summary>要素の並びの文字列表現</summary>
    /// <returns>要素を並べた文字列</returns>
    public override string ToString() => "[" + string.Join(", ", _items) + "]";
}

/// <summary>空でない並びの構築</summary>
public static class NonEmptyList
{
    /// <summary>先頭と残りの要素による構築</summary>
    /// <typeparam name="T">要素の型</typeparam>
    /// <param name="head">先頭の要素</param>
    /// <param name="tail">残りの要素</param>
    /// <returns>先頭と残りを並べた空でない並び</returns>
    public static NonEmptyList<T> Create<T>(T head, params IEnumerable<T> tail) => new([head, .. tail]);

    /// <summary>空でない場合だけの並びの構築</summary>
    /// <typeparam name="T">要素の型</typeparam>
    /// <param name="items">並べる要素</param>
    /// <returns>要素があれば空でない並び、なければ null</returns>
    public static NonEmptyList<T>? FromSequence<T>(IEnumerable<T> items)
    {
        T[] array = [.. items];
        return array.Length == 0 ? null : new NonEmptyList<T>(array);
    }
}
