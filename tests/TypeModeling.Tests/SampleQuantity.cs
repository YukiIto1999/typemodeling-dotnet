using TypeModeling.Domain;

namespace TypeModeling.Tests.Samples;

/// <summary>正であることを検証したテスト専用の数量</summary>
/// <remarks>int を基底値に持つ値オブジェクト生成の検証対象</remarks>
[ValueObject<int>]
public sealed partial record SampleQuantity
{
    /// <summary>外部表現が正であることの検証</summary>
    /// <param name="raw">検証していない外部表現</param>
    /// <returns>正の数量または正でない値を持つ失敗</returns>
    private static Result<int, SampleQuantityFailure> Parse(int raw) =>
        raw <= 0
            ? new Result<int, SampleQuantityFailure>.Failed(new SampleQuantityFailure.NotPositive(raw))
            : new Result<int, SampleQuantityFailure>.Succeeded(raw);
}

/// <summary>テスト専用の数量の検証に失敗した理由を表す閉じた階層</summary>
[ClosedUnion]
public abstract record SampleQuantityFailure
{
    /// <summary>正でないこと以外の失敗理由の追加を閉じる基底の構築</summary>
    private SampleQuantityFailure()
    {
    }

    /// <summary>数量が正でないことを表す失敗</summary>
    /// <param name="Value">検証を拒否された数量</param>
    public sealed record NotPositive(int Value) : SampleQuantityFailure;
}
