using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ClosedTypeDiagnosticFixture;
using static TypeModeling.Analyzers.Tests.ExhaustivenessAssertions;

namespace TypeModeling.Analyzers.Tests;

/// <summary>別の assembly の閉じた class の派生を、C# 15 と同じく見える派生だけで数えること</summary>
public sealed class ClosedHierarchyMetadataTests
{
    /// <summary>別の assembly で宣言した閉じた class の、同じ形の他の assembly 属性に惑わされない網羅</summary>
    [Test]
    public Task Closed_classes_from_referenced_assemblies_are_exhaustive() => AssertExhaustive("""
        using Library;

        internal static class Handler
        {
            internal static int Describe(Signal signal) => signal switch
            {
                Signal.Red => 1,
                Signal.Green => 2,
            };
        }
        """, Library("""
        using TypeModeling.Domain;

        [assembly: Library.Decoy(typeof(Library.Signal), 7)]

        namespace Library;

        [System.AttributeUsage(System.AttributeTargets.Assembly)]
        public sealed class DecoyAttribute(System.Type type, int count) : System.Attribute
        {
            public System.Type Type { get; } = type;
            public int Count { get; } = count;
        }

        [Closed]
        public abstract record Signal
        {
            private Signal() { }
            public sealed record Red : Signal;
            public sealed record Green : Signal;
        }
        """));

    /// <summary>別の assembly の非公開の派生と、派生の数の記録のない別の assembly の閉じた class の網羅の不成立</summary>
    [Test]
    public async Task Closed_classes_with_hidden_or_unrecorded_subtypes_are_not_exhaustive()
    {
        const string consumer = """
            using Library;

            internal static class Handler
            {
                internal static int Describe(Signal signal) => signal switch
                {
                    Signal.Red => 1,
                    Signal.Green => 2,
                };
            }
            """;
        const string signal = """
            using TypeModeling.Domain;

            namespace Library;

            [Closed]
            public abstract record Signal
            {
                private Signal() { }
                public sealed record Red : Signal;
                public sealed record Green : Signal;
                internal sealed record Amber : Signal;
            }
            """;

        await AssertNotExhaustive(consumer, Library(signal));
        await AssertNotExhaustive(consumer, Library(signal.Replace("internal sealed record Amber : Signal;", ""), recordSubtypeCounts: false));
    }
}
