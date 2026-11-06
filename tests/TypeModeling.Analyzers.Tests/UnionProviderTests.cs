using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.UnionFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>手書きの union 型の union 構築子と Value を、C# 15 と同じ規則で探すこと</summary>
public sealed class UnionProviderTests
{
    /// <summary>case 型の宣言</summary>
    private const string Cases = """
        using System.Runtime.CompilerServices;

        public sealed record Cat(string Name);
        public sealed record Dog(string Name);
        public sealed record Bird(string Name);
        """;

    /// <summary>union member provider の static な Create だけを union 構築子とする網羅</summary>
    [Test]
    public async Task Member_providers_supply_the_case_types_and_the_value()
    {
        var diagnostics = await Run(Cases + """
            [Union]
            public readonly struct Provided : Provided.IUnionMembers
            {
                private readonly object _value;

                private Provided(object value) => _value = value;

                object Provided.IUnionMembers.Value => _value;

                public interface IUnionMembers
                {
                    static Provided Create(Cat value) => new(value);
                    static Provided Create(Dog value) => new(value);
                    static object Create(Bird value) => value;
                    Provided Create(string name) => default;
                    object Value { get; }
                }
            }

            internal static class Use
            {
                internal static string Name(Provided pet) => ((Provided.IUnionMembers)pet).Value switch
                {
                    Cat cat => cat.Name,
                    Dog dog => dog.Name,
                };
            }
            """);

        await Assert.That(Errors(diagnostics)).IsEmpty();
        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Id == "CS8509" && !diagnostic.IsSuppressed)).IsEmpty();
    }

    /// <summary>実装しない IUnionMembers を union member provider とみなさないこと</summary>
    [Test]
    public async Task Nested_interfaces_the_union_does_not_implement_are_not_providers()
    {
        var diagnostics = await Run(Cases + """
            [Union]
            public readonly struct Pet
            {
                public Pet(Cat value) => Value = value;
                public Pet(Dog value) => Value = value;
                public object Value { get; }

                public interface IUnionMembers
                {
                    static Pet Create(Bird value) => default;
                }
            }

            internal static class Use
            {
                internal static string Name(Pet pet) => pet.Value switch
                {
                    Cat cat => cat.Name,
                    Dog dog => dog.Name,
                };
            }
            """);

        await Assert.That(Errors(diagnostics)).IsEmpty();
        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Id == "CS8509" && !diagnostic.IsSuppressed)).IsEmpty();
    }

    /// <summary>Union 属性のない型の Value を union として扱わないこと</summary>
    [Test]
    public async Task Types_without_the_union_attribute_are_not_unions()
    {
        var diagnostics = await Run(Cases + """
            public readonly struct Holder : Holder.IUnionMembers
            {
                object Holder.IUnionMembers.Value => null!;

                public interface IUnionMembers
                {
                    static Holder Create(Cat value) => default;
                    object Value { get; }
                }
            }

            internal static class Use
            {
                internal static string Name(Holder holder) => ((Holder.IUnionMembers)holder).Value switch
                {
                    Cat cat => cat.Name,
                };
            }
            """);

        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Id == "CS8509" && !diagnostic.IsSuppressed)).IsNotEmpty();
    }

    /// <summary>object 型で公開の get を持つ instance の Value だけを union の Value とすること</summary>
    /// <param name="value">手書きの union 型に置く Value</param>
    [Test]
    [Arguments("public string Value => string.Empty;")]
    [Arguments("internal object Value => string.Empty;")]
    [Arguments("public static object Value => string.Empty;")]
    public async Task Value_must_be_a_public_instance_object_property(string value)
    {
        var diagnostics = await Run(Cases + $$"""
            [Union]
            public readonly struct Pet
            {
                public Pet(Cat value) { }
                {{value}}
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).Contains("TYPMOD013");
    }

    /// <summary>名前を解決できない case 型を持つ union の、網羅とみなさない判定と、同じ file の他の switch 式の抑止</summary>
    [Test]
    public async Task Unions_with_unresolved_case_types_are_not_exhaustive()
    {
        var diagnostics = await Run(Cases + """
            [Union]
            public readonly partial struct Pet
            {
                public partial Pet(Cat value);
                public partial Pet(Missing value);
            }

            [Union]
            public readonly partial struct Animal
            {
                public partial Animal(Cat value);
                public partial Animal(Dog value);
            }

            internal static class Use
            {
                internal static string Name(Pet pet) => pet.Value switch
                {
                    Cat cat => cat.Name,
                };

                internal static string Kind(Animal animal) => animal.Value switch
                {
                    Cat cat => cat.Name,
                    Dog dog => dog.Name,
                };
            }
            """);
        var reported = diagnostics.Where(diagnostic => diagnostic.Id == "CS8509").ToArray();

        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Id == "CS0246")).IsNotEmpty();
        await Assert.That(reported.Count(diagnostic => !diagnostic.IsSuppressed)).IsEqualTo(1);
        await Assert.That(reported.Count(diagnostic => diagnostic.IsSuppressed)).IsEqualTo(1);
    }
}
