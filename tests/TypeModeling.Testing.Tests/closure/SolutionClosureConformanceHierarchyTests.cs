using System.Reflection;
using TUnit.Core;
using TypeModeling.Testing.Closure;

namespace TypeModeling.Testing.Tests.Closure;

/// <summary>ロード済み CLR 型に対する閉じた階層規約の検証</summary>
public sealed class SolutionClosureConformanceHierarchyTests
{
    /// <summary>未登録の JSON polymorphic 派生型の違反報告</summary>
    [Test]
    public async Task An_unregistered_json_polymorphic_subtype_is_reported()
    {
        const string assemblyName = "JsonHierarchyViolationFixture";
        var assembly = DynamicFixtureCompiler.Compile(
            """
            using System.Text.Json.Serialization;

            namespace DynamicFixtures;

            [JsonPolymorphic]
            [JsonDerivedType(typeof(RegisteredJsonVariant), "registered")]
            public interface JsonBase;

            public sealed class RegisteredJsonVariant : JsonBase;

            public readonly struct MissingJsonVariant : JsonBase;
            """,
            assemblyName);

        var violations = SolutionClosureConformance.ClosedHierarchyViolations(Closure(assembly));

        await Assert.That(violations).IsEquivalentTo([
            $"base DynamicFixtures.JsonBase (assembly {assemblyName}, source project fixtures/{assemblyName}.csproj) " +
            $"<- unregistered subtype DynamicFixtures.MissingJsonVariant (assembly {assemblyName}, " +
            $"source project fixtures/{assemblyName}.csproj)",
        ]);
    }

    /// <summary>全派生型を登録した JSON polymorphic 階層の規約成立</summary>
    [Test]
    public async Task Fully_registered_json_polymorphic_subtypes_have_no_violations()
    {
        const string assemblyName = "JsonHierarchyConformingFixture";
        var assembly = DynamicFixtureCompiler.Compile(
            """
            using System.Text.Json.Serialization;

            namespace DynamicFixtures;

            [JsonPolymorphic]
            [JsonDerivedType(typeof(JsonVariant), "variant")]
            public abstract class JsonBase;

            public sealed class JsonVariant : JsonBase;
            """,
            assemblyName);

        var violations = SolutionClosureConformance.ClosedHierarchyViolations(Closure(assembly));

        await Assert.That(violations).IsEmpty();
    }

    /// <summary>規定形状から外れた ClosedUnion 派生型の違反報告</summary>
    [Test]
    public async Task Closed_union_subtypes_outside_the_required_shape_are_reported()
    {
        const string assemblyName = "ClosedUnionViolationFixture";
        var assembly = DynamicFixtureCompiler.Compile(
            """
            using TypeModeling.Domain;

            namespace DynamicFixtures;

            [ClosedUnion]
            public abstract record BadRecordUnion
            {
                protected BadRecordUnion() { }

                public record Open : BadRecordUnion;
            }

            public sealed record External : BadRecordUnion;

            [ClosedUnion]
            public abstract class BadClassUnion
            {
                protected BadClassUnion() { }

                public sealed class Nested : BadClassUnion;
            }
            """,
            assemblyName);
        var projectPath = $"fixtures/{assemblyName}.csproj";

        var violations = SolutionClosureConformance.ClosedHierarchyViolations(Closure(assembly));

        await Assert.That(violations).IsEquivalentTo([
            $"base DynamicFixtures.BadClassUnion (assembly {assemblyName}, source project {projectPath}) " +
            $"<- subtype DynamicFixtures.BadClassUnion+Nested (assembly {assemblyName}, source project {projectPath}) " +
            "(directly-nested=True, sealed=True, record=False)",
            $"base DynamicFixtures.BadRecordUnion (assembly {assemblyName}, source project {projectPath}) " +
            $"<- subtype DynamicFixtures.BadRecordUnion+Open (assembly {assemblyName}, source project {projectPath}) " +
            "(directly-nested=True, sealed=False, record=True)",
            $"base DynamicFixtures.BadRecordUnion (assembly {assemblyName}, source project {projectPath}) " +
            $"<- subtype DynamicFixtures.External (assembly {assemblyName}, source project {projectPath}) " +
            "(directly-nested=False, sealed=True, record=True)",
        ]);
    }

    /// <summary>generic な ClosedUnion 派生型への規約適用</summary>
    [Test]
    public async Task Generic_closed_union_subtypes_are_checked_against_the_shape()
    {
        const string assemblyName = "GenericClosedUnionFixture";
        var assembly = DynamicFixtureCompiler.Compile(
            """
            using TypeModeling.Domain;

            namespace DynamicFixtures;

            [ClosedUnion]
            public abstract record GenericUnion<TValue>
            {
                protected GenericUnion() { }

                public sealed record Inside : GenericUnion<TValue>;
            }

            public sealed record Outside<TValue> : GenericUnion<TValue>;
            """,
            assemblyName);
        var projectPath = $"fixtures/{assemblyName}.csproj";

        var violations = SolutionClosureConformance.ClosedHierarchyViolations(Closure(assembly));

        await Assert.That(violations).IsEquivalentTo([
            $"base DynamicFixtures.GenericUnion`1 (assembly {assemblyName}, source project {projectPath}) " +
            $"<- subtype DynamicFixtures.Outside`1 (assembly {assemblyName}, source project {projectPath}) " +
            "(directly-nested=False, sealed=True, record=True)",
        ]);
    }

    /// <summary>基底へ直接ネストした sealed record 派生型の規約成立</summary>
    [Test]
    public async Task Directly_nested_sealed_record_union_subtypes_have_no_violations()
    {
        const string assemblyName = "ClosedUnionConformingFixture";
        var assembly = DynamicFixtureCompiler.Compile(
            """
            using TypeModeling.Domain;

            namespace DynamicFixtures;

            [ClosedUnion]
            public abstract record GoodUnion
            {
                private GoodUnion() { }

                internal sealed record Variant : GoodUnion;
            }
            """,
            assemblyName);

        var violations = SolutionClosureConformance.ClosedHierarchyViolations(Closure(assembly));

        await Assert.That(violations).IsEmpty();
    }

    private static LoadedSolutionClosure Closure(Assembly assembly)
    {
        var assemblyName = assembly.GetName().Name
            ?? throw new InvalidOperationException("fixture assembly has no name");
        var project = new LoadedSolutionProject(
            $"fixtures/{assemblyName}.csproj",
            assemblyName,
            assembly.Location,
            assembly.ManifestModule.ModuleVersionId,
            assembly);
        return new LoadedSolutionClosure([], [project], assembly.GetTypes());
    }
}
