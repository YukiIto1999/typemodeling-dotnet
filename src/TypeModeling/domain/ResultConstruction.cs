namespace TypeModeling.Domain;

/// <summary>型引数を推論させる Result の case の構築</summary>
public static class Result
{
    /// <summary>成功の case の構築</summary>
    /// <typeparam name="TValue">成功の値の型</typeparam>
    /// <param name="value">成功の値</param>
    /// <returns>どの失敗の型の Result にも変換できる成功の case</returns>
    public static Succeeded<TValue> Succeeded<TValue>(TValue value) => new(value);

    /// <summary>失敗の case の構築</summary>
    /// <typeparam name="TFailure">想定内の失敗の型</typeparam>
    /// <param name="failure">想定内の失敗</param>
    /// <returns>どの成功の型の Result にも変換できる失敗の case</returns>
    public static Failed<TFailure> Failed<TFailure>(TFailure failure) => new(failure);
}
