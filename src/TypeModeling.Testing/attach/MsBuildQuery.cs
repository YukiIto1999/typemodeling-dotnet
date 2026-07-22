using System.Text.Json;
using TypeModeling.Testing.MsBuild;

namespace TypeModeling.Testing.Attach;

/// <summary>dotnet MSBuild の評価窓口</summary>
internal static class MsBuildQuery
{
    /// <summary>project の MSBuild JSON 評価</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="arguments">追加 MSBuild 引数</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>MSBuild JSON 評価結果の非同期取得</returns>
    internal static async Task<JsonDocument> RunAsync(
        string repoRoot,
        string projectPath,
        IReadOnlyList<string> arguments,
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
                    .. arguments,
                ],
                timeoutMilliseconds: 300_000,
                cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                projectPath + " の MSBuild 評価に失敗した。\n" +
                result.StandardOutput + "\n" + result.StandardError);
        }

        if (!MsBuildJson.TryParse(result.StandardOutput, out var evaluation))
        {
            throw new InvalidOperationException(
                projectPath + " の MSBuild 評価結果に JSON がない。\n" +
                result.StandardOutput);
        }

        return evaluation;
    }

    /// <summary>project の restore 実行</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>project restore の非同期完了</returns>
    internal static async Task RestoreAsync(
        string repoRoot,
        string projectPath,
        CancellationToken cancellationToken)
    {
        var result = await DotnetProcess.RunAsync(
                repoRoot,
                [
                    "restore",
                    projectPath,
                    "--nologo",
                    "--verbosity:quiet",
                    "-property:BuildProjectReferences=false",
                    "-maxcpucount:1",
                    "-nodeReuse:false",
                ],
                timeoutMilliseconds: 300_000,
                cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                projectPath + " の restore に失敗した。\n" +
                result.StandardOutput + "\n" + result.StandardError);
        }
    }

    /// <summary>semantic compilation の参照成果物 build</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="generatedDirectory">compiler generated source の出力先</param>
    /// <param name="configuration">評価済み build 構成</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>参照成果物 build の非同期完了</returns>
    internal static async Task BuildAsync(
        string repoRoot,
        string projectPath,
        string generatedDirectory,
        EvaluatedProjectConfiguration configuration,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(generatedDirectory);
        var arguments = new List<string>
        {
            "build",
            projectPath,
            "--no-restore",
            "--nologo",
            "--verbosity:quiet",
            "-maxcpucount:1",
            "-nodeReuse:false",
            "-property:Configuration=" + configuration.Configuration,
            "-property:Platform=" + configuration.Platform,
            "-property:OutputPath=" + Path.Combine(
                generatedDirectory,
                "output"),
            "-property:EmitCompilerGeneratedFiles=true",
            "-property:CompilerGeneratedFilesOutputPath=" + generatedDirectory,
            "-property:NonExistentFile=" + Path.Combine(
                generatedDirectory,
                "force-compile"),
            "-property:_GlobalPropertiesToRemoveFromProjectReferences=" +
                "TargetFramework%3BRuntimeIdentifier%3B" +
                "OutputPath%3B" +
                "EmitCompilerGeneratedFiles%3BCompilerGeneratedFilesOutputPath%3B" +
                "NonExistentFile",
        };
        if (configuration.TargetFramework.Length > 0)
            arguments.Add("-property:TargetFramework=" + configuration.TargetFramework);
        if (configuration.RuntimeIdentifier.Length > 0)
            arguments.Add("-property:RuntimeIdentifier=" + configuration.RuntimeIdentifier);
        var result = await DotnetProcess.RunAsync(
                repoRoot,
                arguments,
                timeoutMilliseconds: 300_000,
                cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                projectPath + " の semantic compilation 用 build に失敗した。\n" +
                result.StandardOutput + "\n" + result.StandardError);
        }
    }

    /// <summary>MSBuild property 値の取得</summary>
    /// <param name="evaluation">MSBuild JSON 評価結果</param>
    /// <param name="propertyName">property 名</param>
    /// <returns>property 値</returns>
    internal static string Property(JsonDocument evaluation, string propertyName) =>
        MsBuildJson.Property(evaluation, propertyName);

    /// <summary>MSBuild item 集合の取得</summary>
    /// <param name="evaluation">MSBuild JSON 評価結果</param>
    /// <param name="itemName">item 名</param>
    /// <returns>item JSON 集合</returns>
    internal static IReadOnlyList<JsonElement> Items(
        JsonDocument evaluation,
        string itemName) =>
        MsBuildJson.Items(evaluation, itemName);

    /// <summary>MSBuild item metadata 値の取得</summary>
    /// <param name="item">item JSON</param>
    /// <param name="name">metadata 名</param>
    /// <returns>metadata 値</returns>
    internal static string ItemValue(JsonElement item, string name) =>
        MsBuildJson.ItemValue(item, name);

    /// <summary>MSBuild item の絶対 path 解決</summary>
    /// <param name="item">item JSON</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <returns>item の絶対 path</returns>
    internal static string ItemPath(JsonElement item, string projectPath)
    {
        var identity = ItemValue(item, "Identity");
        if (identity.Length > 0)
        {
            return Path.IsPathRooted(identity)
                ? identity
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(projectPath)!, identity));
        }

        return ItemValue(item, "FullPath");
    }

    /// <summary>compiler metadata reference path の解決</summary>
    /// <param name="item">ReferencePath item JSON</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <returns>metadata reference の絶対 path</returns>
    internal static string ReferencePath(JsonElement item, string projectPath)
    {
        var referenceAssembly = ItemValue(item, "ReferenceAssembly");
        return referenceAssembly.Length > 0 && File.Exists(referenceAssembly)
            ? referenceAssembly
            : ItemPath(item, projectPath);
    }
}
