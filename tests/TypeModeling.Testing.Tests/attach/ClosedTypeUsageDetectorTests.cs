using TUnit.Core;
using TypeModeling.Testing.Attach;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>closed type の semantic 利用検出</summary>
public sealed class ClosedTypeUsageDetectorTests
{
    /// <summary>ClosedUnion 基底型と variant の利用検出</summary>
    [Test]
    public async Task Closed_union_base_and_variant_count_as_closed_type_uses()
    {
        var baseCompilation = ClosedTypeCompilationFixture.Create("""
            using Probe.Types;

            internal static class BaseConsumer
            {
                internal static ClosedBase Preserve(ClosedBase value) => value;
            }
            """);
        var variantCompilation = ClosedTypeCompilationFixture.Create("""
            using Probe.Types;

            internal static class VariantConsumer
            {
                internal static ClosedVariant Preserve(ClosedVariant value) => value;
            }
            """);
        var detector = new ClosedTypeUsageDetector();

        await Assert.That(typeof(GuardedTypeUsageWalk).IsPublic).IsTrue();
        await Assert.That(detector.UsesGuardedFeature(baseCompilation)).IsTrue();
        await Assert.That(detector.UsesGuardedFeature(variantCompilation)).IsTrue();
    }

    /// <summary>JSON polymorphic 基底型と登録済み派生型の利用検出</summary>
    [Test]
    public async Task Json_polymorphic_base_and_derived_type_count_as_closed_type_uses()
    {
        var baseCompilation = ClosedTypeCompilationFixture.Create("""
            using Probe.Types;

            internal static class BaseConsumer
            {
                internal static JsonBase Preserve(JsonBase value) => value;
            }
            """);
        var variantCompilation = ClosedTypeCompilationFixture.Create("""
            using Probe.Types;

            internal static class VariantConsumer
            {
                internal static JsonVariant Preserve(JsonVariant value) => value;
            }
            """);
        var detector = new ClosedTypeUsageDetector();

        await Assert.That(detector.UsesGuardedFeature(baseCompilation)).IsTrue();
        await Assert.That(detector.UsesGuardedFeature(variantCompilation)).IsTrue();
    }

    /// <summary>open polymorphic 型と同名属性型の除外</summary>
    [Test]
    public async Task Open_polymorphic_and_same_named_attribute_types_do_not_count_as_closed_type_uses()
    {
        var compilation = ClosedTypeCompilationFixture.Create("""
            using Probe.Types;

            internal static class OpenConsumer
            {
                internal static OpenJsonVariant PreserveOpen(OpenJsonVariant value) => value;

                internal static SameNamedClosedBase PreserveSameNamed(SameNamedClosedBase value) => value;
            }
            """);
        var detector = new ClosedTypeUsageDetector();

        await Assert.That(detector.UsesGuardedFeature(compilation)).IsFalse();
    }
}
