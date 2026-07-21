using Microsoft.CodeAnalysis;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ValueObjectGeneratorDiagnosticFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>ValueObject Parse 署名の生成時診断</summary>
public sealed class Typmod002Tests
{
    /// <summary>Parse 欠落の生成時診断</summary>
    [Test]
    public async Task Typmod002_reports_missing_Parse()
    {
        var result = RunGenerator("""
            using TypeModeling.Domain;

            namespace Probe;

            [ValueObject<int>]
            public sealed partial record Positive;
            """);

        await AssertTypmod002(result, "Parse が見つからない。");
    }

    /// <summary>Parse 引数型不一致の生成時診断</summary>
    [Test]
    public async Task Typmod002_reports_underlying_parameter_type_mismatch()
    {
        var result = RunGenerator("""
            using TypeModeling.Domain;

            namespace Probe;

            public sealed record ValidationFailure;

            [ValueObject<int>]
            public sealed partial record Positive
            {
                private static Result<int, ValidationFailure> Parse(string raw) => throw null!;
            }
            """);

        await AssertTypmod002(
            result,
            "Parse の引数型 string は基底型 int と一致しない。");
    }

    /// <summary>Parse 返却型不一致の生成時診断</summary>
    [Test]
    public async Task Typmod002_reports_non_Result_return_type()
    {
        var result = RunGenerator("""
            using TypeModeling.Domain;

            namespace Probe;

            [ValueObject<int>]
            public sealed partial record Positive
            {
                private static int Parse(int raw) => raw;
            }
            """);

        await AssertTypmod002(
            result,
            "Parse の返却型 int は Result<int, TFailure> ではない。");
    }

    /// <summary>入れ子同名 Result 返却型の生成時診断</summary>
    [Test]
    public async Task Typmod002_reports_nested_Result_spoof_return_type()
    {
        var result = RunGenerator("""
            using TypeModeling.Domain;

            namespace TypeModeling.Domain
            {
                public static class Container
                {
                    public abstract record Result<TValue, TFailure>;
                }
            }

            namespace Probe
            {
                public sealed record ValidationFailure;

                [ValueObject<int>]
                public sealed partial record Positive
                {
                    private static Container.Result<int, ValidationFailure> Parse(int raw) => throw null!;
                }
            }
            """);

        await AssertTypmod002(
            result,
            "Parse の返却型 TypeModeling.Domain.Container.Result<int, Probe.ValidationFailure> は Result<int, TFailure> ではない。");
    }

    /// <summary>Parse アクセシビリティ不一致の生成時診断</summary>
    [Test]
    public async Task Typmod002_reports_accessibility_mismatch()
    {
        var result = RunGenerator("""
            using TypeModeling.Domain;

            namespace Probe;

            public sealed record ValidationFailure;

            [ValueObject<int>]
            public sealed partial record Positive
            {
                public static Result<int, ValidationFailure> Parse(int raw) => throw null!;
            }
            """);

        await AssertTypmod002(
            result,
            "Parse のアクセシビリティは private ではない。");
    }

    /// <summary>適合 Parse の診断抑止</summary>
    [Test]
    public async Task Typmod002_does_not_report_for_conforming_Parse()
    {
        var result = RunGenerator("""
            using TypeModeling.Domain;

            namespace Probe;

            public sealed record ValidationFailure;

            [ValueObject<int>]
            public sealed partial record Positive
            {
                private static Result<int, ValidationFailure> Parse(int raw) => throw null!;
            }
            """);

        await Assert.That(result.GeneratorDiagnostics
                .Any(diagnostic => diagnostic.Id == "TYPMOD002"))
            .IsFalse();
        await Assert.That(result.GeneratedSources.Length).IsEqualTo(1);
        await Assert.That(result.Compilation.GetDiagnostics()
                .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            .IsFalse();
    }

    /// <summary>通常 class の診断対象外判定</summary>
    [Test]
    public async Task Typmod002_does_not_report_for_non_record_class()
    {
        var result = RunGenerator("""
            using TypeModeling.Domain;

            namespace Probe;

            [ValueObject<int>]
            public sealed class Positive;
            """);

        await Assert.That(result.GeneratorDiagnostics
                .Any(diagnostic => diagnostic.Id == "TYPMOD002"))
            .IsFalse();
        await Assert.That(result.GeneratedSources).IsEmpty();
    }
}
