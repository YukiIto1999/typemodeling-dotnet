namespace TypeModeling.Domain;

/// <summary>成功値を写す combinator</summary>
public static class ResultMapping
{
    /// <summary>成功値だけを写す純粋な変換</summary>
    /// <typeparam name="TFailure">想定内の失敗を表す型</typeparam>
    /// <typeparam name="TValue">変換前の成功値の型</typeparam>
    /// <typeparam name="TResult">変換後の成功値の型</typeparam>
    /// <param name="result">変換の対象になる結果</param>
    /// <param name="selector">成功値を変換する純粋な関数</param>
    /// <returns>成功値を変換した結果</returns>
    public static Result<TResult, TFailure> Map<TFailure, TValue, TResult>(
        this Result<TValue, TFailure> result, Func<TValue, TResult> selector) =>
        result.IsFailed(out var failed, out var value) ? failed : Result.Succeeded(selector(value));

    /// <summary>query 式の select に当たる成功値の変換</summary>
    /// <typeparam name="TFailure">想定内の失敗を表す型</typeparam>
    /// <typeparam name="TValue">変換前の成功値の型</typeparam>
    /// <typeparam name="TResult">変換後の成功値の型</typeparam>
    /// <param name="result">変換の対象になる結果</param>
    /// <param name="selector">成功値を変換する純粋な関数</param>
    /// <returns>成功値を変換した結果</returns>
    public static Result<TResult, TFailure> Select<TFailure, TValue, TResult>(
        this Result<TValue, TFailure> result, Func<TValue, TResult> selector) =>
        result.Map(selector);
}
