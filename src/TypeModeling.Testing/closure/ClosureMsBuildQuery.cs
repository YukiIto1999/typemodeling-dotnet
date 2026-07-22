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
    internal static async Task<JsonDocument> EvaluateAsync(
        string repoRoot,
        string projectPath,
        string configuration,
        string outputRoot,
        CancellationToken cancellationToken)
    {
        var result = await DotnetProcess.RunAsync(
                repoRoot,
                [
                    "msbuild",
                    projectPath,
                    "-nologo",
                    "-verbosity:quiet",
                    "-property:Configuration=" + configuration,
                    "-property:BaseOutputPath=" + outputRoot + Path.DirectorySeparatorChar,
                    "-maxcpucount:1",
                    "-nodeReuse:false",
                    "-getProperty:TargetPath",
                    "-getProperty:AssemblyName",
                    "-getItem:ProjectReference",
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

    /// <summary>MSBuild 評価 property の取得</summary>
    /// <param name="evaluation">MSBuild 評価 JSON</param>
    /// <param name="propertyName">property 名</param>
    /// <returns>property 値</returns>
    internal static string Property(JsonDocument evaluation, string propertyName) =>
        MsBuildJson.Property(evaluation, propertyName);

    /// <summary>評価済み ProjectReference の取得</summary>
    /// <param name="evaluation">MSBuild 評価 JSON</param>
    /// <returns>ProjectReference の絶対パス</returns>
    internal static IEnumerable<string> ProjectReferences(JsonDocument evaluation) =>
        MsBuildJson.Items(evaluation, "ProjectReference")
            .Select(item => MsBuildJson.ItemValue(item, "FullPath"))
            .Where(path => path.Length > 0);
}
