using TypeModeling.Domain;

namespace TypeModeling.Tests.Samples;

/// <summary>空ではないことを検証したテスト専用の識別子</summary>
/// <remarks>Guid を基底値に持つ値オブジェクト生成の検証対象</remarks>
[ValueObject<Guid>]
public sealed partial record SampleIdentifier
{
    /// <summary>外部表現が空でないことの検証</summary>
    /// <param name="raw">検証していない外部表現</param>
    /// <returns>空でない識別子または空であることを表す失敗</returns>
    private static Result<Guid, SampleIdentifierFailure> Parse(Guid raw) =>
        raw == Guid.Empty
            ? new Result<Guid, SampleIdentifierFailure>.Failed(new SampleIdentifierFailure.Empty())
            : new Result<Guid, SampleIdentifierFailure>.Succeeded(raw);
}

/// <summary>テスト専用の識別子の検証に失敗した理由を表す閉じた階層</summary>
[ClosedUnion]
public abstract record SampleIdentifierFailure
{
    /// <summary>空以外の失敗理由の追加を閉じる基底の構築</summary>
    private SampleIdentifierFailure()
    {
    }

    /// <summary>識別子が空であることを表す失敗</summary>
    public sealed record Empty : SampleIdentifierFailure;
}
