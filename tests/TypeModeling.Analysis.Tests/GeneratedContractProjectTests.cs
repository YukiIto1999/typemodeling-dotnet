using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace TypeModeling.Analysis.Tests;

/// <summary>contract-generator が出す形の project が、利用側の project として solution の検査を通ること</summary>
/// <remarks>生成 project は利用側の project である。props の取り込みが届けば通り、届かなければ、どこから参照されても TYPMOD204 で失敗する</remarks>
[NotInParallel]
public sealed class GeneratedContractProjectTests
{
    /// <summary>solution に載る生成 project は、props が届けば build できること</summary>
    [Test]
    public async Task Generated_projects_listed_in_the_solution_build_when_the_props_reach_them()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteGeneratedContractProjects();
        fixture.WriteSolution(
            "generated/types/Contract.Types.csproj",
            "generated/provider/Contract.Provider.csproj"
        );

        var result = await fixture.BuildSolutionAsync();

        await Assert.That(result.Succeeded).IsTrue().Because(result.Describe());
    }

    /// <summary>solution に載せず、載っている project が参照する生成 project は、props が届けば build できること</summary>
    [Test]
    public async Task Generated_projects_that_a_listed_project_references_build_when_the_props_reach_them()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteGeneratedContractProjects();
        WriteConsumerThatReferencesProvider(fixture);
        fixture.WriteSolution("consumer/Consumer.csproj");

        var result = await fixture.BuildSolutionAsync();

        await Assert.That(result.Succeeded).IsTrue().Because(result.Describe());
    }

    /// <summary>props の届かない位置に生成された project は、載っている project が参照すれば、solution の build が TYPMOD204 で失敗すること</summary>
    [Test]
    public async Task Generated_projects_outside_the_props_chain_fail_when_a_listed_project_references_them()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteGeneratedContractProjects(reached: false);
        WriteConsumerThatReferencesProvider(fixture);
        fixture.WriteSolution("consumer/Consumer.csproj");

        var result = await fixture.BuildSolutionAsync();

        await Assert.That(result.Reports("TYPMOD204")).IsTrue().Because(result.Describe());
        await Assert
            .That(
                result.Errors.Any(error =>
                    error.Contains("Contract.Provider.csproj", StringComparison.Ordinal)
                )
            )
            .IsTrue()
            .Because(result.Describe());
    }

    /// <summary>生成した Provider を参照する consumer を書く。consumer は、参照を通して届く TypeModeling の型を、自分では宣言しない</summary>
    /// <param name="fixture">書き込み先の fixture</param>
    private static void WriteConsumerThatReferencesProvider(ConsumerFixture fixture)
    {
        fixture.WriteProject(
            """<ItemGroup><ProjectReference Include="../generated/provider/Contract.Provider.csproj" /></ItemGroup>"""
        );
        fixture.WriteSource("namespace Fixture.Consumer { internal sealed class Marker; }");
    }
}
