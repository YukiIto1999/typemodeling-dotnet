using TUnit.Core;
using TypeModeling.Testing.Closure;

namespace TypeModeling.Testing.Tests.Closure;

/// <summary>solution 閉包に対する repo project inventory の対応検証</summary>
public sealed class SolutionClosureConformanceInventoryTests
{
    /// <summary>bin と obj を除いた repo 全 project の相対パス列挙</summary>
    [Test]
    public async Task Repo_project_paths_skip_bin_and_obj_and_sort_ordinally()
    {
        var root = Directory.CreateTempSubdirectory("typemodeling-repo-projects-").FullName;
        try
        {
            foreach (var relative in (string[])
                     [
                         "src/App/App.csproj",
                         "tests/App.Tests/App.Tests.csproj",
                         "src/App/bin/Debug/Decoy.csproj",
                         "src/App/obj/Decoy.csproj",
                     ])
            {
                var path = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(
                    path,
                    "<Project Sdk=\"Microsoft.NET.Sdk\" />",
                    CancellationToken.None);
            }

            var paths = SolutionClosureConformance.RepoProjectPaths(root);

            await Assert.That(paths).IsEquivalentTo([
                "src/App/App.csproj",
                "tests/App.Tests/App.Tests.csproj",
            ]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>git が除外する生成 project を repo 棚卸しに含めない規約</summary>
    [Test]
    public async Task Repo_project_paths_respect_git_ignored_directories()
    {
        var root = Directory.CreateTempSubdirectory("typemodeling-git-projects-").FullName;
        try
        {
            var gitPath = (Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(directory => Path.Combine(directory, OperatingSystem.IsWindows() ? "git.exe" : "git"))
                .First(File.Exists);
            var start = new System.Diagnostics.ProcessStartInfo(gitPath)
            {
                WorkingDirectory = root,
                ArgumentList = { "init", "-q" },
                RedirectStandardError = true,
            };
            foreach (var name in start.Environment.Keys.Where(name =>
                         name.StartsWith("GIT_", StringComparison.Ordinal)).ToArray())
                start.Environment.Remove(name);
            using (var git = System.Diagnostics.Process.Start(start)!)
            {
                await git.WaitForExitAsync();
                await Assert.That(git.ExitCode).IsEqualTo(0);
            }

            await File.WriteAllTextAsync(Path.Combine(root, ".gitignore"), ".devenv/\ngenerated/\n");
            foreach (var relative in (string[])
                     [
                         "src/App/App.csproj",
                         "orphan/Orphan.csproj",
                         ".devenv/state/mutation-dotnet/Mutation.csproj",
                         "generated/Generated.csproj",
                     ])
            {
                var path = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            }

            await Assert.That(SolutionClosureConformance.RepoProjectPaths(root)).IsEquivalentTo([
                "orphan/Orphan.csproj",
                "src/App/App.csproj",
            ]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>閉包にも除外にも属さない repo project の違反報告</summary>
    [Test]
    public async Task An_orphan_repo_project_is_reported()
    {
        var violations = SolutionClosureConformance.InventoryViolations(
            Closure("app/App.csproj"),
            ["app/App.csproj", "orphan/Orphan.csproj"],
            new Dictionary<string, string>(StringComparer.Ordinal));

        await Assert.That(violations).IsEquivalentTo([
            "orphan/Orphan.csproj: repo project is neither in the static closure nor explicitly excluded",
        ]);
    }

    /// <summary>根拠を持たない除外の違反報告</summary>
    [Test]
    public async Task An_exclusion_without_justification_is_reported()
    {
        var violations = SolutionClosureConformance.InventoryViolations(
            Closure("app/App.csproj"),
            ["app/App.csproj", "tests/Unjustified.Tests.csproj"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["tests/Unjustified.Tests.csproj"] = "",
            });

        await Assert.That(violations).IsEquivalentTo([
            "tests/Unjustified.Tests.csproj: exclusion has no justification",
        ]);
    }

    /// <summary>根拠付き除外による inventory 規約の成立</summary>
    [Test]
    public async Task A_justified_exclusion_satisfies_the_inventory()
    {
        var violations = SolutionClosureConformance.InventoryViolations(
            Closure("app/App.csproj"),
            ["app/App.csproj", "tests/Tool.Tests.csproj"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["tests/Tool.Tests.csproj"] = "test-only project outside the shipped solution closure",
            });

        await Assert.That(violations).IsEmpty();
    }

    /// <summary>静的閉包への所属による inventory 規約の成立</summary>
    [Test]
    public async Task A_project_in_the_static_closure_satisfies_the_inventory()
    {
        var violations = SolutionClosureConformance.InventoryViolations(
            Closure("app/App.csproj"),
            ["app/App.csproj"],
            new Dictionary<string, string>(StringComparer.Ordinal));

        await Assert.That(violations).IsEmpty();
    }

    /// <summary>存在しない project を指す除外の違反報告</summary>
    [Test]
    public async Task A_stale_exclusion_is_reported()
    {
        var violations = SolutionClosureConformance.InventoryViolations(
            Closure("app/App.csproj"),
            ["app/App.csproj"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["removed/Removed.csproj"] = "removed test harness",
            });

        await Assert.That(violations).IsEquivalentTo([
            "removed/Removed.csproj: exclusion is stale because the repo project does not exist",
        ]);
    }

    /// <summary>静的閉包と重複する除外の違反報告</summary>
    [Test]
    public async Task An_exclusion_overlapping_the_static_closure_is_reported()
    {
        var violations = SolutionClosureConformance.InventoryViolations(
            Closure("app/App.csproj"),
            ["app/App.csproj"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["app/App.csproj"] = "incorrect overlap",
            });

        await Assert.That(violations).IsEquivalentTo([
            "app/App.csproj: exclusion overlaps the static closure",
        ]);
    }

    private static LoadedSolutionClosure Closure(params string[] projectPaths) =>
        new(projectPaths, [], []);
}
