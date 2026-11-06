using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace TypeModeling.Domain;

/// <summary>純粋な計算の成功と想定内の失敗を表す C# 15 の union 型</summary>
/// <remarks>
/// case 型は <see cref="Succeeded{TValue}"/> と <see cref="Failed{TFailure}"/> で、内部の tag は 0 を未初期化、1 を成功、2 を失敗に固定する。
/// 未初期化の値の観測は欠陥として例外を送出する。
/// </remarks>
/// <typeparam name="TValue">成功したときの値の型</typeparam>
/// <typeparam name="TFailure">想定内の失敗を表す型</typeparam>
[Union]
[MustUse]
public readonly struct Result<TValue, TFailure> : IUnion, IEquatable<Result<TValue, TFailure>>
{
    /// <summary>未初期化を表す tag</summary>
    private const byte UninitializedTag = 0;

    /// <summary>成功を表す tag</summary>
    private const byte SucceededTag = 1;

    /// <summary>失敗を表す tag</summary>
    private const byte FailedTag = 2;

    /// <summary>どの case の値を持つかの tag</summary>
    private readonly byte _tag;

    /// <summary>成功の値</summary>
    private readonly TValue _value;

    /// <summary>失敗の値</summary>
    private readonly TFailure _failure;

    /// <summary>成功の case による構築</summary>
    /// <param name="value">成功の case</param>
    public Result(Succeeded<TValue> value)
        : this(SucceededTag, value.Value, default!)
    {
    }

    /// <summary>失敗の case による構築</summary>
    /// <param name="value">失敗の case</param>
    public Result(Failed<TFailure> value)
        : this(FailedTag, default!, value.Failure)
    {
    }

    /// <summary>tag と、tag が示す case の値による構築</summary>
    /// <param name="tag">持つ case の tag</param>
    /// <param name="value">成功の値。失敗なら既定値</param>
    /// <param name="failure">失敗の値。成功なら既定値</param>
    private Result(byte tag, TValue value, TFailure failure)
    {
        _tag = tag;
        _value = value;
        _failure = failure;
    }

    /// <summary>持つ case の値。未初期化なら null</summary>
    public object? Value => _tag switch
    {
        SucceededTag => new Succeeded<TValue>(_value),
        FailedTag => new Failed<TFailure>(_failure),
        _ => null,
    };

    /// <summary>case の値を持つかの判定</summary>
    public bool HasValue => _tag != UninitializedTag;

    /// <summary>成功の case の取得</summary>
    /// <param name="value">成功の case</param>
    /// <returns>成功なら true</returns>
    public bool TryGetValue(out Succeeded<TValue> value)
    {
        value = new Succeeded<TValue>(_value);
        return _tag == SucceededTag;
    }

    /// <summary>失敗の case の取得</summary>
    /// <param name="value">失敗の case</param>
    /// <returns>失敗なら true</returns>
    public bool TryGetValue(out Failed<TFailure> value)
    {
        value = new Failed<TFailure>(_failure);
        return _tag == FailedTag;
    }

    /// <summary>失敗なら失敗の case を、成功なら成功の値を取り出す判定</summary>
    /// <param name="failed">失敗のときの失敗の case</param>
    /// <param name="value">成功のときの成功の値</param>
    /// <returns>失敗なら true</returns>
    /// <remarks>失敗を呼び出し元の Result へそのまま返す早期 return に使う</remarks>
    public bool IsFailed(out Failed<TFailure> failed, [MaybeNullWhen(true)] out TValue value)
    {
        EnsureInitialized();
        failed = new Failed<TFailure>(_failure);
        value = _value;
        return _tag == FailedTag;
    }

    /// <summary>成功と失敗のそれぞれに対応する値への畳み込み</summary>
    /// <typeparam name="TResult">畳み込んだ値の型</typeparam>
    /// <param name="onSucceeded">成功の値からの変換</param>
    /// <param name="onFailed">失敗の値からの変換</param>
    /// <returns>どちらかの変換の結果</returns>
    public TResult Match<TResult>(Func<TValue, TResult> onSucceeded, Func<TFailure, TResult> onFailed)
    {
        EnsureInitialized();
        return _tag == SucceededTag ? onSucceeded(_value) : onFailed(_failure);
    }

    /// <summary>成功の case から Result への union 変換</summary>
    /// <param name="value">成功の case</param>
    /// <returns>成功の Result</returns>
    public static implicit operator Result<TValue, TFailure>(Succeeded<TValue> value) => new(value);

    /// <summary>失敗の case から Result への union 変換</summary>
    /// <param name="value">失敗の case</param>
    /// <returns>失敗の Result</returns>
    public static implicit operator Result<TValue, TFailure>(Failed<TFailure> value) => new(value);

    /// <summary>同じ case と同じ値を持つかの判定</summary>
    /// <param name="other">比較する相手</param>
    /// <returns>同じ case と値なら true</returns>
    public bool Equals(Result<TValue, TFailure> other) =>
        _tag == other._tag && _tag switch
        {
            SucceededTag => EqualityComparer<TValue>.Default.Equals(_value, other._value),
            FailedTag => EqualityComparer<TFailure>.Default.Equals(_failure, other._failure),
            _ => true,
        };

    /// <summary>同じ case と同じ値を持つ Result かの判定</summary>
    /// <param name="obj">比較する相手</param>
    /// <returns>同じ case と値の Result なら true</returns>
    public override bool Equals(object? obj) => obj is Result<TValue, TFailure> other && Equals(other);

    /// <summary>case と値のハッシュ値</summary>
    /// <returns>case と値から求めたハッシュ値</returns>
    public override int GetHashCode() => _tag switch
    {
        SucceededTag => HashCode.Combine(_tag, _value),
        FailedTag => HashCode.Combine(_tag, _failure),
        _ => 0,
    };

    /// <summary>持つ case の文字列表現</summary>
    /// <returns>case の文字列表現。未初期化なら空文字列</returns>
    public override string ToString() => Value?.ToString() ?? string.Empty;

    /// <summary>同じ case と同じ値を持つかの等価演算</summary>
    /// <param name="left">左辺</param>
    /// <param name="right">右辺</param>
    /// <returns>同じ case と値なら true</returns>
    public static bool operator ==(Result<TValue, TFailure> left, Result<TValue, TFailure> right) => left.Equals(right);

    /// <summary>違う case か違う値を持つかの非等価演算</summary>
    /// <param name="left">左辺</param>
    /// <param name="right">右辺</param>
    /// <returns>違う case か値なら true</returns>
    public static bool operator !=(Result<TValue, TFailure> left, Result<TValue, TFailure> right) => !left.Equals(right);

    /// <summary>未初期化の観測を欠陥として閉じる確認</summary>
    /// <exception cref="InvalidOperationException">未初期化の Result を観測した場合</exception>
    private void EnsureInitialized()
    {
        if (_tag == UninitializedTag)
            throw new InvalidOperationException("未初期化の Result を観測した。");
    }
}
