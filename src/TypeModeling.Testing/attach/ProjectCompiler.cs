using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text.Json;

namespace TypeModeling.Testing.Attach;

/// <summary>MSBuild 評価済み project の semantic compiler</summary>
internal static class ProjectCompiler
{
    /// <summary>評価済み build 構成の C# compilation 構築</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="generatedDirectory">compiler generated source の出力先</param>
    /// <param name="configuration">評価済み build 構成</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>semantic C# compilation の非同期取得</returns>
    internal static async Task<CSharpCompilation> CompileAsync(
        string repoRoot,
        string projectPath,
        string generatedDirectory,
        EvaluatedProjectConfiguration configuration,
        CancellationToken cancellationToken)
    {
        await MsBuildQuery.BuildAsync(
                repoRoot,
                projectPath,
                generatedDirectory,
                configuration,
                cancellationToken)
            .ConfigureAwait(false);
        using var evaluation = await MsBuildQuery.RunAsync(
                repoRoot,
                projectPath,
                EvaluationArguments(configuration),
                cancellationToken)
            .ConfigureAwait(false);
        var parseOptions = new CSharpParseOptions(
            ParseLanguageVersion(MsBuildQuery.Property(evaluation, "LangVersion")),
            preprocessorSymbols: MsBuildValueSplitter.SplitList(
                MsBuildQuery.Property(evaluation, "DefineConstants")));
        var sourcePaths = MsBuildQuery.Items(evaluation, "Compile")
            .Select(item => MsBuildQuery.ItemPath(item, projectPath))
            .Concat(Directory.EnumerateFiles(
                generatedDirectory,
                "*.cs",
                SearchOption.AllDirectories))
            .Where(File.Exists)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var syntaxTrees = sourcePaths.Select(path =>
            CSharpSyntaxTree.ParseText(
                File.ReadAllText(path),
                parseOptions,
                path,
                cancellationToken: cancellationToken));
        var compilation = CSharpCompilation.Create(
            MsBuildQuery.Property(evaluation, "AssemblyName"),
            syntaxTrees,
            MetadataReferences(evaluation, projectPath),
            new CSharpCompilationOptions(
                ParseOutputKind(MsBuildQuery.Property(evaluation, "OutputType")),
                allowUnsafe: bool.TryParse(
                    MsBuildQuery.Property(evaluation, "AllowUnsafeBlocks"),
                    out var allowUnsafe) && allowUnsafe,
                nullableContextOptions: NullableOptions(
                    MsBuildQuery.Property(evaluation, "Nullable"))));
        ThrowIfInvalid(compilation, projectPath, cancellationToken);
        return compilation;
    }

    /// <summary>semantic compilation に必要な参照 assembly の収集</summary>
    /// <param name="evaluation">MSBuild 評価結果</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <returns>解決済み metadata reference 集合</returns>
    private static MetadataReference[] MetadataReferences(
        JsonDocument evaluation,
        string projectPath)
    {
        var referencePaths = MsBuildQuery.Items(evaluation, "ReferencePath")
            .Select(item => MsBuildQuery.ReferencePath(item, projectPath))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var missingReferencePaths = referencePaths
            .Where(path => !File.Exists(path))
            .ToArray();
        if (missingReferencePaths.Length > 0)
        {
            throw new InvalidOperationException(
                projectPath + " の ReferencePath に存在しない成果物がある:\n" +
                string.Join("\n", missingReferencePaths));
        }

        var directReferencePaths = MsBuildQuery.Items(evaluation, "Reference")
            .Select(item => MsBuildQuery.ItemPath(item, projectPath))
            .Where(File.Exists);
        return referencePaths
            .Concat(directReferencePaths)
            .Distinct(StringComparer.Ordinal)
            .Select(path => MetadataReference.CreateFromFile(path))
            .ToArray();
    }

    /// <summary>semantic compilation error の早期報告</summary>
    /// <param name="compilation">検査対象の semantic compilation</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="cancellationToken">処理中止 token</param>
    private static void ThrowIfInvalid(
        Compilation compilation,
        string projectPath,
        CancellationToken cancellationToken)
    {
        var errors = compilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error &&
                diagnostic.Id != "CS5001")
            .Take(10)
            .ToArray();
        if (errors.Length > 0)
        {
            throw new InvalidOperationException(
                projectPath + " の MSBuild 評価済み semantic compilation に error がある:\n" +
                string.Join("\n", errors.Select(error => error.ToString())));
        }
    }

    /// <summary>MSBuild 評価引数の構築</summary>
    /// <param name="configuration">評価済み build 構成</param>
    /// <returns>MSBuild 評価引数</returns>
    private static string[] EvaluationArguments(EvaluatedProjectConfiguration configuration)
    {
        var arguments = new List<string>
        {
            "-target:ResolveReferences",
            "-property:BuildProjectReferences=false",
            "-property:DesignTimeBuild=true",
            "-property:_GlobalPropertiesToRemoveFromProjectReferences=" +
                "TargetFramework%3BRuntimeIdentifier",
            "-getProperty:DefineConstants",
            "-getProperty:LangVersion",
            "-getProperty:Nullable",
            "-getProperty:AllowUnsafeBlocks",
            "-getProperty:OutputType",
            "-getProperty:AssemblyName",
            "-getItem:Compile",
            "-getItem:Reference",
            "-getItem:ReferencePath",
            "-property:Configuration=" + configuration.Configuration,
            "-property:Platform=" + configuration.Platform,
        };
        if (configuration.TargetFramework.Length > 0)
            arguments.Add("-property:TargetFramework=" + configuration.TargetFramework);
        if (configuration.RuntimeIdentifier.Length > 0)
            arguments.Add("-property:RuntimeIdentifier=" + configuration.RuntimeIdentifier);
        return [.. arguments];
    }

    /// <summary>MSBuild 設定値から Roslyn 言語 version への変換</summary>
    /// <param name="value">MSBuild の LangVersion 値</param>
    /// <returns>Roslyn の言語 version</returns>
    private static LanguageVersion ParseLanguageVersion(string value) =>
        LanguageVersionFacts.TryParse(value, out var version)
            ? version
            : LanguageVersion.Latest;

    /// <summary>MSBuild 設定値から Roslyn 出力種別への変換</summary>
    /// <param name="value">MSBuild の OutputType 値</param>
    /// <returns>Roslyn の出力種別</returns>
    private static OutputKind ParseOutputKind(string value) =>
        value is "Exe" or "WinExe"
            ? OutputKind.ConsoleApplication
            : OutputKind.DynamicallyLinkedLibrary;

    /// <summary>MSBuild 設定値から Roslyn null 許容範囲への変換</summary>
    /// <param name="value">MSBuild の Nullable 値</param>
    /// <returns>Roslyn の null 許容範囲</returns>
    private static NullableContextOptions NullableOptions(string value) =>
        value.ToLowerInvariant() switch
        {
            "enable" => NullableContextOptions.Enable,
            "warnings" => NullableContextOptions.Warnings,
            "annotations" => NullableContextOptions.Annotations,
            _ => NullableContextOptions.Disable,
        };
}
