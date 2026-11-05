using CsCheck;
using TypeModeling.Domain;
using TUnit.Core;

namespace TypeModeling.Tests;

/// <summary>結果の等価、文字列表現、case の取り出しの property 検証</summary>
public sealed class ResultEqualityLaws
{
    private static Result<int, string> Succeed(int value) => Result.Succeeded(value);

    private static Result<int, string> Fail(string failure) => Result.Failed(failure);

    private static Result<int, string> Uninitialized() => new Result<int, string>[1].Single();

    /// <summary>同じ case と同じ値を持つ結果だけの等価</summary>
    [Test]
    public void Only_results_with_the_same_case_and_value_are_equal()
    {
        Gen.Int.Select(Gen.String).Sample((value, reason) =>
        {
            var succeeded = Succeed(value);
            var failed = Fail(reason);
            var uninitialized = Uninitialized();
            return succeeded == Succeed(value) && succeeded.Equals((object)Succeed(value)) &&
                   failed == Fail(reason) && uninitialized == Uninitialized() &&
                   Succeed(0) != failed && failed != succeeded &&
                   succeeded != Succeed(unchecked(value + 1)) && failed != Fail(reason + "x") &&
                   succeeded != uninitialized && !succeeded.Equals((object)value);
        });
    }

    /// <summary>持つ case の文字列表現と、未初期化の空文字列</summary>
    [Test]
    public void ToString_shows_the_case_it_holds()
    {
        Gen.Int.Select(Gen.String).Sample((value, reason) =>
            Succeed(value).ToString() == new Succeeded<int>(value).ToString() &&
            Fail(reason).ToString() == new Failed<string>(reason).ToString() &&
            Uninitialized().ToString().Length == 0);
    }

    /// <summary>TryGetValue による持つ case だけの取り出し</summary>
    [Test]
    public void TryGetValue_takes_out_only_the_case_it_holds()
    {
        Gen.Int.Select(Gen.String).Sample((value, reason) =>
            Succeed(value).TryGetValue(out Succeeded<int> succeeded) && succeeded.Value == value &&
            !Succeed(value).TryGetValue(out Failed<string> _) &&
            Fail(reason).TryGetValue(out Failed<string> failed) && failed.Failure == reason &&
            !Fail(reason).TryGetValue(out Succeeded<int> _));
    }
}
