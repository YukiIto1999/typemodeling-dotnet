using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>case を挙げて分岐した位置の下を、型を調べるだけの case が受け取るかを、variant interface の参照変換で判定すること</summary>
public sealed class ExplicitCaseVarianceReachTests
{
    /// <summary>共変と反変の interface、参照型の引数で実装する sealed な record、値型の引数で実装する sealed な record の宣言</summary>
    private const string Declarations = """

        internal interface IOut<out T> { }

        internal interface IIn<in T> { }

        internal sealed record Covariant : IOut<string>
        {
            public PaymentState State { get; init; } = new PaymentState.Pending();
        }

        internal sealed record Contravariant : IIn<object>
        {
            public PaymentState State { get; init; } = new PaymentState.Pending();
        }

        internal sealed record BoxedOut : IOut<int>
        {
            public PaymentState State { get; init; } = new PaymentState.Pending();
        }

        internal sealed record BoxedIn : IIn<int>
        {
            public PaymentState State { get; init; } = new PaymentState.Pending();
        }
        """;

    /// <summary>sealed な型が参照変換で一致する variant interface の case を、分岐した位置の中身を隠すものとして拒否すること</summary>
    [Test]
    public async Task A_variant_interface_that_a_sealed_scope_converts_to_is_rejected_where_it_hides_the_payload()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int Covariance(object value)
                {
                    var result = 0;
                    switch (value)
                    {
                        case Covariant { State: PaymentState.Pending }: result = 1; break;
                        case IOut<object>: result = 2; break;
                    }

                    return result;
                }

                internal static int Contravariance(object value)
                {
                    var result = 0;
                    switch (value)
                    {
                        case Contravariant { State: PaymentState.Pending }: result = 1; break;
                        case IIn<string>: result = 2; break;
                    }

                    return result;
                }
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["IOut<object>", "IIn<string>"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>値型の引数で実装する型は、box を要する変換と別の値型への変換では variant interface と一致しないので、case を許可すること</summary>
    [Test]
    public async Task A_variant_interface_that_only_a_boxing_or_numeric_conversion_reaches_is_accepted()
    {
        var diagnostics = await RunClosed(Payment() + Declarations + """

            internal static class Handler
            {
                internal static int Covariance(object value)
                {
                    var result = 0;
                    switch (value)
                    {
                        case BoxedOut { State: PaymentState.Pending }: result = 1; break;
                        case IOut<object>: result = 2; break;
                        case IOut<long>: result = 3; break;
                    }

                    return result;
                }

                internal static int Contravariance(object value)
                {
                    var result = 0;
                    switch (value)
                    {
                        case BoxedIn { State: PaymentState.Pending }: result = 1; break;
                        case IIn<object>: result = 2; break;
                        case IIn<long>: result = 3; break;
                    }

                    return result;
                }
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
