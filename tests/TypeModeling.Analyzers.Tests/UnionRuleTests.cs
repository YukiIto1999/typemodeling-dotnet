using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.UnionFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>union 型と union 宣言の形の C# 15 と同じ規則</summary>
public sealed class UnionRuleTests
{
    /// <summary>case 型の宣言</summary>
    private const string Cases = """
        using System.Runtime.CompilerServices;

        public sealed record Cat(string Name);
        public sealed record Dog(string Name);
        public interface IAnimal;
        """;

    /// <summary>union 宣言の形の制約の違反ごとの診断</summary>
    /// <param name="id">期待する診断の ID</param>
    /// <param name="members">union 宣言の形に置く member</param>
    [Test]
    [Arguments("TYPMOD008", "private readonly int _count;")]
    [Arguments("TYPMOD008", "public int Count { get; }")]
    [Arguments("TYPMOD009", "public Pet(string name) : this(new Cat(name)) { }")]
    [Arguments("TYPMOD010", "internal Pet(int count, string name) { }")]
    public async Task Union_declarations_reject_members_that_csharp_15_rejects(string id, string members)
    {
        var diagnostics = await Run(Cases + $$"""
            [Union]
            public readonly partial struct Pet
            {
                public partial Pet(Cat value);
                public partial Pet(Dog value);
                {{members}}
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).Contains(id);
    }

    /// <summary>this 初期化子で union 構築子を呼ぶコンストラクタの許可</summary>
    [Test]
    public async Task Constructors_that_chain_to_a_case_constructor_are_allowed()
    {
        var diagnostics = await Run(Cases + """
            [Union]
            public readonly partial struct Pet
            {
                public partial Pet(Cat value);
                public partial Pet(Dog value);
                internal Pet(string catName, int unused) : this(new Cat(catName)) { }
            }
            """);

        await Assert.That(Errors(diagnostics)).IsEmpty();
    }

    /// <summary>class の union 宣言の形と、partial でない型への入れ子の診断</summary>
    [Test]
    public async Task Union_declarations_must_be_partial_structs_inside_partial_types()
    {
        var diagnostics = await Run(Cases + """
            [Union]
            public partial class ClassPet
            {
                public partial ClassPet(Cat value);
            }

            public static class Outer
            {
                [Union]
                public readonly partial struct Nested
                {
                    public partial Nested(Cat value);
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).Contains("TYPMOD007");
        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).Contains("TYPMOD014");
    }

    /// <summary>手書きの union 型の union 構築子と Value の欠落の診断</summary>
    [Test]
    public async Task Hand_written_unions_need_creation_members_and_a_value_property()
    {
        var diagnostics = await Run(Cases + """
            [Union]
            public readonly struct NoCases
            {
                public object? Value => null;
            }

            [Union]
            public readonly struct NoValue
            {
                public NoValue(Cat value) { }
            }

            [Union]
            public readonly struct Valid
            {
                public Valid(Cat value) => Value = value;
                public Valid(Dog value) => Value = value;
                public object Value { get; }
            }

            internal static class Use
            {
                internal static string Name(Valid pet) => pet.Value switch { Cat cat => cat.Name, Dog dog => dog.Name };
            }
            """);

        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Id.StartsWith("TYPMOD", StringComparison.Ordinal)).Select(diagnostic => diagnostic.Id))
            .IsEquivalentTo(["TYPMOD012", "TYPMOD013"]);
        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Id == "CS8509" && !diagnostic.IsSuppressed)).IsEmpty();
    }

    /// <summary>interface の case 型に暗黙の変換を生成しないこと</summary>
    [Test]
    public async Task Interface_case_types_get_no_implicit_conversion()
    {
        const string source = Cases + """
            [Union]
            public readonly partial struct Pet
            {
                public partial Pet(Cat value);
                public partial Pet(IAnimal value);
            }
            """;

        await Assert.That(Errors(await Run(source))).IsEmpty();
        await Assert.That(GeneratedSource(source)).Contains("implicit operator Pet(global::Cat value)");
        await Assert.That(GeneratedSource(source)).DoesNotContain("implicit operator Pet(global::IAnimal");
    }
}
