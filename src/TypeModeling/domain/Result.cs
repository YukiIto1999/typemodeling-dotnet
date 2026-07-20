namespace TypeModeling.Domain;

/// <summary>純粋な計算の成功と想定内の失敗を型に現す閉じた階層</summary>
/// <typeparam name="TValue">成功したときに返す値の型</typeparam>
/// <typeparam name="TFailure">想定内の失敗を表す型</typeparam>
[ClosedUnion]
public abstract record Result<TValue, TFailure>
{
    /// <summary>成功と失敗以外の派生を防ぐ基底の構築</summary>
    private Result()
    {
    }

    /// <summary>成功して値を返した結果</summary>
    /// <param name="Value">成功して得られた値</param>
    public sealed record Succeeded(TValue Value) : Result<TValue, TFailure>;

    /// <summary>想定内の失敗で終わった結果</summary>
    /// <param name="Failure">起きた想定内の失敗</param>
    public sealed record Failed(TFailure Failure) : Result<TValue, TFailure>;
}
