using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>union の Value への switch が、後から足す case 型を受け取る arm を持たないことを、診断の設定で求めること</summary>
public sealed class ExplicitCaseUnionTests
{
    /// <summary>case 型の基底と、二つの case 型を持つ union の宣言</summary>
    private const string Pets = """
        using System.Runtime.CompilerServices;

        namespace Probe;

        public abstract record Animal(string Name);
        public sealed record Cat(string Name) : Animal(Name);
        public sealed record Dog(string Name) : Animal(Name);

        [Union]
        public readonly partial struct Pet
        {
            public partial Pet(Cat value);
            public partial Pet(Dog value);
        }
        """;

    /// <summary>case 型を挙げた後の、他の全ての値を受け取る arm の拒否</summary>
    /// <param name="arm">case 型を挙げた後に置く arm</param>
    /// <param name="flagged">拒否される pattern の source</param>
    [Test]
    [Arguments("_ => 0", "_")]
    [Arguments("var other => 0", "var other")]
    [Arguments("object other => 0", "object other")]
    [Arguments("Animal other => 0", "Animal other")]
    public async Task Arms_that_accept_every_other_value_of_a_union_are_rejected(string arm, string flagged)
    {
        var diagnostics = await RunUnion(Pets + $$"""

            internal static class Use
            {
                internal static int Describe(Pet pet) => pet.Value switch
                {
                    Cat => 1,
                    Dog => 2,
                    {{arm}},
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo([flagged]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>case 型を一つずつ挙げる arm と、組の各要素の Value を全て挙げた arm の許可</summary>
    [Test]
    public async Task Case_types_are_accepted_alone_and_inside_a_tuple_of_values()
    {
        var diagnostics = await RunUnion(Pets + """

            internal static class Use
            {
                internal static int Single(Pet pet) => pet.Value switch
                {
                    Cat cat => cat.Name.Length,
                    Dog dog => dog.Name.Length,
                };

                internal static int Pair(Pet left, Pet right) => (left.Value, right.Value) switch
                {
                    (Cat, Cat) => 1,
                    (Cat, Dog) => 2,
                    (Dog, Cat or Dog) => 3,
                };

                internal static int Statement(Pet pet)
                {
                    var result = 0;
                    switch (pet.Value)
                    {
                        case Cat: result = 1; break;
                        case Dog: result = 2; break;
                    }

                    return result;
                }
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>組の要素の Value と switch 文の default を、case 型を挙げていなくても守ること</summary>
    [Test]
    public async Task Every_union_value_of_a_tuple_input_and_a_default_label_are_protected()
    {
        var diagnostics = await RunUnion(Pets + """

            internal static class Use
            {
                internal static int Pair(Pet left, Pet right) => (left.Value, right.Value) switch
                {
                    (Cat, _) => 1,
                    (Dog, Cat or Dog) => 2,
                };

                internal static int Statement(Pet pet)
                {
                    var result = 0;
                    switch (pet.Value)
                    {
                        default: result = 1; break;
                    }

                    return result;
                }
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["_", "default:"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>組の入力の全体に当てる discard と束縛が、組の要素の Value を隠すときの拒否</summary>
    [Test]
    public async Task A_broad_arm_over_a_tuple_with_a_union_value_is_rejected()
    {
        var diagnostics = await RunUnion(Pets + """

            internal static class Use
            {
                internal static int Discard(Pet pet, bool flag) => (pet.Value, flag) switch { _ => 0 };

                internal static int Bound(Pet pet, bool flag) => (pet.Value, flag) switch
                {
                    (Cat, _) => 1,
                    var rest => rest.Item2 ? 2 : 3,
                };
            }
            """);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["_", "var rest"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
