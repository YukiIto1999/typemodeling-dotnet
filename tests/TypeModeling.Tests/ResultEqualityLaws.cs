using System.Globalization;
using CsCheck;
using TypeModeling.Domain;
using TUnit.Core;

namespace TypeModeling.Tests;

/// <summary>結果の等価、文字列表現、case の取り出しの property 検証</summary>
public sealed class ResultEqualityLaws
{
    private static Result<int, string> Succeed(int value) => Result.Succeeded(value);

    private static Result<int, string> Fail(string failure) => Result.Failed(failure);

    /// <summary>同じ case と同じ値を持つ結果だけの等価</summary>
    [Test]
    public void Only_results_with_the_same_case_and_value_are_equal()
    {
        Gen.Int.Select(Gen.String).Sample((value, reason) =>
        {
            var succeeded = Succeed(value);
            var failed = Fail(reason);
            return succeeded == Succeed(value) && succeeded.Equals((object)Succeed(value)) &&
                   failed == Fail(reason) && failed.Equals((object)Fail(reason)) &&
                   Succeed(0) != failed && failed != succeeded &&
                   succeeded != Succeed(unchecked(value + 1)) && failed != Fail(reason + "x") &&
                   !succeeded.Equals((object)value) && !failed.Equals(null) &&
                   succeeded.GetHashCode() == Succeed(value).GetHashCode() &&
                   failed.GetHashCode() == Fail(reason).GetHashCode();
        });
    }

    /// <summary>辞書のキーとしての使用で有効な case の payload だけを観測するハッシュ</summary>
    [Test]
    public void Dictionary_keys_hash_only_the_active_case_payload()
    {
        Gen.Int.Select(value => value | 1).Sample(value =>
        {
            Result<GuardedPayload, GuardedPayload> succeeded = Result.Succeeded(new GuardedPayload(value));
            Result<GuardedPayload, GuardedPayload> failed = Result.Failed(new GuardedPayload(value));
            var entries = new Dictionary<Result<GuardedPayload, GuardedPayload>, string>
            {
                [succeeded] = "success",
                [failed] = "failure",
            };
            return entries[(Result<GuardedPayload, GuardedPayload>)Result.Succeeded(new GuardedPayload(value))] == "success" &&
                   entries[(Result<GuardedPayload, GuardedPayload>)Result.Failed(new GuardedPayload(value))] == "failure";
        });
    }

    /// <summary>未使用の payload を観測しない結果の文字列化</summary>
    [Test]
    public void Rendering_observes_only_the_active_case_payload()
    {
        Gen.Int.Select(value => value | 1).Sample(value =>
        {
            Result<GuardedPayload, GuardedPayload> succeeded = Result.Succeeded(new GuardedPayload(value));
            Result<GuardedPayload, GuardedPayload> failed = Result.Failed(new GuardedPayload(value));
            var marker = "payload:" + value.ToString(CultureInfo.InvariantCulture);
            return succeeded.ToString().Contains(marker, StringComparison.Ordinal) &&
                   failed.ToString().Contains(marker, StringComparison.Ordinal);
        });
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

    /// <summary>未初期化の payload の観測を拒否する値</summary>
    /// <param name="Value">初期化済みを表す非零の値</param>
    private readonly record struct GuardedPayload(int Value)
    {
        /// <summary>初期化済みの値からのハッシュ</summary>
        /// <returns>保持している非零の値</returns>
        /// <exception cref="InvalidOperationException">未初期化の payload の観測</exception>
        public override int GetHashCode() => Observe();

        /// <summary>初期化済みの payload の表示</summary>
        /// <returns>値を識別できる表示文字列</returns>
        /// <exception cref="InvalidOperationException">未初期化の payload の観測</exception>
        public override string ToString() => "payload:" + Observe().ToString(CultureInfo.InvariantCulture);

        private int Observe() => Value != 0
            ? Value
            : throw new InvalidOperationException("An inactive payload was observed.");
    }
}
