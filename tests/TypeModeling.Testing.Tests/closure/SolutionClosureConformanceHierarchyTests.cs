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

    /// <summary>同じ assembly の直接の派生は形を問わない規約成立</summary>
    [Test]
    public async Task Same_assembly_subtypes_of_a_closed_class_have_no_violations_whatever_their_shape()
    {
        const string assemblyName = "ClosedSameAssemblyFixture";
        var assembly = DynamicFixtureCompiler.Compile(
            """
            using TypeModeling.Domain;

            namespace DynamicFixtures;

            [Closed]
            public abstract record GateState
            {
                public sealed record Nested : GateState;
            }

            public record TopLevel : GateState;

            public sealed record Deeper : TopLevel;

            [Closed]
            public abstract class Generic<TValue>;

            public sealed class Inside<TValue> : Generic<TValue>;
            """,
            assemblyName);

        var violations = SolutionClosureConformance.ClosedHierarchyViolations(Closure(assembly));

        await Assert.That(violations).IsEmpty();
    }

    /// <summary>別の assembly の直接の派生の違反報告</summary>
    [Test]
    public async Task A_direct_subtype_in_another_assembly_is_reported()
    {
        const string baseName = "ClosedBaseFixture";
        const string subtypeName = "ClosedForeignSubtypeFixture";
        var baseAssembly = DynamicFixtureCompiler.Compile(
            """
            using TypeModeling.Domain;

            namespace DynamicFixtures;

            [Closed]
            public abstract record Shared;

            [Closed]
            public abstract record Generic<TValue>;

            public record Open : Shared;
            """,
            baseName);
        var subtypeAssembly = DynamicFixtureCompiler.Compile(
            """
            namespace DynamicFixtures.Foreign;

            public sealed record Direct : DynamicFixtures.Shared;

            public sealed record GenericDirect<TValue> : DynamicFixtures.Generic<TValue>;

            public sealed record Indirect : DynamicFixtures.Open;
            """,
            subtypeName,
            baseAssembly);

        var violations = SolutionClosureConformance.ClosedHierarchyViolations(Closure(baseAssembly, subtypeAssembly));

        await Assert.That(violations).IsEquivalentTo([
            $"base DynamicFixtures.Generic`1 (assembly {baseName}, source project fixtures/{baseName}.csproj) " +
            $"<- subtype DynamicFixtures.Foreign.GenericDirect`1 (assembly {subtypeName}, source project fixtures/{subtypeName}.csproj) " +
            "(direct subtype outside the assembly of the closed type)",
            $"base DynamicFixtures.Shared (assembly {baseName}, source project fixtures/{baseName}.csproj) " +
            $"<- subtype DynamicFixtures.Foreign.Direct (assembly {subtypeName}, source project fixtures/{subtypeName}.csproj) " +
            "(direct subtype outside the assembly of the closed type)",
        ]);
    }

    private static LoadedSolutionClosure Closure(params Assembly[] assemblies)
    {
        var projects = assemblies.Select(assembly =>
        {
            var assemblyName = assembly.GetName().Name
                ?? throw new InvalidOperationException("fixture assembly has no name");
            return new LoadedSolutionProject(
                $"fixtures/{assemblyName}.csproj",
                assemblyName,
                assembly.Location,
                assembly.ManifestModule.ModuleVersionId,
                assembly);
        });
        return new LoadedSolutionClosure([], [.. projects], [.. assemblies.SelectMany(assembly => assembly.GetTypes())]);
    }
}
