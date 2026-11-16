using System.Runtime.Loader;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using TypeModeling.Testing.Closure;

namespace TypeModeling.Testing.Tests.Closure;

/// <summary>solution 閉包 loader の実 process 統合検証</summary>
[NotInParallel]
public sealed class SolutionClosureLoaderTests
{
    /// <summary>推移 ProjectReference solution の完全ロード</summary>
    [Test]
    public async Task A_transitive_project_reference_solution_is_built_evaluated_and_loaded()
    {
        using var fixture = new TemporarySolutionFixture();
        var scan = new SolutionClosureScan(
            fixture.RepoRoot,
            fixture.SolutionFileName,
            "Debug",
            new Dictionary<string, string>(StringComparer.Ordinal));

        var closure = await SolutionClosureLoader.LoadAsync(
            scan,
            CancellationToken.None);

        await Assert.That(closure.ExpectedProjectPaths).IsEquivalentTo([
            "../external/External.csproj",
            "App/App.csproj",
            "Library/Library.csproj",
        ]);
        await Assert.That(closure.Projects.Select(project => project.ProjectPath)).IsEquivalentTo([
            "../external/External.csproj",
            "App/App.csproj",
            "Library/Library.csproj",
        ]);
        await Assert.That(closure.ReflectionTypes.Select(type => type.FullName ?? type.Name)).IsEquivalentTo([
            "Fixture.App.AppType",
            "Fixture.External.ExternalType",
            "Fixture.Library.LibraryType",
        ]);
        await Assert.That(closure.Projects.All(project =>
            AssemblyLoadContext.GetLoadContext(project.Assembly) != AssemblyLoadContext.Default)).IsTrue();
        foreach (var project in closure.Projects)
        {
            await Assert.That(Path.GetFullPath(project.Assembly.Location))
                .IsEqualTo(Path.GetFullPath(project.TargetPath));
            await Assert.That(project.Assembly.ManifestModule.ModuleVersionId)
                .IsEqualTo(project.TargetMvid);
        }
    }

    /// <summary>build より先行する静的解決不能 ProjectReference の拒否</summary>
    [Test]
    public async Task A_statically_unresolvable_project_reference_is_rejected_before_build()
    {
        using var fixture = new TemporarySolutionFixture();
        fixture.UseStaticallyUnresolvableReferenceWithBrokenBuild();
        var scan = new SolutionClosureScan(
            fixture.RepoRoot,
            fixture.SolutionFileName,
            "Debug",
            new Dictionary<string, string>(StringComparer.Ordinal));

        await Assert.That(async () => await SolutionClosureLoader.LoadAsync(
                scan,
                CancellationToken.None))
            .ThrowsExactly<InvalidOperationException>()
            .WithMessageContaining(
                "App/App.csproj: inventoryが静的に解決できないProjectReference: $(LibraryProject)");
    }

    /// <summary>contract-generator が出す形の project の、Directory.Build.props の property による参照を閉包と台帳の検査が読むこと</summary>
    [Test]
    public async Task A_project_reference_through_a_property_of_Directory_Build_props_is_resolved()
    {
        using var fixture = new TemporarySolutionFixture();
        fixture.UseGeneratedContractShape();
        var excluded = new Dictionary<string, string>(StringComparer.Ordinal);
        var scan = new SolutionClosureScan(
            fixture.RepoRoot,
            fixture.SolutionFileName,
            "Debug",
            excluded);

        var closure = await SolutionClosureLoader.LoadAsync(
            scan,
            CancellationToken.None);

        await Assert.That(closure.ExpectedProjectPaths).IsEquivalentTo([
            "../external/External.csproj",
            "App/App.csproj",
            "generated/provider/Contract.Provider.csproj",
            "generated/types/Contract.Types.csproj",
        ]);
        await Assert.That(closure.Projects.Select(project => project.ProjectPath)).IsEquivalentTo(
            closure.ExpectedProjectPaths);
        await Assert.That(SolutionClosureConformance.InventoryViolations(
                closure,
                SolutionClosureConformance.RepoProjectPaths(fixture.RepoRoot),
                excluded))
            .IsEmpty();
        await Assert.That(SolutionClosureConformance.ClosedHierarchyViolations(closure)).IsEmpty();
    }
}
