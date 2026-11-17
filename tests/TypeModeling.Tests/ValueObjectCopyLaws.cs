using CsCheck;
using TUnit.Core;
using TypeModeling.Tests.Samples;
using TypeModeling.Tests.Support;

namespace TypeModeling.Tests;

/// <summary>生成された値オブジェクトの with による複製が検証済みの値を保つことの property 検証</summary>
public sealed class ValueObjectCopyLaws
{
    /// <summary>識別子の複製が別の instance で同じ Guid を保つこと</summary>
    [Test]
    public void A_with_copy_of_an_identifier_keeps_its_guid() =>
        ValueObjectGenerators.ValidIdentifier.Sample(raw =>
            ValueObjectLawAssertions.CopiesKeepTheValidatedValue(
                raw,
                raw,
                SampleIdentifier.Create,
                value => value.Value,
                value => value with { }
            )
        );

    /// <summary>名前の複製が別の instance で正規化済みの文字列を保つこと</summary>
    [Test]
    public void A_with_copy_of_a_name_keeps_its_normalized_text() =>
        ValueObjectGenerators.ValidName.Sample(raw =>
            ValueObjectLawAssertions.CopiesKeepTheValidatedValue(
                raw,
                raw.Trim(),
                SampleName.Create,
                value => value.Value,
                value => value with { }
            )
        );

    /// <summary>数量の複製が別の instance で同じ整数を保つこと</summary>
    [Test]
    public void A_with_copy_of_a_quantity_keeps_its_int() =>
        ValueObjectGenerators.ValidQuantity.Sample(raw =>
            ValueObjectLawAssertions.CopiesKeepTheValidatedValue(
                raw,
                raw,
                SampleQuantity.Create,
                value => value.Value,
                value => value with { }
            )
        );
}
