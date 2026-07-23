using System.Runtime.CompilerServices;
using TUnit.Core;
using TypeModeling.Testing.Attach;
using TypeModeling.Testing.Closure;

namespace TypeModeling.Testing.Tests;

/// <summary>TypeModeling library の Testing package 自己監査</summary>
[NotInParallel]
public sealed class SelfAuditTests
{
    /// <summary>closed type 使用 project と自 analyzer attachment の適合</summary>
    [Test]
    public async Task Closed_type_using_projects_attach_the_type_modeling_analyzer()
    {
        var repoRoot = RepoRoot();
        var violations = await AnalyzerAttachmentConformance.ViolationsAsync(
            repoRoot,
            new AnalyzerAttachmentRequirement(
                "src/TypeModeling.Analyzers/TypeModeling.Analyzers.csproj",
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "TCSSUP001",
                    "TCSSUP002",
                },
                new HashSet<string>(StringComparer.Ordinal) { "TYPMOD" },
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "src/TypeModeling/TypeModeling.csproj",
                    "tests/TypeModeling.Tests/TypeModeling.Tests.csproj",
                },
                new HashSet<string>(StringComparer.Ordinal) { "TypeModeling" }),
            new ClosedTypeUsageDetector(),
            CancellationToken.None);

        RequireNoViolations("closed type 使用 project の analyzer attachment", violations);
    }

    /// <summary>TypeModeling solution 閉包と閉じた階層規約の適合</summary>
    [Test]
    public async Task TypeModeling_solution_conforms_to_the_closed_hierarchy_rules()
    {
        var repoRoot = RepoRoot();
        var excludedProjects = new Dictionary<string, string>(StringComparer.Ordinal);
        var closure = await SolutionClosureLoader.LoadAsync(
            new SolutionClosureScan(
                repoRoot,
                "TypeModeling.slnx",
                "Debug",
                excludedProjects),
            CancellationToken.None);

        RequireNoViolations(
            "TypeModeling solution の project 棚卸し",
            SolutionClosureConformance.InventoryViolations(
                closure,
                SolutionClosureConformance.RepoProjectPaths(repoRoot),
                excludedProjects));
        RequireNoViolations(
            "TypeModeling solution の閉じた階層",
            SolutionClosureConformance.ClosedHierarchyViolations(closure));
    }

    /// <summary>test source path を基準にした repository root の解決</summary>
    /// <param name="sourcePath">呼び出し元 test source の絶対 path</param>
    /// <returns>TypeModeling repository root の絶対 path</returns>
    private static string RepoRoot([CallerFilePath] string sourcePath = "")
    {
        var testsDirectory = Path.GetDirectoryName(sourcePath) ??
            throw new InvalidOperationException("test source directory が見つからない");
        var repoRoot = Path.GetFullPath(Path.Combine(testsDirectory, "..", "..", ".."));
        if (File.Exists(Path.Combine(
                repoRoot,
                "src",
                "TypeModeling",
                "TypeModeling.csproj")))
        {
            return repoRoot;
        }

        throw new InvalidOperationException("TypeModeling repository root が見つからない");
    }

    /// <summary>自己監査 violations の空集合検証</summary>
    /// <param name="surface">検査面の表示名</param>
    /// <param name="violations">conformance engine が返した violations</param>
    private static void RequireNoViolations(
        string surface,
        IReadOnlyList<string> violations)
    {
        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                surface + " に違反がある:\n  " +
                string.Join("\n  ", violations));
        }
    }
}
