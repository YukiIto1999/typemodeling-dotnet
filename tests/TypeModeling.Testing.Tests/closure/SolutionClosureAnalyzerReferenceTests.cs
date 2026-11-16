using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using TypeModeling.Testing.Closure;

namespace TypeModeling.Testing.Tests.Closure;

/// <summary>props が足す、analyzer だけを届ける参照の扱い</summary>
/// <remarks>analyzer だけを届ける参照は runtime の閉包に入れず、通常の参照の漏れは引き続き検出する</remarks>
[NotInParallel]
public sealed class SolutionClosureAnalyzerReferenceTests
{
    /// <summary>props だけが足す analyzer だけを届ける参照は、宣言された graph との比較で除外されること</summary>
    [Test]
    public async Task An_analyzer_only_reference_added_by_imported_props_is_not_a_closure_member()
    {
        using var fixture = new TemporarySolutionFixture();
        fixture.UseAnalyzerOnlyReferenceFromImportedProps();

        var closure = await SolutionClosureLoader.LoadAsync(Scan(fixture), CancellationToken.None);

        await Assert
            .That(closure.Projects.Select(project => project.ProjectPath))
            .IsEquivalentTo([
                "../external/External.csproj",
                "App/App.csproj",
                "Library/Library.csproj",
            ]);
    }

    /// <summary>props だけが足す通常の参照は、宣言された graph に無ければ引き続き拒否されること</summary>
    [Test]
    public async Task A_runtime_reference_added_only_by_imported_props_is_still_rejected()
    {
        using var fixture = new TemporarySolutionFixture();
        fixture.UseRuntimeReferenceFromImportedProps();

        await Assert
            .That(async () =>
                await SolutionClosureLoader.LoadAsync(Scan(fixture), CancellationToken.None)
            )
            .ThrowsExactly<InvalidOperationException>()
            .WithMessageContaining(
                "../external/Companion.csproj: loaded project is absent from the declared build graph"
            );
    }

    /// <summary>fixture の solution を走査する設定</summary>
    /// <param name="fixture">走査する fixture</param>
    /// <returns>除外のない走査の設定</returns>
    private static SolutionClosureScan Scan(TemporarySolutionFixture fixture) =>
        new(
            fixture.RepoRoot,
            fixture.SolutionFileName,
            "Debug",
            new Dictionary<string, string>(StringComparer.Ordinal)
        );
}
