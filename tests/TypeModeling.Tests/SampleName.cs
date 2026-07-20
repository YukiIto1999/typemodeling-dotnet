using TypeModeling.Domain;

namespace TypeModeling.Tests.Samples;

/// <summary>前後の空白を除いて検証したテスト専用の名前</summary>
/// <remarks>string を基底値に持つ正規化付き値オブジェクト生成の検証対象</remarks>
[ValueObject<string>]
public sealed partial record SampleName
{
    /// <summary>前後の空白を除いた外部表現の内容検証</summary>
    /// <param name="raw">検証していない外部表現</param>
    /// <returns>前後の空白を除いた名前または空白だけであることを表す失敗</returns>
    private static Result<string, SampleNameFailure> Parse(string raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? new Result<string, SampleNameFailure>.Failed(new SampleNameFailure.Blank())
            : new Result<string, SampleNameFailure>.Succeeded(raw.Trim());
}

/// <summary>テスト専用の名前の検証に失敗した理由を表す閉じた階層</summary>
[ClosedUnion]
public abstract record SampleNameFailure
{
    /// <summary>空白だけであること以外の失敗理由の追加を閉じる基底の構築</summary>
    private SampleNameFailure()
    {
    }

    /// <summary>名前が空白だけであることを表す失敗</summary>
    public sealed record Blank : SampleNameFailure;
}
