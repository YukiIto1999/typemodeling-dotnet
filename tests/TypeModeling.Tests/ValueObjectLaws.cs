using CsCheck;
using TypeModeling.Tests.Samples;
using TypeModeling.Tests.Support;
using TUnit.Core;

namespace TypeModeling.Tests;

/// <summary>生成された値オブジェクトの検証付き構築と値の振る舞いの property 検証</summary>
public sealed class ValueObjectLaws
{
    /// <summary>空でない Guid による識別子の構築成功</summary>
    [Test]
    public void A_sample_identifier_succeeds_with_its_non_empty_guid() =>
        ValueObjectGenerators.ValidIdentifier.Sample(raw =>
            ValueObjectLawAssertions.SucceedsWith(raw, raw, SampleIdentifier.Create, value => value.Value));

    /// <summary>空の Guid による識別子の構築失敗</summary>
    [Test]
    public void A_sample_identifier_fails_with_empty_for_an_empty_guid() =>
        ValueObjectGenerators.InvalidIdentifier.Sample(raw =>
            ValueObjectLawAssertions.FailsWith(raw, new SampleIdentifierFailure.Empty(), SampleIdentifier.Create));

    /// <summary>前後空白を除いた名前の構築成功</summary>
    [Test]
    public void A_sample_name_succeeds_with_its_trimmed_text() =>
        ValueObjectGenerators.ValidName.Sample(raw =>
            ValueObjectLawAssertions.SucceedsWith(raw, raw.Trim(), SampleName.Create, value => value.Value));

    /// <summary>空白だけの名前の構築失敗</summary>
    [Test]
    public void A_sample_name_fails_with_blank_for_whitespace() =>
        ValueObjectGenerators.InvalidName.Sample(raw =>
            ValueObjectLawAssertions.FailsWith(raw, new SampleNameFailure.Blank(), SampleName.Create));

    /// <summary>正の整数による数量の構築成功</summary>
    [Test]
    public void A_sample_quantity_succeeds_with_its_positive_int() =>
        ValueObjectGenerators.ValidQuantity.Sample(raw =>
            ValueObjectLawAssertions.SucceedsWith(raw, raw, SampleQuantity.Create, value => value.Value));

    /// <summary>零または負の整数による数量の構築失敗</summary>
    [Test]
    public void A_sample_quantity_fails_with_the_rejected_non_positive_int() =>
        ValueObjectGenerators.InvalidQuantity.Sample(raw =>
            ValueObjectLawAssertions.FailsWith(raw, new SampleQuantityFailure.NotPositive(raw), SampleQuantity.Create));

    /// <summary>保持値から再構築した値オブジェクトの冪等性</summary>
    [Test]
    public void Rebuilding_from_the_underlying_value_is_idempotent()
    {
        ValueObjectGenerators.ValidIdentifier.Sample(raw =>
            ValueObjectLawAssertions.RebuildsIdempotently(raw, SampleIdentifier.Create, value => value.Value));
        ValueObjectGenerators.ValidName.Sample(raw =>
            ValueObjectLawAssertions.RebuildsIdempotently(raw, SampleName.Create, value => value.Value));
        ValueObjectGenerators.ValidQuantity.Sample(raw =>
            ValueObjectLawAssertions.RebuildsIdempotently(raw, SampleQuantity.Create, value => value.Value));
    }

    /// <summary>同じ値を持つ識別子の等価性</summary>
    [Test]
    public void Sample_identifiers_with_the_same_value_are_equal() =>
        ValueObjectGenerators.ValidIdentifier.Sample(raw =>
            ValueObjectLawAssertions.EqualCreations(raw, raw, SampleIdentifier.Create));

    /// <summary>同じ正規化済み値を持つ名前の等価性</summary>
    [Test]
    public void Sample_names_with_the_same_normalized_value_are_equal() =>
        ValueObjectGenerators.ValidName.Sample(raw =>
        {
            var normalized = raw.Trim();
            return ValueObjectLawAssertions.EqualCreations(normalized, $" {normalized} ", SampleName.Create);
        });

    /// <summary>同じ値を持つ数量の等価性</summary>
    [Test]
    public void Sample_quantities_with_the_same_value_are_equal() =>
        ValueObjectGenerators.ValidQuantity.Sample(raw =>
            ValueObjectLawAssertions.EqualCreations(raw, raw, SampleQuantity.Create));

    /// <summary>異なる値を持つ値オブジェクトの非等価性</summary>
    [Test]
    public void Value_objects_with_different_values_are_not_equal()
    {
        ValueObjectGenerators.DistinctIdentifiers.Sample(pair =>
            ValueObjectLawAssertions.UnequalCreations(pair.First, pair.Second, SampleIdentifier.Create));
        ValueObjectGenerators.ValidName.Sample(raw =>
        {
            var normalized = raw.Trim();
            return ValueObjectLawAssertions.UnequalCreations(normalized, normalized + "a", SampleName.Create);
        });
        ValueObjectGenerators.IncrementableQuantity.Sample(raw =>
            ValueObjectLawAssertions.UnequalCreations(raw, raw + 1, SampleQuantity.Create));
    }

    /// <summary>等しい値オブジェクトの hash code の整合</summary>
    [Test]
    public void Equal_value_objects_have_equal_hash_codes()
    {
        ValueObjectGenerators.ValidIdentifier.Sample(raw =>
            ValueObjectLawAssertions.EqualCreationsHaveEqualHashes(raw, SampleIdentifier.Create));
        ValueObjectGenerators.ValidName.Sample(raw =>
            ValueObjectLawAssertions.EqualCreationsHaveEqualHashes(raw, SampleName.Create));
        ValueObjectGenerators.ValidQuantity.Sample(raw =>
            ValueObjectLawAssertions.EqualCreationsHaveEqualHashes(raw, SampleQuantity.Create));
    }

    /// <summary>値オブジェクトと保持値の文字列表現の一致</summary>
    [Test]
    public void ToString_matches_the_underlying_values_string_representation()
    {
        ValueObjectGenerators.ValidIdentifier.Sample(raw =>
            ValueObjectLawAssertions.StringifiesAsUnderlying(raw, SampleIdentifier.Create, value => value.Value));
        ValueObjectGenerators.ValidName.Sample(raw =>
            ValueObjectLawAssertions.StringifiesAsUnderlying(raw, SampleName.Create, value => value.Value));
        ValueObjectGenerators.ValidQuantity.Sample(raw =>
            ValueObjectLawAssertions.StringifiesAsUnderlying(raw, SampleQuantity.Create, value => value.Value));
    }
}
