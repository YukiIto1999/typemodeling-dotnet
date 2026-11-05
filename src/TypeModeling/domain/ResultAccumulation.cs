namespace TypeModeling.Domain;

/// <summary>全ての想定内の失敗を集める combinator</summary>
/// <remarks>F# の Validation に当たり、最初の失敗で止めずに全ての失敗を返す</remarks>
public static class ResultAccumulation
{
    /// <summary>全ての失敗を集める結果の並びの束ね</summary>
    /// <typeparam name="TValue">成功値の型</typeparam>
    /// <typeparam name="TFailure">想定内の失敗を表す型</typeparam>
    /// <param name="results">束ねる結果の並び</param>
    /// <returns>全て成功なら成功値の並び、それ以外は全ての失敗</returns>
    public static Result<IReadOnlyList<TValue>, NonEmptyList<TFailure>> Accumulate<TValue, TFailure>(
        this IEnumerable<Result<TValue, TFailure>> results)
    {
        var values = new List<TValue>();
        var failures = new List<TFailure>();
        foreach (var result in results)
        {
            if (result.IsFailed(out var failed, out var value))
                failures.Add(failed.Failure);
            else
                values.Add(value);
        }

        return NonEmptyList.FromSequence(failures) is { } collected
            ? Result.Failed(collected)
            : Result.Succeeded<IReadOnlyList<TValue>>(values);
    }

    /// <summary>二つの結果の成功値の組合せと、両方の失敗の収集</summary>
    /// <typeparam name="TFailure">想定内の失敗を表す型</typeparam>
    /// <typeparam name="TFirst">一つ目の成功値の型</typeparam>
    /// <typeparam name="TSecond">二つ目の成功値の型</typeparam>
    /// <typeparam name="TResult">組み合わせた値の型</typeparam>
    /// <param name="first">一つ目の結果</param>
    /// <param name="second">二つ目の結果</param>
    /// <param name="combine">両方の成功値の組合せ</param>
    /// <returns>両方成功なら組み合わせた値、それ以外は起きた全ての失敗</returns>
    public static Result<TResult, NonEmptyList<TFailure>> Zip<TFailure, TFirst, TSecond, TResult>(
        this Result<TFirst, TFailure> first,
        Result<TSecond, TFailure> second,
        Func<TFirst, TSecond, TResult> combine)
    {
        var firstFailed = first.IsFailed(out var firstFailure, out var firstValue);
        var secondFailed = second.IsFailed(out var secondFailure, out var secondValue);
        return (firstFailed, secondFailed) switch
        {
            (false, false) => Result.Succeeded(combine(firstValue!, secondValue!)),
            (true, false) => Result.Failed(NonEmptyList.Create(firstFailure.Failure)),
            (false, true) => Result.Failed(NonEmptyList.Create(secondFailure.Failure)),
            (true, true) => Result.Failed(NonEmptyList.Create(firstFailure.Failure, secondFailure.Failure)),
        };
    }
}
