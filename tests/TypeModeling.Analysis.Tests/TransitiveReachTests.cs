using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace TypeModeling.Analysis.Tests;

/// <summary>solution に載せず、ProjectReference で推移的にだけ build される project も、props の取り込みが届かなければ solution の build を TYPMOD204 で失敗させること</summary>
/// <remarks>旧 inventory は solution の推移閉包の全ての project を検査していた。直接載る project だけを検査すると、閉包の中の届かない project が analyzer なしで compile される</remarks>
[NotInParallel]
public sealed class TransitiveReachTests
{
    /// <summary>複数の framework を build する、取り込みが届く project の framework の宣言</summary>
    private const string MultiTargeting = "<TargetFrameworks>net10.0;net8.0</TargetFrameworks>";

    /// <summary>solution に載せず、consumer が参照するだけの project を、親を import しない Directory.Build.props の下に置く</summary>
    /// <param name="fixture">書き込み先の fixture</param>
    /// <param name="directory">project を置く、root からの相対 directory</param>
    /// <param name="name">project の名前</param>
    /// <param name="body">project に足す ItemGroup</param>
    /// <param name="frameworks">TargetFramework か TargetFrameworks の宣言</param>
    private static void WriteUnreachedProject(
        ConsumerFixture fixture,
        string directory,
        string name,
        string body = "",
        string frameworks = "<TargetFramework>net10.0</TargetFramework>"
    )
    {
        fixture.WriteFile($"{directory}/Directory.Build.props", "<Project />");
        fixture.WriteFile($"{directory}/{name}.csproj", Project(body, frameworks));
    }

    /// <summary>取り込みが届く位置に project を置く。root の Directory.Build.props が届く</summary>
    /// <param name="fixture">書き込み先の fixture</param>
    /// <param name="directory">project を置く、root からの相対 directory</param>
    /// <param name="name">project の名前</param>
    /// <param name="body">project に足す ItemGroup</param>
    /// <param name="frameworks">TargetFramework か TargetFrameworks の宣言</param>
    private static void WriteReachedProject(
        ConsumerFixture fixture,
        string directory,
        string name,
        string body = "",
        string frameworks = "<TargetFramework>net10.0</TargetFramework>"
    ) => fixture.WriteFile($"{directory}/{name}.csproj", Project(body, frameworks));

    /// <summary>最小の project の内容</summary>
    /// <param name="body">project に足す ItemGroup</param>
    /// <param name="frameworks">TargetFramework か TargetFrameworks の宣言</param>
    /// <returns>csproj の内容</returns>
    private static string Project(string body, string frameworks) =>
        $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>{frameworks}</PropertyGroup>
              {body}
            </Project>
            """;

    /// <summary>ProjectReference の ItemGroup</summary>
    /// <param name="include">参照先の、参照元からの相対 path</param>
    /// <param name="condition">ItemGroup の Condition</param>
    /// <returns>ItemGroup の内容</returns>
    private static string Reference(string include, string condition = "true") =>
        $"""<ItemGroup Condition="{condition}"><ProjectReference Include="{include}" /></ItemGroup>""";

    /// <summary>solution に載せず、取り込みの届かない project を、載っている project が参照すると、solution の build が失敗すること</summary>
    [Test]
    public async Task A_project_that_is_only_built_through_a_reference_and_is_not_reached_fails_the_solution_build()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(Reference("../transitive/Transitive.csproj"));
        WriteUnreachedProject(fixture, "transitive", "Transitive");
        fixture.WriteSolution("consumer/Consumer.csproj");

        var result = await fixture.BuildSolutionAsync();

        await Assert.That(result.Reports("TYPMOD204")).IsTrue().Because(result.Describe());
        await Assert
            .That(
                result.Errors.Any(error =>
                    error.Contains("Transitive.csproj", StringComparison.Ordinal)
                )
            )
            .IsTrue()
            .Because(result.Describe());
    }

    /// <summary>参照の連なりの途中までは取り込みが届き、その先の project だけが届かない場合に、その project を挙げて失敗すること</summary>
    [Test]
    public async Task A_project_deeper_in_the_reference_chain_is_named_when_only_it_is_not_reached()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(Reference("../middle/Middle.csproj"));
        WriteReachedProject(fixture, "middle", "Middle", Reference("../deep/Deep.csproj"));
        WriteUnreachedProject(fixture, "deep", "Deep");
        fixture.WriteSolution("consumer/Consumer.csproj");

        var result = await fixture.BuildSolutionAsync();

        await Assert.That(result.Reports("TYPMOD204")).IsTrue().Because(result.Describe());
        await Assert
            .That(
                result.Errors.Any(error => error.Contains("Deep.csproj", StringComparison.Ordinal))
            )
            .IsTrue()
            .Because(result.Describe());
    }

    /// <summary>参照される project の全てに取り込みが届く solution は、推移的に build されても通ること</summary>
    [Test]
    public async Task A_solution_whose_transitively_built_projects_are_all_reached_builds()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(Reference("../middle/Middle.csproj"));
        WriteReachedProject(fixture, "middle", "Middle", Reference("../deep/Deep.csproj"));
        WriteReachedProject(fixture, "deep", "Deep");
        fixture.WriteSolution("consumer/Consumer.csproj");

        var result = await fixture.BuildSolutionAsync();

        await Assert.That(result.Succeeded).IsTrue().Because(result.Describe());
    }

    /// <summary>ある target framework だけが宣言する参照も、たどって検出すること</summary>
    [Test]
    public async Task A_reference_that_only_one_target_framework_declares_is_followed()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            Reference("../transitive/Transitive.csproj", "'$(TargetFramework)' == 'net8.0'"),
            MultiTargeting
        );
        WriteUnreachedProject(fixture, "transitive", "Transitive", frameworks: MultiTargeting);
        fixture.WriteSolution("consumer/Consumer.csproj");

        var result = await fixture.BuildSolutionAsync();

        await Assert.That(result.Reports("TYPMOD204")).IsTrue().Because(result.Describe());
        await Assert
            .That(
                result.Errors.Any(error =>
                    error.Contains("Transitive.csproj", StringComparison.Ordinal)
                )
            )
            .IsTrue()
            .Because(result.Describe());
    }

    /// <summary>複数の framework を build する project の連なりに取り込みが届く solution は、通ること</summary>
    [Test]
    public async Task A_multi_targeted_chain_that_is_reached_builds()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(Reference("../middle/Middle.csproj"), MultiTargeting);
        WriteReachedProject(fixture, "middle", "Middle", frameworks: MultiTargeting);
        fixture.WriteSolution("consumer/Consumer.csproj");

        var result = await fixture.BuildSolutionAsync();

        await Assert.That(result.Succeeded).IsTrue().Because(result.Describe());
    }
}
