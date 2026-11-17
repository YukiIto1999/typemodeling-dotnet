using Microsoft.CodeAnalysis;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ValueObjectConstructionFixture;
using static TypeModeling.Analyzers.Tests.ValueObjectSources;

namespace TypeModeling.Analyzers.Tests;

/// <summary>値オブジェクトの構築のうち、生成した Create と with の複製が許されることの確認</summary>
public sealed class ValueObjectCreateTests
{
    /// <summary>生成した Create、with の複製、他の型の new を許す確認</summary>
    [Test]
    public async Task Create_with_copies_and_other_creations_are_not_rejected()
    {
        var (result, flagged) = await Analyze(
            Declare(
                SealedPositive,
                """
                public Positive Copy() => this with { };
                public static object Other() => new object();
                public static ValidationFailure Failure() => new ValidationFailure();
                public static System.Collections.Generic.List<Positive> Items() => new();
                """
            )
                + """

                public static class Consumer
                {
                    public static Result<Positive, ValidationFailure> Build(int raw) => Positive.Create(raw);

                    public static Positive Copy(Positive value) => value with { };
                }
                """
        );

        await Assert.That(result.GeneratedSources.Length).IsEqualTo(1);
        await Assert.That(result.GeneratorDiagnostics).IsEmpty();
        await Assert.That(flagged).IsEmpty();
        await Assert
            .That(
                result
                    .Compilation.GetDiagnostics()
                    .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            )
            .IsFalse();
    }

    /// <summary>生成する Create と同じ署名の宣言が compile できないことの確認</summary>
    [Test]
    public async Task A_declaration_that_repeats_the_generated_Create_does_not_compile()
    {
        var (result, _) = await Analyze(
            Declare(
                SealedPositive,
                "public static Result<Positive, ValidationFailure> Create(int raw) => throw null!;"
            )
        );

        await Assert
            .That(
                result
                    .Compilation.GetDiagnostics()
                    .Any(diagnostic =>
                        diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id == "CS0111"
                    )
            )
            .IsTrue();
    }
}
