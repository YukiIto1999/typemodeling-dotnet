namespace TypeModeling.Domain;

/// <summary>想定内の失敗を扱う combinator</summary>
public static class ResultFailureHandling
{
    /// <summary>想定内の失敗だけを写す型の変換</summary>
    /// <typeparam name="TFailure">写像前の失敗の型</typeparam>
    /// <typeparam name="TFailureMapped">写像後の失敗の型</typeparam>
    /// <typeparam name="TValue">成功の値の型</typeparam>
    /// <param name="result">写像の対象になる結果</param>
    /// <param name="selector">想定内の失敗を変換する関数</param>
    /// <returns>失敗の型を写した結果</returns>
    public static Result<TValue, TFailureMapped> MapFailure<TFailure, TFailureMapped, TValue>(
        this Result<TValue, TFailure> result, Func<TFailure, TFailureMapped> selector) =>
        result switch
        {
            Result<TValue, TFailure>.Succeeded succeeded =>
                new Result<TValue, TFailureMapped>.Succeeded(succeeded.Value),
            Result<TValue, TFailure>.Failed failed =>
                new Result<TValue, TFailureMapped>.Failed(selector(failed.Failure)),
        };
}
