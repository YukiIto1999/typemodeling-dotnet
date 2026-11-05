namespace TypeModeling.Tests;

/// <summary>例外の送出の確認</summary>
internal static class Expect
{
    /// <summary>指定した型の例外を送出することの確認</summary>
    /// <typeparam name="TException">送出されるはずの例外の型</typeparam>
    /// <param name="action">例外を送出するはずの処理</param>
    /// <returns>送出された例外</returns>
    /// <exception cref="InvalidOperationException">指定した例外を送出しなかった場合</exception>
    internal static TException Throws<TException>(Func<object> action)
        where TException : Exception
    {
        try
        {
            _ = action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(typeof(TException).Name + " が送出されなかった。");
    }
}
