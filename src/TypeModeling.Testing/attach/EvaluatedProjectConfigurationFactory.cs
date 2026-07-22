using Microsoft.CodeAnalysis.CSharp;

namespace TypeModeling.Testing.Attach;

/// <summary>外部 MSBuild 評価による project 構成生成</summary>
internal static class EvaluatedProjectConfigurationFactory
{
    /// <summary>MSBuild 評価による単一 build 構成生成</summary>
    /// <param name="repoRoot">repository root</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="configuration">Configuration 値</param>
    /// <param name="platform">Platform 値</param>
    /// <param name="targetFramework">TargetFramework 値</param>
    /// <param name="runtimeIdentifier">RuntimeIdentifier 値</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>評価済み project 構成の非同期取得</returns>
    internal static async Task<EvaluatedProjectConfiguration> CreateAsync(
        string repoRoot,
        string projectPath,
        string configuration,
        string platform,
        string targetFramework,
        string runtimeIdentifier,
        CancellationToken cancellationToken)
    {
        var arguments = new List<string>
        {
            "-property:Configuration=" + configuration,
            "-property:Platform=" + platform,
        };
        if (targetFramework.Length > 0)
            arguments.Add("-property:TargetFramework=" + targetFramework);
        if (runtimeIdentifier.Length > 0)
            arguments.Add("-property:RuntimeIdentifier=" + runtimeIdentifier);
        arguments.AddRange(
        [
            "-getProperty:NoWarn",
            "-getProperty:RunAnalyzers",
            "-getProperty:RunAnalyzersDuringBuild",
            "-getProperty:DefineConstants",
            "-getProperty:LangVersion",
            "-getItem:ProjectReference",
            "-getItem:Compile",
            "-getItem:EditorConfigFiles",
        ]);
        using var evaluation = await MsBuildQuery.RunAsync(
                repoRoot,
                projectPath,
                [.. arguments],
                cancellationToken)
            .ConfigureAwait(false);
        var parseOptions = new CSharpParseOptions(
            ParseLanguageVersion(MsBuildQuery.Property(evaluation, "LangVersion")),
            preprocessorSymbols: MsBuildValueSplitter.SplitList(
                MsBuildQuery.Property(evaluation, "DefineConstants")));
        return new EvaluatedProjectConfiguration(
            configuration,
            platform,
            targetFramework,
            runtimeIdentifier,
            EvaluationLabel(configuration, platform, targetFramework, runtimeIdentifier),
            MsBuildQuery.Property(evaluation, "NoWarn"),
            MsBuildQuery.Property(evaluation, "RunAnalyzers"),
            MsBuildQuery.Property(evaluation, "RunAnalyzersDuringBuild"),
            MsBuildQuery.Items(evaluation, "ProjectReference")
                .Select(item => new EvaluatedProjectReference(
                    Path.GetRelativePath(repoRoot, MsBuildQuery.ItemPath(item, projectPath))
                        .Replace('\\', '/'),
                    MsBuildQuery.ItemPath(item, projectPath),
                    MsBuildQuery.ItemValue(item, "OutputItemType"),
                    MsBuildQuery.ItemValue(item, "ReferenceOutputAssembly")))
                .ToArray(),
            MsBuildQuery.Items(evaluation, "Compile")
                .Select(item => MsBuildQuery.ItemPath(item, projectPath))
                .Where(File.Exists)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            MsBuildQuery.Items(evaluation, "EditorConfigFiles")
                .Select(item => MsBuildQuery.ItemPath(item, projectPath))
                .Where(File.Exists)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            parseOptions);
    }

    /// <summary>build 構成の表示名生成</summary>
    /// <param name="configuration">Configuration 値</param>
    /// <param name="platform">Platform 値</param>
    /// <param name="targetFramework">TargetFramework 値</param>
    /// <param name="runtimeIdentifier">RuntimeIdentifier 値</param>
    /// <returns>build 構成の表示名</returns>
    private static string EvaluationLabel(
        string configuration,
        string platform,
        string targetFramework,
        string runtimeIdentifier)
    {
        var dimensions = new List<string>
        {
            "Configuration=" + configuration,
            "Platform=" + platform,
        };
        if (targetFramework.Length > 0)
            dimensions.Add("TargetFramework=" + targetFramework);
        if (runtimeIdentifier.Length > 0)
            dimensions.Add("RuntimeIdentifier=" + runtimeIdentifier);
        return string.Join(", ", dimensions);
    }

    /// <summary>C# 言語 version の解析</summary>
    /// <param name="value">LangVersion 値</param>
    /// <returns>C# 言語 version</returns>
    private static LanguageVersion ParseLanguageVersion(string value) =>
        LanguageVersionFacts.TryParse(value, out var version)
            ? version
            : LanguageVersion.Latest;
}
