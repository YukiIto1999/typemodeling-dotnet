using CsCheck;
using TypeModeling.Domain;
using TUnit.Core;

namespace TypeModeling.Tests;

/// <summary>空でない並びの構築と値の等価の property 検証</summary>
public sealed class NonEmptyListLaws
{
    /// <summary>先頭と残りからの構築と、要素の順序の保持</summary>
    [Test]
    public void Create_keeps_the_head_first_and_the_tail_in_order()
    {
        Gen.Int.Select(Gen.Int.Array[0, 5]).Sample((head, tail) =>
        {
            var list = NonEmptyList.Create(head, tail);
            return list.Head == head && list.Count == tail.Length + 1 && list.Skip(1).SequenceEqual(tail);
        });
    }

    /// <summary>空の並びの拒否と、空でない並びの受理</summary>
    [Test]
    public void FromSequence_rejects_only_the_empty_sequence()
    {
        Gen.Int.Array[0, 5].Sample(items =>
            items.Length == 0
                ? NonEmptyList.FromSequence(items) is null
                : NonEmptyList.FromSequence(items) is { } list && list.SequenceEqual(items));
    }

    /// <summary>同じ要素を同じ順に持つ並びの等価とハッシュ値の一致</summary>
    [Test]
    public void Lists_with_the_same_items_are_equal_and_hash_alike()
    {
        Gen.Int.Select(Gen.Int.Array[0, 5]).Sample((head, tail) =>
        {
            var first = NonEmptyList.Create(head, tail);
            var second = NonEmptyList.Create(head, [.. tail]);
            return first.Equals(second) && first.GetHashCode() == second.GetHashCode();
        });
    }

    /// <summary>要素の違う並びと null との非等価</summary>
    [Test]
    public void Lists_with_other_items_or_null_are_not_equal()
    {
        Gen.Int.Select(Gen.Int.Array[0, 5]).Sample((head, tail) =>
        {
            var list = NonEmptyList.Create(head, tail);
            var longer = list.Concat([head]);
            return !list.Equals(null) && !list.Equals(longer) && !list.Equals((object)head) &&
                   list.GetHashCode() != longer.GetHashCode();
        });
    }

    /// <summary>要素を並べた文字列表現</summary>
    [Test]
    public void ToString_lists_the_items_in_order()
    {
        Gen.Int.Select(Gen.Int.Array[0, 5]).Sample((head, tail) =>
            NonEmptyList.Create(head, tail).ToString() == "[" + string.Join(", ", [head, .. tail]) + "]");
    }

    /// <summary>要素ごとの変換と連結の要素数の保持</summary>
    [Test]
    public void Select_and_Concat_preserve_the_items()
    {
        Gen.Int.Select(Gen.Int.Array[0, 5]).Sample((head, tail) =>
        {
            var list = NonEmptyList.Create(head, tail);
            return list.Select(item => item * 2).SequenceEqual(Enumerable.Select(list, item => item * 2)) &&
                   list.Concat(tail).Count == list.Count + tail.Length;
        });
    }
}
