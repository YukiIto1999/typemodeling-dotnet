using Microsoft.CodeAnalysis;
using TUnit.Core;
using TypeModeling.Testing.Attach;

namespace TypeModeling.Testing.Tests.Attach;

/// <summary>複数の検めを一回の走査で判ずることと、検出の結果の控えの仕様</summary>
[NotInParallel]
public sealed class AnalyzerAttachmentSharedScanTests
{
    /// <summary>検めごとに走査した結果と一回の走査の結果が一致すること</summary>
    [Test]
    public async Task One_scan_judges_each_check_as_a_scan_per_check_does()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        var closed = new ClosedTypeUsageDetector();
        var never = new NeverUsesDetector();

        var shared = await AnalyzerAttachmentConformance.ViolationsAsync(
            fixture.RepoRoot,
            [
                new AnalyzerAttachmentCheck(fixture.Requirement, closed),
                new AnalyzerAttachmentCheck(fixture.Requirement, never),
            ],
            null,
            CancellationToken.None);
        var closedAlone = await AnalyzerAttachmentConformance.ViolationsAsync(
            fixture.RepoRoot,
            fixture.Requirement,
            closed,
            CancellationToken.None);
        var neverAlone = await AnalyzerAttachmentConformance.ViolationsAsync(
            fixture.RepoRoot,
            fixture.Requirement,
            never,
            CancellationToken.None);

        Equal(closedAlone, shared[0], "閉じた型の検めが単独の走査と食い違った");
        Equal(neverAlone, shared[1], "何も使わない検めが単独の走査と食い違った");
        if (shared[0].SequenceEqual(shared[1], StringComparer.Ordinal))
        {
            throw new InvalidOperationException("二つの検めが同じ結果になり、検めの区別を確かめられない");
        }
    }

    /// <summary>入力が同じ間は検出を重ねず、project の原文が変われば検出し直すこと</summary>
    [Test]
    public async Task A_verdict_is_kept_until_the_project_input_changes()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        var cache = Path.Combine(fixture.RepoRoot, ".verdicts");
        var detector = new CountingDetector();

        await ScanAsync(fixture, detector, cache);
        var first = detector.Calls;
        await ScanAsync(fixture, detector, cache);
        var second = detector.Calls;
        fixture.WriteConsumerSource(
            TemporaryAnalyzerAttachmentFixture.ClosedTypeDeclarations + "\n// changed\n");
        await ScanAsync(fixture, detector, cache);
        var third = detector.Calls;

        if (first == 0)
        {
            throw new InvalidOperationException("最初の走査が検出器を呼ばなかった");
        }

        if (second != first)
        {
            throw new InvalidOperationException(
                $"入力が同じなのに検出し直した: {first} 回から {second} 回");
        }

        if (third <= second)
        {
            throw new InvalidOperationException("原文が変わったのに検出し直さなかった");
        }
    }

    /// <summary>控えを使わない走査は検出を毎回行うこと</summary>
    [Test]
    public async Task A_scan_without_a_cache_directory_detects_every_time()
    {
        using var fixture = new TemporaryAnalyzerAttachmentFixture();
        var detector = new CountingDetector();

        await ScanAsync(fixture, detector, null);
        var first = detector.Calls;
        await ScanAsync(fixture, detector, null);

        if (detector.Calls <= first)
        {
            throw new InvalidOperationException("控えを使わないのに検出を重ねなかった");
        }
    }

    /// <summary>一つの検めの走査</summary>
    /// <param name="fixture">検査対象の一時 repository</param>
    /// <param name="detector">検出器</param>
    /// <param name="cacheDirectory">控えの置き場で、使わなければ null</param>
    /// <returns>走査の終わり</returns>
    private static Task<IReadOnlyList<IReadOnlyList<string>>> ScanAsync(
        TemporaryAnalyzerAttachmentFixture fixture,
        IGuardedFeatureDetector detector,
        string? cacheDirectory) =>
        AnalyzerAttachmentConformance.ViolationsAsync(
            fixture.RepoRoot,
            [new AnalyzerAttachmentCheck(fixture.Requirement, detector)],
            cacheDirectory,
            CancellationToken.None);

    /// <summary>二つの違反集合が同じであることの検め</summary>
    /// <param name="expected">期待する違反集合</param>
    /// <param name="actual">得た違反集合</param>
    /// <param name="message">食い違ったときの失敗メッセージ</param>
    private static void Equal(
        IReadOnlyList<string> expected,
        IReadOnlyList<string> actual,
        string message)
    {
        if (!expected.SequenceEqual(actual, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                message + ":\n期待\n" + string.Join("\n", expected) + "\n実際\n" + string.Join("\n", actual));
        }
    }

    /// <summary>どの project も guarded feature を使わないと判ずる検出器</summary>
    private sealed class NeverUsesDetector : IGuardedFeatureDetector
    {
        /// <summary>guarded feature の利用判定</summary>
        /// <param name="compilation">検査対象 compilation で、読まない</param>
        /// <returns>常に false</returns>
        public bool UsesGuardedFeature(Compilation compilation) => false;
    }

    /// <summary>呼ばれた回数を数え、閉じた型の使用を判ずる検出器</summary>
    private sealed class CountingDetector : IGuardedFeatureDetector
    {
        /// <summary>閉じた型の検出器</summary>
        private readonly ClosedTypeUsageDetector inner = new();

        /// <summary>検出した回数</summary>
        public int Calls { get; private set; }

        /// <summary>guarded feature の利用判定</summary>
        /// <param name="compilation">検査対象 compilation</param>
        /// <returns>閉じた型を使う場合に true</returns>
        public bool UsesGuardedFeature(Compilation compilation)
        {
            Calls++;
            return inner.UsesGuardedFeature(compilation);
        }
    }
}
