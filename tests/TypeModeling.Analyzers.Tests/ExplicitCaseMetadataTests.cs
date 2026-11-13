using Microsoft.CodeAnalysis;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using TypeModeling.Domain;
using static TypeModeling.Analyzers.Tests.ClosedTypeDiagnosticFixture;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>別の assembly の型の Value と派生を、同じ assembly の型と同じく守ること</summary>
public sealed class ExplicitCaseMetadataTests
{
    /// <summary>TypeModeling の assembly の参照</summary>
    /// <remarks>この test project が union 型や閉じた型を使う project にならないよう、assembly は閉じていない Unit から得る</remarks>
    private static readonly MetadataReference Runtime =
        MetadataReference.CreateFromFile(typeof(Unit).Assembly.Location);

    /// <summary>別の assembly で宣言した、手書きの union と閉じた class の宣言</summary>
    private const string LibrarySource = """
        namespace System.Runtime.CompilerServices
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
            public sealed class UnionAttribute : System.Attribute;
        }

        namespace Library
        {
            public sealed record Cat(string Name);
            public sealed record Dog(string Name);

            [System.Runtime.CompilerServices.Union]
            public readonly struct Pet
            {
                public Pet(Cat value) => Value = value;
                public Pet(Dog value) => Value = value;
                public object Value { get; }
            }

            [TypeModeling.Domain.Closed]
            public abstract record Signal
            {
                private Signal() { }
                public sealed record Red : Signal;
                public sealed record Green : Signal;
            }
        }
        """;

    /// <summary>TypeModeling の Result の Value を、case を挙げれば許し、catch-all なら拒否すること</summary>
    [Test]
    public async Task The_value_of_a_result_from_the_runtime_assembly_is_protected()
    {
        var diagnostics = await RunClosed("""
            using TypeModeling.Domain;

            namespace Probe;

            internal static class Handler
            {
                internal static int Explicit(Result<int, string> result) => result.Value switch
                {
                    Succeeded<int>(var value) => value,
                    Failed<string>(var failure) => failure.Length,
                    null => -1,
                };

                internal static int Fallback(Result<int, string> result) => result.Value switch
                {
                    Succeeded<int>(var value) => value,
                    _ => 0,
                };
            }
            """, true, Runtime);

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["_"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>別の assembly の union の Value と閉じた class の catch-all の拒否と、挙げた case の許可</summary>
    [Test]
    public async Task Types_from_a_referenced_assembly_are_protected()
    {
        var diagnostics = await RunClosed("""
            using Library;

            internal static class Handler
            {
                internal static int Union(Pet pet) => pet.Value switch
                {
                    Cat => 1,
                    Dog => 2,
                    _ => 0,
                };

                internal static int Closed(Signal signal) => signal switch
                {
                    Signal.Red => 1,
                    _ => 0,
                };

                internal static int Complete(Pet pet, Signal signal) => (pet.Value, signal) switch
                {
                    (Cat, Signal.Red or Signal.Green) => 1,
                    (Dog, Signal.Red or Signal.Green) => 2,
                };
            }
            """, true, Library(LibrarySource));

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo(["_", "_"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }
}
