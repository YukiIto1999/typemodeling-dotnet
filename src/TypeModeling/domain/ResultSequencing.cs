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
        result switch
        {
            Result<TValue, TFailure>.Succeeded succeeded => bind(succeeded.Value),
            Result<TValue, TFailure>.Failed failed =>
                new Result<TResult, TFailure>.Failed(failed.Failure),
        };
}
