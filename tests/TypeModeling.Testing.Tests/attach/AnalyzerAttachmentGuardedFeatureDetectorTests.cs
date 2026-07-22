using Microsoft.CodeAnalysis;
using TUnit.Core;
using TypeModeling.Testing.Attach;
using static TypeModeling.Testing.Tests.Attach.AnalyzerAttachmentConformanceTestSupport;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>guarded feature detector 差替動作の検証</summary>
[NotInParallel]
public sealed class AnalyzerAttachmentGuardedFeatureDetectorTests
{
    /// <summary>差替 detector の呼び出しと期待 inventory 差分の報告</summary>
    [Test]
    public async Task Supplied_detector_controls_guarded_project_inventory()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        fixture.UseSourceGeneratorAnalyzer();
        var detector = new RecordingGuardedFeatureDetector();
        var requirement = fixture.Requirement with
        {
            ExpectedProjectPaths = new HashSet<string>(StringComparer.Ordinal)
            {
                "expected/Expected.csproj",
            },
        };

        var violations = await AnalyzerAttachmentConformance.ViolationsAsync(
            fixture.RepoRoot,
            requirement,
            detector,
            CancellationToken.None);

        if (detector.CallCount == 0)
            throw new InvalidOperationException("engine が差替 detector を呼び出さなかった");
        AssertPolicyViolationContainingAll(
            violations,
            ["不足", "expected/Expected.csproj"],
            "期待 inventory の不足 project を報告できなかった");
        AssertPolicyViolationContainingAll(
            violations,
            ["追加", "consumer/Consumer.csproj"],
            "期待 inventory にない追加 project を報告できなかった");
    }

    /// <summary>consumer compilation だけを guarded feature 利用として記録する detector</summary>
    private sealed class RecordingGuardedFeatureDetector : IGuardedFeatureDetector
    {
        /// <summary>detector 呼び出し回数</summary>
        internal int CallCount { get; private set; }

        /// <summary>consumer assembly に対する guarded feature 利用判定</summary>
        /// <param name="compilation">engine が構築した project compilation</param>
        /// <returns>consumer assembly の場合に true</returns>
        public bool UsesGuardedFeature(Compilation compilation)
        {
            CallCount++;
            return string.Equals(
                    compilation.AssemblyName,
                    "Fixture.Consumer",
                    StringComparison.Ordinal) &&
                compilation.SyntaxTrees.Any(tree =>
                    tree.FilePath.EndsWith(
                        "GeneratedGuardedUse.g.cs",
                        StringComparison.Ordinal));
        }
    }
}
