using CsCheck;
using TypeModeling.Domain;
using TUnit.Core;

namespace TypeModeling.Tests;

/// <summary>query 式と、結果の並びの束ねの property 検証</summary>
public sealed class ResultAggregationLaws
{
    private static Result<int, string> Parse(int value) =>
        value >= 0 ? Result.Succeeded(value) : Result.Failed($"negative {value}");

    /// <summary>query 式の成功の連結と、最初の失敗の伝播</summary>
    [Test]
    public void Query_expressions_combine_successes_and_stop_at_the_first_failure()
    {
        Gen.Int.Select(Gen.Int).Sample((first, second) =>
        {
            var combined = from left in Parse(first)
                           from right in Parse(second)
                           select (long)left + right;
            var firstFailure = first < 0 ? $"negative {first}" : null;
            var failure = firstFailure ?? (second < 0 ? $"negative {second}" : null);
            Result<long, string> expected = failure is null ? Result.Succeeded((long)first + second) : Result.Failed(failure);
            return combined == expected;
        });
    }

    /// <summary>Sequence の全ての成功の保持と、最初の失敗での停止</summary>
    [Test]
    public void Sequence_keeps_every_success_or_returns_the_first_failure()
    {
        Gen.Int[-3, 3].Array[0, 6].Sample(values =>
        {
            var sequenced = values.Select(Parse).Sequence();
            var firstNegative = values.FirstOrDefault(value => value < 0, 0);
            return values.All(value => value >= 0)
                ? sequenced.Value is Succeeded<IReadOnlyList<int>> { Value: var kept } && kept.SequenceEqual(values)
                : sequenced.Value is Failed<string> { Failure: var failure } && failure == $"negative {firstNegative}";
        });
    }

    /// <summary>Accumulate による全ての失敗の収集</summary>
    [Test]
    public void Accumulate_collects_every_failure_in_order()
    {
        Gen.Int[-3, 3].Array[0, 6].Sample(values =>
        {
            var accumulated = values.Select(Parse).Accumulate();
            var failures = values.Where(value => value < 0).Select(value => $"negative {value}").ToArray();
            return failures.Length == 0
                ? accumulated.Value is Succeeded<IReadOnlyList<int>> { Value: var kept } && kept.SequenceEqual(values)
                : accumulated.Value is Failed<NonEmptyList<string>> { Failure: var collected } && collected.SequenceEqual(failures);
        });
    }

    /// <summary>Zip の成功値の組合せと、両方の失敗の収集</summary>
    [Test]
    public void Zip_combines_successes_and_collects_both_failures()
    {
        Gen.Int[-3, 3].Select(Gen.Int[-3, 3]).Sample((first, second) =>
        {
            var zipped = Parse(first).Zip(Parse(second), (left, right) => left * right);
            var failures = new[] { first, second }.Where(value => value < 0).Select(value => $"negative {value}").ToArray();
            return failures.Length == 0
                ? zipped == Result.Succeeded(first * second)
                : zipped.Value is Failed<NonEmptyList<string>> { Failure: var collected } && collected.SequenceEqual(failures);
        });
    }
}
