using System.Text.Json;
using TypeModeling.Testing.MsBuild;

namespace TypeModeling.Testing.Closure;

/// <summary>managed project の MSBuild JSON query</summary>
internal static class ClosureMsBuildQuery
{
    /// <summary>managed project の MSBuild 評価</summary>
    /// <param name="repoRoot">repository root の絶対パス</param>
    /// <param name="projectPath">repository root からの project 相対パス</param>
    /// <param name="configuration">build configuration</param>
    /// <param name="outputRoot">隔離した build output root</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>MSBuild 評価 JSON の非同期取得</returns>
    internal static Task<JsonDocument> EvaluateAsync(
        string repoRoot,
        string projectPath,
        string configuration,
        string outputRoot,
        CancellationToken cancellationToken) =>
        QueryAsync(
            repoRoot,
            projectPath,
            [
                "-property:Configuration=" + configuration,
                "-property:BaseOutputPath=" + outputRoot + Path.DirectorySeparatorChar,
                "-getProperty:TargetPath",
                "-getProperty:AssemblyName",
                "-getItem:ProjectReference",
            ],
            cancellationToken);

    /// <summary>project の MSBuild 評価による property の値の取得</summary>
    /// <param name="repoRoot">repository root の絶対パス</param>
    /// <param name="projectPath">repository root からの project 相対パス</param>
    /// <param name="configuration">build configuration</param>
    /// <param name="propertyNames">値を求める property 名</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>property 名と値の対の非同期取得。未定義の property は空の値</returns>
    internal static async Task<IReadOnlyDictionary<string, string>> PropertiesAsync(
        string repoRoot,
        string projectPath,
        string configuration,
        IReadOnlyList<string> propertyNames,
        CancellationToken cancellationToken)
    {
        // property を 1 つだけ問うと、MSBuild は JSON でなく値だけを出力する。project 名を足して JSON に揃える
        using var evaluation = await QueryAsync(
                repoRoot,
                projectPath,
                [
                    "-property:Configuration=" + configuration,
                    "-getProperty:MSBuildProjectName",
                    .. propertyNames.Select(name => "-getProperty:" + name),
                ],
                cancellationToken)
            .ConfigureAwait(false);
        return propertyNames.ToDictionary(
            name => name,
            name => Property(evaluation, name),
            StringComparer.Ordinal);
    }

    /// <summary>MSBuild 評価 property の取得</summary>
    /// <param name="evaluation">MSBuild 評価 JSON</param>
    /// <param name="propertyName">property 名</param>
    /// <returns>property 値</returns>
    internal static string Property(JsonDocument evaluation, string propertyName) =>
        MsBuildJson.Property(evaluation, propertyName);

    /// <summary>評価済み ProjectReference の取得。analyzer だけを届ける参照は runtime の閉包に入れない</summary>
    /// <param name="evaluation">MSBuild 評価 JSON</param>
    /// <returns>ProjectReference の絶対パス</returns>
    internal static IEnumerable<string> ProjectReferences(JsonDocument evaluation) =>
        MsBuildJson.Items(evaluation, "ProjectReference")
            .Where(item => MsBuildJson.ItemValue(item, "OutputItemType") != "Analyzer")
            .Select(item => MsBuildJson.ItemValue(item, "FullPath"))
            .Where(path => path.Length > 0);

    /// <summary>project に対する MSBuild の query の実行</summary>
    /// <param name="repoRoot">repository root の絶対パス</param>
    /// <param name="projectPath">repository root からの project 相対パス</param>
    /// <param name="queryArguments">評価の条件と、問う property や item の引数</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>MSBuild 評価 JSON の非同期取得</returns>
    private static async Task<JsonDocument> QueryAsync(
        string repoRoot,
        string projectPath,
        IReadOnlyList<string> queryArguments,
        CancellationToken cancellationToken)
    {
        var result = await DotnetProcess.RunAsync(
                repoRoot,
                [
                    "msbuild",
                    projectPath,
                    "-nologo",
                    "-verbosity:quiet",
                    "-maxcpucount:1",
                    "-nodeReuse:false",
                    .. queryArguments,
                ],
                timeoutMilliseconds: 60_000,
                cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{projectPath}のMSBuild評価に失敗した。\n" +
                result.StandardOutput + "\n" + result.StandardError);
        }

        if (!MsBuildJson.TryParse(result.StandardOutput, out var evaluation))
        {
            throw new InvalidOperationException(
                $"{projectPath}のMSBuild評価結果にJSONがない。\n{result.StandardOutput}");
        }

        return evaluation;
    }
}
