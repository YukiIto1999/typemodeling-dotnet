using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using TypeModeling.Analyzers.Diagnostics.MustUse;
using TypeModeling.Analyzers.Diagnostics.ResultUsage;

namespace TypeModeling.Analyzers.Tests;

/// <summary>Result の未初期化の構築、case 型への cast、使わずに捨てた値の検出</summary>
public sealed class ResultUsageTests
{
    // analyzer は metadata 名で判定するため、実 assembly を参照しない同名の型の合成
    private const string Domain = """
        namespace TypeModeling.Domain
        {
            [System.AttributeUsage(System.AttributeTargets.All)]
            public sealed class MustUseAttribute : System.Attribute;

            public readonly record struct Succeeded<TValue>(TValue Value);

            [MustUse]
            public readonly struct Result<TValue, TFailure>
            {
                public Result(Succeeded<TValue> value) => Value = value;
                public object? Value { get; }
            }
        }

        namespace Probe
        {
            using TypeModeling.Domain;

            public static class Source
            {
                public static Result<int, string> Parse(string text) => new(new Succeeded<int>(text.Length));
                [MustUse]
                public static int Count(string text) => text.Length;
                public static int Ignore(string text) => text.Length;
                [System.ComponentModel.Description("length")]
                public static int Described(string text) => text.Length;
            }
        }
        """;

    /// <summary>default と引数なしの構築による Result の TYPMOD015</summary>
    [Test]
    public async Task Uninitialized_results_are_rejected()
    {
        var ids = await Ids("""
            using TypeModeling.Domain;

            internal static class Use
            {
                internal static Result<int, string> Explicit() => default(Result<int, string>);
                internal static Result<int, string> Literal() => default;
                internal static Result<int, string> Created() => new Result<int, string>();
                internal static Result<int, string> Valid() => new(new Succeeded<int>(1));
            }
            """);

        await Assert.That(ids.Count(id => id == "TYPMOD015")).IsEqualTo(3);
    }

    /// <summary>Value から case 型への cast の TYPMOD016</summary>
    [Test]
    public async Task Casting_the_value_to_a_case_type_is_rejected()
    {
        var ids = await Ids("""
            using TypeModeling.Domain;

            internal static class Use
            {
                internal static int Cast(Result<int, string> result) => ((Succeeded<int>)result.Value!).Value;
                internal static int Pattern(Result<int, string> result) => result.Value is Succeeded<int> succeeded ? succeeded.Value : 0;
                internal static long Widen(int? number) => (long)number!.Value;
            }
            """);

        await Assert.That(ids.Count(id => id == "TYPMOD016")).IsEqualTo(1);
    }

    /// <summary>式文で捨てた MustUse の値の TYPMOD017 と、discard への代入の許可</summary>
    [Test]
    public async Task Discarding_a_must_use_value_is_reported()
    {
        var ids = await Ids("""
            using Probe;

            internal static class Use
            {
                internal static void Discard()
                {
                    Source.Parse("x");
                    Source.Count("x");
                    Source.Ignore("x");
                    Source.Described("x");
                    _ = Source.Parse("x");
                }
            }
            """);

        await Assert.That(ids.Count(id => id == "TYPMOD017")).IsEqualTo(2);
    }

    /// <summary>Result の analyzer を当てた診断の ID</summary>
    /// <param name="source">検査対象の C# source</param>
    /// <returns>診断の ID</returns>
    private static async Task<ImmutableArray<string>> Ids(string source)
    {
        var compilation = CSharpCompilation.Create(
            $"result-usage-{Guid.NewGuid():N}",
            [CSharpSyntaxTree.ParseText(source), CSharpSyntaxTree.ParseText(Domain)],
            [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
        var diagnostics = await compilation
            .WithAnalyzers([new ResultUsageAnalyzer(), new MustUseAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
        return [.. diagnostics.Select(diagnostic => diagnostic.Id)];
    }
}
