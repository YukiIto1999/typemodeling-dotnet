using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExhaustivenessAssertions;
using static TypeModeling.Analyzers.Tests.UnionFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>型と member の名前の書き方を問わない網羅の判定</summary>
public sealed class TypeNameShapeTests
{
    /// <summary>keyword と同じ名前の派生と enum の member を持つ閉じた型の網羅</summary>
    [Test]
    public Task Closed_types_with_keyword_names_are_exhaustive() => AssertExhaustive("""
        using TypeModeling.Domain;

        namespace Probe;

        [Closed]
        public abstract record Signal
        {
            private Signal() { }
            public sealed record @event : Signal;
            public sealed record @class : Signal;
        }

        [Closed]
        public enum Mode
        {
            @default,
            @checked,
        }

        internal static class Handler
        {
            internal static int Describe(Signal signal, Mode mode) => (signal, mode) switch
            {
                (Signal.@event, Mode.@default) => 1,
                (Signal.@event, Mode.@checked) => 2,
                (Signal.@class, _) => 3,
            };
        }
        """);

    /// <summary>派生を持たない閉じた派生を、型 pattern で扱う switch 式の網羅</summary>
    [Test]
    public Task A_closed_subtype_without_subtypes_is_covered_by_its_type_pattern() => AssertExhaustive("""
        using TypeModeling.Domain;

        namespace Probe;

        [Closed]
        public abstract record Shape
        {
            private Shape() { }
            public sealed record Circle : Shape;

            [Closed]
            public abstract record Empty : Shape
            {
                private Empty() { }
            }
        }

        internal static class Handler
        {
            internal static int Describe(Shape shape) => shape switch
            {
                Shape.Circle => 1,
                Shape.Empty => 2,
            };
        }
        """);

    /// <summary>System を using しない位置での、組み込み型を case 型に持つ union の網羅</summary>
    [Test]
    public async Task Unions_of_built_in_types_are_exhaustive_without_using_system()
    {
        var diagnostics = await Run("""
            namespace Probe;

            [System.Runtime.CompilerServices.Union]
            public readonly partial struct Scalar
            {
                public partial Scalar(int value);
                public partial Scalar(string value);
            }

            internal static class Use
            {
                internal static int Describe(Scalar scalar) => scalar.Value switch
                {
                    int number => number,
                    string text => text.Length,
                };
            }
            """);

        await Assert.That(Errors(diagnostics)).IsEmpty();
        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Id == "CS8509" && !diagnostic.IsSuppressed)).IsEmpty();
    }
}
