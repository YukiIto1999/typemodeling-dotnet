using System.Text.Json;
using TypeModeling.Domain;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace TypeModeling.Tests;

/// <summary>JSON serializer 設定の同一 instance を読取専用化する freeze factory の実証</summary>
public sealed class FrozenJsonOptionsTests
{
    /// <summary>凍結済み options の同一性と変更拒否</summary>
    [Test]
    public async Task Frozen_returns_the_same_read_only_options_and_rejects_later_mutation()
    {
        var options = new JsonSerializerOptions();

        var frozen = FrozenJsonOptions.Frozen(options);

        await Assert.That(ReferenceEquals(frozen, options)).IsTrue();
        await Assert.That(frozen.IsReadOnly).IsTrue();
        await Assert.That(() => frozen.WriteIndented = true)
            .ThrowsExactly<InvalidOperationException>();
    }
}
