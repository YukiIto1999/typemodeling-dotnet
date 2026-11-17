namespace TypeModeling.Analyzers.Tests;

/// <summary>generator と analyzer の検証に使う、基底値が int の Positive を一つ宣言する source の構築</summary>
internal static class ValueObjectSources
{
    /// <summary>適合する型宣言の頭</summary>
    internal const string SealedPositive = "public sealed partial record Positive";

    /// <summary>Parse を持つ Positive の宣言を含む source の構築</summary>
    /// <param name="declaration">属性の直後に置く型宣言の頭</param>
    /// <param name="members">型宣言の本体へ足す member</param>
    /// <returns>generator に渡す source</returns>
    internal static string Declare(string declaration, string members = "") =>
        $$"""
            using TypeModeling.Domain;

            namespace Probe;

            public sealed record ValidationFailure;

            public record Base;

            [ValueObject<int>]
            {{declaration}}
            {
                {{members}}

                private static Result<int, ValidationFailure> Parse(int raw) => throw null!;
            }
            """;
}
