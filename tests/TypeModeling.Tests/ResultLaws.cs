using CsCheck;
using TypeModeling.Domain;
using TUnit.Core;

namespace TypeModeling.Tests;

/// <summary>結果が functor と monad の法則を満たし成功と失敗を判別できることの property 検証</summary>
public sealed class ResultLaws
{
    private static Result<int, string> Succeed(int value) =>
        new Result<int, string>.Succeeded(value);

    private static Result<int, string> Fail(string failure) =>
        new Result<int, string>.Failed(failure);

    private static Result<int, string> Continue(int value) =>
        value % 2 == 0 ? Succeed(value + 1) : Fail("odd");

    private static Result<int, string> Branch(int value) =>
        value > 100 ? Fail("large") : Succeed(value * 2);

    private static int Discriminate(Result<int, string> result) =>
        result switch
        {
            Result<int, string>.Succeeded succeeded => succeeded.Value,
            Result<int, string>.Failed => 0,
        };

    /// <summary>Map の functor identity law</summary>
    [Test]
    public void Map_satisfies_the_functor_identity_law()
    {
        Gen.Int.Sample(value =>
            Succeed(value).Map(item => item) == Succeed(value) &&
            Fail("e").Map(item => item) == Fail("e"));
    }

    /// <summary>Map の functor composition law</summary>
    [Test]
    public void Map_satisfies_the_functor_composition_law()
    {
        Gen.Int.Sample(value =>
        {
            int F(int item) => item + 7;
            int G(int item) => item * 3;
            return Succeed(value).Map(F).Map(G) == Succeed(value).Map(item => G(F(item))) &&
                   Fail("e").Map(F).Map(G) == Fail("e").Map(item => G(F(item)));
        });
    }

    /// <summary>Bind の monad left identity law</summary>
    [Test]
    public void Bind_satisfies_the_monad_left_identity_law()
    {
        Gen.Int.Sample(value => Succeed(value).Bind(Continue) == Continue(value));
    }

    /// <summary>Bind の monad right identity law</summary>
    [Test]
    public void Bind_satisfies_the_monad_right_identity_law()
    {
        Gen.Int.Sample(value =>
            Succeed(value).Bind(Succeed) == Succeed(value) &&
            Fail("e").Bind(Succeed) == Fail("e"));
    }

    /// <summary>Bind の monad associativity law</summary>
    [Test]
    public void Bind_satisfies_the_monad_associativity_law()
    {
        Gen.Int.Sample(value =>
            Succeed(value).Bind(Continue).Bind(Branch) ==
            Succeed(value).Bind(item => Continue(item).Bind(Branch)) &&
            Fail("e").Bind(Continue).Bind(Branch) ==
            Fail("e").Bind(item => Continue(item).Bind(Branch)));
    }

    /// <summary>MapFailure の functor identity law</summary>
    [Test]
    public void MapFailure_satisfies_the_functor_identity_law()
    {
        Gen.Int.Sample(value =>
            Succeed(value).MapFailure(failure => failure) == Succeed(value) &&
            Fail("e").MapFailure(failure => failure) == Fail("e"));
    }

    /// <summary>MapFailure の functor composition law</summary>
    [Test]
    public void MapFailure_satisfies_the_functor_composition_law()
    {
        Gen.Int.Sample(value =>
        {
            string F(string failure) => failure + "a";
            string G(string failure) => failure + "b";
            return Succeed(value).MapFailure(F).MapFailure(G) ==
                   Succeed(value).MapFailure(failure => G(F(failure))) &&
                   Fail("e").MapFailure(F).MapFailure(G) ==
                   Fail("e").MapFailure(failure => G(F(failure)));
        });
    }

    /// <summary>Succeeded と Failed の網羅的な判別</summary>
    [Test]
    public void Succeeded_and_Failed_are_discriminated_by_an_exhaustive_switch()
    {
        Gen.Int.Sample(value =>
            Discriminate(Succeed(value)) == value &&
            Discriminate(Fail("e")) == 0);
    }
}
