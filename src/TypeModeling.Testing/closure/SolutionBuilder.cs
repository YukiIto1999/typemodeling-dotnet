using TypeModeling.Testing.MsBuild;

namespace TypeModeling.Testing.Closure;

/// <summary>solution build の実行機</summary>
internal static class SolutionBuilder
{
    /// <summary>project assets がない場合の solution restore</summary>
    /// <param name="repoRoot">repository root の絶対パス</param>
    /// <param name="solutionFileName">solution ファイル名</param>
    /// <param name="projectPaths">solution root 相対の静的 project 閉包</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>必要な restore の非同期完了</returns>
    internal static async Task EnsureRestoredAsync(
        string repoRoot,
        string solutionFileName,
        IReadOnlyList<string> projectPaths,
        CancellationToken cancellationToken)
    {
        var allAssetsExist = projectPaths.All(projectPath =>
            File.Exists(Path.Combine(
                Path.GetDirectoryName(Path.Combine(repoRoot, projectPath)) ?? "",
                "obj",
                "project.assets.json")));
        if (allAssetsExist)
            return;

        var result = await DotnetProcess.RunAsync(
                repoRoot,
                [
                    "restore",
                    solutionFileName,
                    "--nologo",
                    "--verbosity:quiet",
                    "-maxcpucount:1",
                    "-nodeReuse:false",
                ],
                timeoutMilliseconds: 300_000,
                cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "closed hierarchy検査用のsolution restoreに失敗した。\n" +
                result.StandardOutput + "\n" + result.StandardError);
        }
    }

    /// <summary>閉包検査対象 solution の build</summary>
    /// <param name="repoRoot">repository root の絶対パス</param>
    /// <param name="solutionFileName">solution ファイル名</param>
    /// <param name="configuration">build configuration</param>
    /// <param name="outputRoot">隔離した build output root</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>solution build の非同期完了</returns>
    internal static async Task BuildAsync(
        string repoRoot,
        string solutionFileName,
        string configuration,
        string outputRoot,
        CancellationToken cancellationToken)
    {
        var result = await DotnetProcess.RunAsync(
                repoRoot,
                [
                    "build",
                    solutionFileName,
                    "-c",
                    configuration,
                    "--no-restore",
                    "--nologo",
                    "--verbosity:quiet",
                    "-maxcpucount:1",
                    "-nodeReuse:false",
                    "-property:BaseOutputPath=" + outputRoot + Path.DirectorySeparatorChar,
                ],
                timeoutMilliseconds: 300_000,
                cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "closed hierarchy検査用のsolution buildに失敗した。\n" +
                result.StandardOutput + "\n" + result.StandardError);
        }
    }
}
