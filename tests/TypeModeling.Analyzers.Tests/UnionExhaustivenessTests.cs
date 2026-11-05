using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.UnionFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>C# 14 の union 宣言の形を、C# 15 の union と同じく使えること</summary>
public sealed class UnionExhaustivenessTests
{
    /// <summary>C# 15 の <c>union Pet(Cat, Dog);</c> に当たる宣言</summary>
    private const string Pet = """
        using System.Runtime.CompilerServices;

        namespace Probe;

        public sealed record Cat(string Name);
        public sealed record Dog(string Name);

        [Union]
        public readonly partial struct Pet
        {
            public partial Pet(Cat value);
            public partial Pet(Dog value);
        }
        """;

    /// <summary>case 型からの変換と Value を持つ union の生成</summary>
    [Test]
    public async Task The_declaration_compiles_with_conversions_from_every_case_type()
    {
        var diagnostics = await Run(Pet + """
            internal static class Use
            {
                internal static Pet FromCat() => new Cat("Tama");
                internal static object FromDog() => ((Pet)new Dog("Pochi")).Value;
                internal static IUnion AsUnion(Pet pet) => pet;
            }
            """);

        await Assert.That(Errors(diagnostics)).IsEmpty();
    }

    /// <summary>二つの union の Value の組の全ての組合せを扱う switch 式の網羅</summary>
    [Test]
    public async Task Tuples_of_union_values_are_exhaustive_when_every_combination_is_covered()
    {
        var diagnostics = await Run(Pet + """
            internal static class Use
            {
                internal static int Pair(Pet left, Pet right) => (left.Value, right.Value) switch
                {
                    (Cat, _) => 1,
                    (Dog, Cat) => 2,
                    (Dog, Dog) => 3,
                };
            }
            """);

        await Assert.That(Errors(diagnostics)).IsEmpty();
        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Id == "CS8509" && !diagnostic.IsSuppressed)).IsEmpty();
        await Assert.That(diagnostics.Any(diagnostic => diagnostic.Id == "CS8509" && diagnostic.IsSuppressed)).IsTrue();
    }

    /// <summary>Value への switch 式の網羅と、case 型を欠く switch 式の CS8509 の維持</summary>
    [Test]
    public async Task Switching_on_value_is_exhaustive_only_when_every_case_type_is_handled()
    {
        var exhaustive = await Run(Pet + """
            internal static class Use
            {
                internal static string Name(Pet pet) => pet.Value switch { Cat cat => cat.Name, Dog dog => dog.Name };
            }
            """);
        var missing = await Run(Pet + """
            internal static class Use
            {
                internal static string Name(Pet pet) => pet.Value switch { Cat cat => cat.Name };
            }
            """);

        await Assert.That(Errors(exhaustive)).IsEmpty();
        await Assert.That(exhaustive.Where(diagnostic => diagnostic.Id is "CS8509" or "CS8655" && !diagnostic.IsSuppressed)).IsEmpty();
        await Assert.That(missing.Any(diagnostic => diagnostic.Id == "CS8509" && !diagnostic.IsSuppressed)).IsTrue();
    }

    /// <summary>generic な union を generic method の中で網羅する switch 式</summary>
    [Test]
    public async Task Generic_unions_are_exhaustive_inside_generic_methods()
    {
        var diagnostics = await Run("""
            using System.Runtime.CompilerServices;

            public sealed record Left<T>(T Value);
            public sealed record Right<T>(T Value);

            [Union]
            public readonly partial struct Either<TLeft, TRight>
            {
                public partial Either(Left<TLeft> value);
                public partial Either(Right<TRight> value);
            }

            internal static class Use
            {
                internal static string Show<TLeft, TRight>(Either<TLeft, TRight> either) => either.Value switch
                {
                    Left<TLeft>(var left) => $"{left}",
                    Right<TRight>(var right) => $"{right}",
                };
            }
            """);

        await Assert.That(Errors(diagnostics)).IsEmpty();
        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Id == "CS8509" && !diagnostic.IsSuppressed)).IsEmpty();
    }

    /// <summary>C# 15 の言語では生成した union が言語の union になり、暗黙の変換を生成しないこと</summary>
    [Test]
    public async Task Under_csharp_15_the_generated_type_is_a_language_union()
    {
        var source = Pet + """
            internal static class Use
            {
                internal static Pet FromCat() => new Cat("Tama");
                internal static string Name(Pet pet) => pet switch { Cat cat => cat.Name, Dog dog => dog.Name };
            }
            """;

        var diagnostics = await Run(source, Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview);

        await Assert.That(Errors(diagnostics)).IsEmpty();
        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Id == "CS8509")).IsEmpty();
        await Assert.That(GeneratedSource(source, Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview)).DoesNotContain("implicit operator");
    }
}
