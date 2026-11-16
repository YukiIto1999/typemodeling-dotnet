using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace TypeModeling.Analysis.Tests;

/// <summary>props の取り込みが届かない project を、solution の build で TYPMOD204 として失敗させること</summary>
/// <remarks>旧 inventory が検出していた、analyzer の届かない project に当たる。実行時の test には戻さず build の失敗で塞ぐ</remarks>
[NotInParallel]
public sealed class ReachTests
{
    /// <summary>SDK の props より前に ImportDirectoryBuildProps を false にして、取り込みを外した project</summary>
    private const string OptOutProject = """
        <Project>
          <PropertyGroup><ImportDirectoryBuildProps>false</ImportDirectoryBuildProps></PropertyGroup>
          <Import Project="Sdk.props" Sdk="Microsoft.NET.Sdk" />
          <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
          <Import Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" />
        </Project>
        """;

    /// <summary>全ての project が props を取り込む solution は build できること</summary>
    [Test]
    public async Task A_solution_whose_projects_all_import_the_props_builds()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteSolution("consumer/Consumer.csproj");

        var result = await fixture.BuildSolutionAsync();

        await Assert.That(result.Succeeded).IsTrue().Because(result.Describe());
    }

    /// <summary>取り込みを外した project があれば、solution の build が TYPMOD204 でその project を挙げて失敗すること</summary>
    [Test]
    public async Task A_project_that_opts_out_of_Directory_Build_props_fails_the_solution_build()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteFile("optout/OptOut.csproj", OptOutProject);
        fixture.WriteSolution("consumer/Consumer.csproj", "optout/OptOut.csproj");

        var result = await fixture.BuildSolutionAsync();

        await Assert.That(result.Reports("TYPMOD204")).IsTrue().Because(result.Describe());
        await Assert
            .That(
                result.Errors.Any(error =>
                    error.Contains("OptOut.csproj", StringComparison.Ordinal)
                )
            )
            .IsTrue();
        await Assert
            .That(
                result.Errors.Any(error =>
                    error.Contains("Consumer.csproj", StringComparison.Ordinal)
                )
            )
            .IsFalse();
    }

    /// <summary>親を import しない入れ子の Directory.Build.props の下の project があれば、solution の build が TYPMOD204 で失敗すること</summary>
    [Test]
    public async Task A_nested_Directory_Build_props_that_does_not_chain_fails_the_solution_build()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteFile("nested/Directory.Build.props", "<Project />");
        fixture.WriteFile(
            "nested/Nested.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"
        );
        fixture.WriteSolution("consumer/Consumer.csproj", "nested/Nested.csproj");

        var result = await fixture.BuildSolutionAsync();

        await Assert.That(result.Reports("TYPMOD204")).IsTrue().Because(result.Describe());
        await Assert
            .That(
                result.Errors.Any(error =>
                    error.Contains("Nested.csproj", StringComparison.Ordinal)
                )
            )
            .IsTrue();
    }

    /// <summary>global property で ImportDirectoryBuildProps を false にした build は、全ての project を届かないとして失敗すること</summary>
    [Test]
    public async Task The_global_property_ImportDirectoryBuildProps_false_fails_the_solution_build()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteSolution("consumer/Consumer.csproj");

        var result = await fixture.BuildSolutionAsync("-p:ImportDirectoryBuildProps=false");

        await Assert.That(result.Reports("TYPMOD204")).IsTrue().Because(result.Describe());
        await Assert
            .That(
                result.Errors.Any(error =>
                    error.Contains("Consumer.csproj", StringComparison.Ordinal)
                )
            )
            .IsTrue();
    }
}
