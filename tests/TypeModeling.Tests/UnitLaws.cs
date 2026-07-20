using TypeModeling.Domain;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace TypeModeling.Tests;

/// <summary>値を持たない成功の型がただ一つの値として常に等価であることの確認</summary>
public sealed class UnitLaws
{
    /// <summary>任意の Unit 値同士の等価性</summary>
    [Test]
    public async Task Any_two_unit_values_are_always_equal()
    {
        var first = Unit.Value;
        var second = Unit.Value;

        await Assert.That(first == second).IsTrue();
        await Assert.That(first != second).IsFalse();
        await Assert.That(first.Equals(second)).IsTrue();
    }
}
