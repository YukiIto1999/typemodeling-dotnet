using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace TypeModeling.Analysis.Tests;

/// <summary>Directory.Build.props の 1 か所の取り込みで、analyzer が全ての C# project に届くこと</summary>
/// <remarks>旧 AnalyzerAttachmentRequirementTests、AnalyzerAttachmentBuildDimensionTests の取り込みの性質に当たる</remarks>
[NotInParallel]
public sealed class AnalyzerImportTests
{
    /// <summary>analyzer への参照を書かない新しい project が、閉じた型の規則を守らされること</summary>
    [Test]
    public async Task A_project_without_its_own_reference_is_checked_by_the_analyzer()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteSource(ConsumerFixture.ViolatingSource);

        var result = await fixture.BuildAsync();

        await Assert.That(result.Reports("TYPMOD003")).IsTrue().Because(result.Describe());
        await Assert.That(result.Succeeded).IsFalse();
    }

    /// <summary>規則に従う project は Debug と Release の両方で build できること</summary>
    /// <param name="configuration">build の構成</param>
    [Test]
    [Arguments("Debug")]
    [Arguments("Release")]
    public async Task A_conforming_project_builds(string configuration)
    {
        using var fixture = new ConsumerFixture();

        var result = await fixture.BuildAsync("-c", configuration);

        await Assert.That(result.Succeeded).IsTrue().Because(result.Describe());
    }

    /// <summary>Release にだけある違反も、Release の build で analyzer が検出すること</summary>
    [Test]
    public async Task A_violation_that_exists_only_in_release_is_found_by_the_release_build()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteProject(
            """
            <PropertyGroup><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
            <ItemGroup><Compile Include="Source.cs" /></ItemGroup>
            <ItemGroup Condition="'$(Configuration)' == 'Release'">
              <Compile Include="ReleaseOnly.cs" />
            </ItemGroup>
            """
        );
        fixture.WriteFile(
            "consumer/ReleaseOnly.cs",
            "namespace Fixture.Consumer { [TypeModeling.Domain.Closed] public class ReleaseConcrete; }"
        );

        var debug = await fixture.BuildAsync("-c", "Debug");
        var release = await fixture.BuildAsync("-c", "Release");

        await Assert.That(release.Reports("TYPMOD003")).IsTrue().Because(release.Describe());
        await Assert.That(debug.Succeeded).IsTrue().Because(debug.Describe());
    }

    /// <summary>入れ子の Directory.Build.props が親を取り込めば、入れ子の project にも届くこと</summary>
    [Test]
    public async Task A_nested_Directory_Build_props_that_chains_to_its_parent_still_reaches_the_project()
    {
        using var fixture = new ConsumerFixture();
        fixture.WriteFile(
            "consumer/Directory.Build.props",
            """
            <Project>
              <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />
            </Project>
            """
        );
        fixture.WriteSource(ConsumerFixture.ViolatingSource);

        var result = await fixture.BuildAsync();

        await Assert.That(result.Reports("TYPMOD003")).IsTrue().Because(result.Describe());
    }

    /// <summary>props が指す analyzer project が checkout に無ければ、build が失敗すること</summary>
    [Test]
    public async Task A_checkout_without_the_analyzer_project_fails_the_build()
    {
        var incomplete = Path.Combine(
            Path.GetTempPath(),
            "typemodeling-incomplete-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(incomplete);
        try
        {
            File.Copy(
                Path.Combine(ConsumerFixture.CurrentCheckout(), "TypeModeling.Analysis.props"),
                Path.Combine(incomplete, "TypeModeling.Analysis.props")
            );
            using var fixture = new ConsumerFixture(incomplete);

            var result = await fixture.BuildAsync();

            await Assert.That(result.Reports("TYPMOD201")).IsTrue().Because(result.Describe());
        }
        finally
        {
            Directory.Delete(incomplete, recursive: true);
        }
    }

    /// <summary>build の出力が無い checkout でも、利用側より先に analyzer が build されて届くこと</summary>
    [Test]
    public async Task A_fresh_checkout_builds_the_analyzer_before_the_consumer()
    {
        var fresh = Path.Combine(
            Path.GetTempPath(),
            "typemodeling-fresh-" + Guid.NewGuid().ToString("N")
        );
        try
        {
            CheckoutCopy.Create(ConsumerFixture.CurrentCheckout(), fresh);
            using var fixture = new ConsumerFixture(fresh);
            fixture.WriteSource(ConsumerFixture.ViolatingSource);

            var result = await fixture.BuildAsync();

            await Assert.That(result.Reports("TYPMOD003")).IsTrue().Because(result.Describe());
        }
        finally
        {
            Directory.Delete(fresh, recursive: true);
        }
    }
}
