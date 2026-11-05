using CsCheck;
using TypeModeling.Domain;
using TUnit.Core;

namespace TypeModeling.Tests;

/// <summary>結果の観測と、成功の型が違う結果への失敗の受け渡しの property 検証</summary>
public sealed class ResultObservationLaws
{
    /// <summary>IsFailed による失敗の取り出しと成功値の取り出し</summary>
    [Test]
    public void IsFailed_separates_the_failure_from_the_value()
    {
        Gen.Int.Sample(value =>
        {
            Result<int, string> succeeded = Result.Succeeded(value);
            Result<int, string> failed = Result.Failed("e");
            return !succeeded.IsFailed(out _, out var kept) && kept == value &&
                   failed.IsFailed(out var failure, out _) && failure.Failure == "e";
        });
    }

    /// <summary>失敗の case の、成功の型が違う結果へのそのままの受け渡し</summary>
    [Test]
    public void A_failure_passes_to_a_result_of_another_value_type_unchanged()
    {
        Gen.String.Sample(reason =>
        {
            Result<int, string> source = Result.Failed(reason);
            Result<bool, string> propagated = source.IsFailed(out var failed, out var value) ? failed : Result.Succeeded(value > 0);
            return propagated == Result.Failed(reason);
        });
    }

    /// <summary>Match による成功と失敗それぞれの畳み込み</summary>
    [Test]
    public void Match_folds_each_case_with_its_function()
    {
        Gen.Int.Select(Gen.String).Sample((value, reason) =>
            ((Result<int, string>)Result.Succeeded(value)).Match(item => item + 1, _ => 0) == value + 1 &&
            ((Result<int, string>)Result.Failed(reason)).Match(item => item + 1, failure => -failure.Length - 1) == -reason.Length - 1);
    }

    /// <summary>Value に入る case と HasValue の対応</summary>
    [Test]
    public void Value_holds_the_case_that_built_the_result()
    {
        Gen.Int.Sample(value =>
            ((Result<int, string>)Result.Succeeded(value)).Value is Succeeded<int> { Value: var kept } && kept == value &&
            ((Result<int, string>)Result.Failed("e")).Value is Failed<string> { Failure: "e" } &&
            ((Result<int, string>)Result.Succeeded(value)).HasValue);
    }

    /// <summary>未初期化の結果の観測の欠陥としての送出</summary>
    [Test]
    public void Observing_an_uninitialized_result_is_a_defect()
    {
        var uninitialized = new Result<int, string>[1].Single();

        if (uninitialized.HasValue || uninitialized.Value is not null)
            throw new InvalidOperationException("未初期化の Result が case の値を持っている。");

        var defect = Expect.Throws<InvalidOperationException>(() => uninitialized.Match(item => item, _ => 0));
        _ = Expect.Throws<InvalidOperationException>(() => uninitialized.IsFailed(out _, out _));
        if (defect.Message != "未初期化の Result を観測した。")
            throw new InvalidOperationException("未初期化の観測の例外が理由を述べていない: " + defect.Message);
    }
}
