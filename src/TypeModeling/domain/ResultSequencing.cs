namespace TypeModeling.Domain;

/// <summary>結果を逐次に連ねる combinator</summary>
public static class ResultSequencing
{
    /// <summary>成功のときだけ次の結果へ進む連結</summary>
    /// <typeparam name="TFailure">想定内の失敗を表す型</typeparam>
    /// <typeparam name="TValue">前段の成功値の型</typeparam>
    /// <typeparam name="TResult">次の結果が返す成功値の型</typeparam>
    /// <param name="result">前段になる結果</param>
    /// <param name="bind">成功値から次の結果を作る関数</param>
    /// <returns>前段の成功に依存して次段を評価した結果</returns>
    public static Result<TResult, TFailure> Bind<TFailure, TValue, TResult>(
        this Result<TValue, TFailure> result,
        Func<TValue, Result<TResult, TFailure>> bind) =>
        result.IsFailed(out var failed, out var value) ? failed : bind(value);

    /// <summary>query 式の二つ目以降の from に当たる連結</summary>
    /// <typeparam name="TFailure">想定内の失敗を表す型</typeparam>
    /// <typeparam name="TValue">前段の成功値の型</typeparam>
    /// <typeparam name="TIntermediate">次段の成功値の型</typeparam>
    /// <typeparam name="TResult">両方の成功値から作る値の型</typeparam>
    /// <param name="result">前段になる結果</param>
    /// <param name="bind">成功値から次の結果を作る関数</param>
    /// <param name="project">両方の成功値から結果の値を作る関数</param>
    /// <returns>両方が成功した場合だけ成功する結果</returns>
    public static Result<TResult, TFailure> SelectMany<TFailure, TValue, TIntermediate, TResult>(
        this Result<TValue, TFailure> result,
        Func<TValue, Result<TIntermediate, TFailure>> bind,
        Func<TValue, TIntermediate, TResult> project) =>
        result.Bind(value => bind(value).Map(intermediate => project(value, intermediate)));

    /// <summary>最初の失敗で止める結果の並びの束ね</summary>
    /// <typeparam name="TValue">成功値の型</typeparam>
    /// <typeparam name="TFailure">想定内の失敗を表す型</typeparam>
    /// <param name="results">束ねる結果の並び</param>
    /// <returns>全て成功なら成功値の並び、それ以外は最初の失敗</returns>
    public static Result<IReadOnlyList<TValue>, TFailure> Sequence<TValue, TFailure>(
        this IEnumerable<Result<TValue, TFailure>> results)
    {
        var values = new List<TValue>();
        foreach (var result in results)
        {
            if (result.IsFailed(out var failed, out var value))
                return failed;

            values.Add(value);
        }

        return Result.Succeeded<IReadOnlyList<TValue>>(values);
    }
}
