using TypeModeling.Domain;

namespace TypeModeling.Tests.Support;

/// <summary>生成された値オブジェクトが満たす規則の判定</summary>
internal static class ValueObjectLawAssertions
{
    /// <summary>有効な入力の受理と Parse が返した基底値の保持</summary>
    /// <typeparam name="TValue">構築される値オブジェクトの型</typeparam>
    /// <typeparam name="TRaw">構築の入力になる基底値の型</typeparam>
    /// <typeparam name="TFailure">構築に失敗した理由の型</typeparam>
    /// <param name="raw">検証していない外部表現</param>
    /// <param name="expected">Parse が返す基底値</param>
    /// <param name="create">入力を検証して値オブジェクトを構築する関数</param>
    /// <param name="underlying">値オブジェクトから基底値を取り出す関数</param>
    /// <returns>成功して期待する基底値を保持した場合は true</returns>
    public static bool SucceedsWith<TValue, TRaw, TFailure>(
        TRaw raw,
        TRaw expected,
        Func<TRaw, Result<TValue, TFailure>> create,
        Func<TValue, TRaw> underlying) =>
        create(raw) is Result<TValue, TFailure>.Succeeded succeeded &&
        EqualityComparer<TRaw>.Default.Equals(underlying(succeeded.Value), expected);

    /// <summary>無効な入力の拒否と Parse が返した失敗理由の保持</summary>
    /// <typeparam name="TValue">構築される値オブジェクトの型</typeparam>
    /// <typeparam name="TRaw">構築の入力になる基底値の型</typeparam>
    /// <typeparam name="TFailure">構築に失敗した理由の型</typeparam>
    /// <param name="raw">検証していない外部表現</param>
    /// <param name="expected">Parse が返す失敗理由</param>
    /// <param name="create">入力を検証して値オブジェクトを構築する関数</param>
    /// <returns>失敗して期待する理由を保持した場合は true</returns>
    public static bool FailsWith<TValue, TRaw, TFailure>(
        TRaw raw,
        TFailure expected,
        Func<TRaw, Result<TValue, TFailure>> create) =>
        create(raw) is Result<TValue, TFailure>.Failed failed &&
        EqualityComparer<TFailure>.Default.Equals(failed.Failure, expected);

    /// <summary>保持値を入力にした再構築の冪等性</summary>
    /// <typeparam name="TValue">構築される値オブジェクトの型</typeparam>
    /// <typeparam name="TRaw">構築の入力になる基底値の型</typeparam>
    /// <typeparam name="TFailure">構築に失敗した理由の型</typeparam>
    /// <param name="raw">規則に沿う外部表現</param>
    /// <param name="create">入力を検証して値オブジェクトを構築する関数</param>
    /// <param name="underlying">値オブジェクトから基底値を取り出す関数</param>
    /// <returns>初回と再構築後の保持値が等しい場合は true</returns>
    public static bool RebuildsIdempotently<TValue, TRaw, TFailure>(
        TRaw raw,
        Func<TRaw, Result<TValue, TFailure>> create,
        Func<TValue, TRaw> underlying)
    {
        if (create(raw) is not Result<TValue, TFailure>.Succeeded first)
            return false;

        var firstUnderlying = underlying(first.Value);
        return create(firstUnderlying) is Result<TValue, TFailure>.Succeeded rebuilt &&
               EqualityComparer<TRaw>.Default.Equals(underlying(rebuilt.Value), firstUnderlying);
    }

    /// <summary>同じ値へ正規化される基底値から構築した値オブジェクトの等価性</summary>
    /// <typeparam name="TValue">構築される値オブジェクトの型</typeparam>
    /// <typeparam name="TRaw">構築の入力になる基底値の型</typeparam>
    /// <typeparam name="TFailure">構築に失敗した理由の型</typeparam>
    /// <param name="firstRaw">一つ目の規則に沿う外部表現</param>
    /// <param name="secondRaw">二つ目の規則に沿う外部表現</param>
    /// <param name="create">入力を検証して値オブジェクトを構築する関数</param>
    /// <returns>二つの値オブジェクトが等しい場合は true</returns>
    public static bool EqualCreations<TValue, TRaw, TFailure>(
        TRaw firstRaw,
        TRaw secondRaw,
        Func<TRaw, Result<TValue, TFailure>> create) =>
        create(firstRaw) is Result<TValue, TFailure>.Succeeded first &&
        create(secondRaw) is Result<TValue, TFailure>.Succeeded second &&
        EqualityComparer<TValue>.Default.Equals(first.Value, second.Value);

    /// <summary>異なる基底値から構築した値オブジェクトの非等価性</summary>
    /// <typeparam name="TValue">構築される値オブジェクトの型</typeparam>
    /// <typeparam name="TRaw">構築の入力になる基底値の型</typeparam>
    /// <typeparam name="TFailure">構築に失敗した理由の型</typeparam>
    /// <param name="firstRaw">一つ目の規則に沿う外部表現</param>
    /// <param name="secondRaw">二つ目の規則に沿う外部表現</param>
    /// <param name="create">入力を検証して値オブジェクトを構築する関数</param>
    /// <returns>二つの値オブジェクトが等しくない場合は true</returns>
    public static bool UnequalCreations<TValue, TRaw, TFailure>(
        TRaw firstRaw,
        TRaw secondRaw,
        Func<TRaw, Result<TValue, TFailure>> create) =>
        create(firstRaw) is Result<TValue, TFailure>.Succeeded first &&
        create(secondRaw) is Result<TValue, TFailure>.Succeeded second &&
        !EqualityComparer<TValue>.Default.Equals(first.Value, second.Value);

    /// <summary>等しい値オブジェクトのハッシュ値の整合</summary>
    /// <typeparam name="TValue">構築される値オブジェクトの型</typeparam>
    /// <typeparam name="TRaw">構築の入力になる基底値の型</typeparam>
    /// <typeparam name="TFailure">構築に失敗した理由の型</typeparam>
    /// <param name="raw">規則に沿う外部表現</param>
    /// <param name="create">入力を検証して値オブジェクトを構築する関数</param>
    /// <returns>等しい値オブジェクトのハッシュ値が等しい場合は true</returns>
    public static bool EqualCreationsHaveEqualHashes<TValue, TRaw, TFailure>(
        TRaw raw,
        Func<TRaw, Result<TValue, TFailure>> create) =>
        create(raw) is Result<TValue, TFailure>.Succeeded first &&
        create(raw) is Result<TValue, TFailure>.Succeeded second &&
        EqualityComparer<TValue>.Default.Equals(first.Value, second.Value) &&
        first.Value?.GetHashCode() == second.Value?.GetHashCode();

    /// <summary>値オブジェクトの文字列表現と保持値の文字列表現の整合</summary>
    /// <typeparam name="TValue">構築される値オブジェクトの型</typeparam>
    /// <typeparam name="TRaw">構築の入力になる基底値の型</typeparam>
    /// <typeparam name="TFailure">構築に失敗した理由の型</typeparam>
    /// <param name="raw">規則に沿う外部表現</param>
    /// <param name="create">入力を検証して値オブジェクトを構築する関数</param>
    /// <param name="underlying">値オブジェクトから基底値を取り出す関数</param>
    /// <returns>値オブジェクトと保持値の文字列表現が等しい場合は true</returns>
    public static bool StringifiesAsUnderlying<TValue, TRaw, TFailure>(
        TRaw raw,
        Func<TRaw, Result<TValue, TFailure>> create,
        Func<TValue, TRaw> underlying) =>
        create(raw) is Result<TValue, TFailure>.Succeeded succeeded &&
        succeeded.Value?.ToString() == underlying(succeeded.Value)?.ToString();
}
