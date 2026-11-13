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

        if (uninitialized.HasValue)
            throw new InvalidOperationException("An uninitialized result reported an initialized case.");

        _ = Expect.Throws<InvalidOperationException>(() => uninitialized.Value is not null);
        _ = Expect.Throws<InvalidOperationException>(() => uninitialized.TryGetValue(out Succeeded<int> _));
        _ = Expect.Throws<InvalidOperationException>(() => uninitialized.TryGetValue(out Failed<string> _));
        _ = Expect.Throws<InvalidOperationException>(() => uninitialized.Match<int>(
            _ => throw new System.Diagnostics.UnreachableException("An uninitialized result selected success."),
            _ => throw new System.Diagnostics.UnreachableException("An uninitialized result selected failure.")));
        _ = Expect.Throws<InvalidOperationException>(() => uninitialized.IsFailed(out _, out _));
        _ = Expect.Throws<InvalidOperationException>(() => uninitialized.GetHashCode());
        _ = Expect.Throws<InvalidOperationException>(() => uninitialized.ToString());
        _ = Expect.Throws<InvalidOperationException>(() => uninitialized.Equals(null));
        _ = Expect.Throws<InvalidOperationException>(() => uninitialized.Equals(new object()));

        Result<int, string>[] others = [uninitialized, Result.Succeeded(0), Result.Failed("e")];
        foreach (var other in others)
        {
            _ = Expect.Throws<InvalidOperationException>(() => uninitialized.Equals(other));
            _ = Expect.Throws<InvalidOperationException>(() => other.Equals(uninitialized));
            _ = Expect.Throws<InvalidOperationException>(() => uninitialized.Equals((object)other));
            _ = Expect.Throws<InvalidOperationException>(() => other.Equals((object)uninitialized));
            _ = Expect.Throws<InvalidOperationException>(() => _ = uninitialized == other);
            _ = Expect.Throws<InvalidOperationException>(() => _ = other == uninitialized);
            _ = Expect.Throws<InvalidOperationException>(() => _ = uninitialized != other);
            _ = Expect.Throws<InvalidOperationException>(() => _ = other != uninitialized);
        }
    }

    /// <summary>null payload を持つ初期化済み case の区別</summary>
    [Test]
    public void Null_payloads_are_initialized_and_preserve_their_cases()
    {
        Result<string?, string?> succeeded = Result.Succeeded<string?>(null);
        Result<string?, string?> failed = Result.Failed<string?>(null);
        Result<string?, string?> sameSuccess = Result.Succeeded<string?>(null);
        Result<string?, string?> sameFailure = Result.Failed<string?>(null);

        if (!succeeded.HasValue || !failed.HasValue ||
            succeeded.Value is not Succeeded<string?> { Value: null } ||
            failed.Value is not Failed<string?> { Failure: null } ||
            !succeeded.TryGetValue(out Succeeded<string?> success) || success.Value is not null ||
            succeeded.TryGetValue(out Failed<string?> _) ||
            !failed.TryGetValue(out Failed<string?> failure) || failure.Failure is not null ||
            failed.TryGetValue(out Succeeded<string?> _) ||
            succeeded.IsFailed(out _, out var value) || value is not null ||
            !failed.IsFailed(out var error, out _) || error.Failure is not null ||
            !succeeded.Match(item => item is null, _ => false) ||
            !failed.Match(_ => false, item => item is null) ||
            succeeded == failed || failed == succeeded ||
            succeeded != sameSuccess || failed != sameFailure ||
            !succeeded.Equals((object)sameSuccess) || !failed.Equals((object)sameFailure) ||
            succeeded.GetHashCode() != sameSuccess.GetHashCode() ||
            failed.GetHashCode() != sameFailure.GetHashCode() ||
            succeeded.ToString() != new Succeeded<string?>(null).ToString() ||
            failed.ToString() != new Failed<string?>(null).ToString())
            throw new InvalidOperationException("Null payloads did not preserve initialized case semantics.");
    }
}
