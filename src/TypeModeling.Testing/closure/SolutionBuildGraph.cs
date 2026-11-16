using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TypeModeling.Testing.Closure;

/// <summary>solution の authored project graph</summary>
internal static partial class SolutionBuildGraph
{
    /// <summary>authored ProjectReference による静的 project 閉包</summary>
    /// <remarks>
    /// csproj に書かれた ProjectReference だけをたどり、item の評価では props から足された参照を拾わない。
    /// Include に書いた <c>$(Name)</c> は、その project を MSBuild が評価した property の値で置き換える。
    /// 値が空の property と、property 関数のように置き換えられない記述は、閉包が欠けるので拒否する。
    /// </remarks>
    /// <param name="repoRoot">solution root の絶対パス</param>
    /// <param name="solutionFileName">solution ファイル名</param>
    /// <param name="configuration">property の評価に使う build configuration</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>solution root からの project 相対パスの非同期取得</returns>
    internal static async Task<string[]> DeclaredProjectPathsAsync(
        string repoRoot,
        string solutionFileName,
        string configuration,
        CancellationToken cancellationToken)
    {
        var queue = new Queue<string>(SolutionProjectPaths(repoRoot, solutionFileName));
        var projectPaths = new HashSet<string>(StringComparer.Ordinal);

        while (queue.TryDequeue(out var projectPath))
        {
            projectPath = NormalizeProjectPath(repoRoot, projectPath);
            if (!projectPaths.Add(projectPath))
                continue;

            var projectDirectory = Path.GetDirectoryName(projectPath) ?? "";
            var resolvedReferences = await ResolveReferencesAsync(
                    repoRoot,
                    projectPath,
                    configuration,
                    cancellationToken)
                .ConfigureAwait(false);
            foreach (var resolvedReference in resolvedReferences)
                queue.Enqueue(Path.Combine(projectDirectory, resolvedReference));
        }

        return projectPaths.Order(StringComparer.Ordinal).ToArray();
    }

    /// <summary>csproj に書かれた ProjectReference の、property を解決した Include の列挙</summary>
    /// <param name="repoRoot">solution root の絶対パス</param>
    /// <param name="projectPath">solution root からの project 相対パス</param>
    /// <param name="configuration">property の評価に使う build configuration</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>参照先の、参照元 directory からの相対パスか絶対パスの非同期取得</returns>
    private static async Task<IReadOnlyList<string>> ResolveReferencesAsync(
        string repoRoot,
        string projectPath,
        string configuration,
        CancellationToken cancellationToken)
    {
        var declaredReferences = XDocument.Load(Path.Combine(repoRoot, projectPath))
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference"
                && element.Attribute("OutputItemType")?.Value != "Analyzer")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Cast<string>()
            .ToArray();
        var propertyNames = declaredReferences
            .SelectMany(reference => PropertyToken().Matches(reference))
            .Select(token => token.Groups["name"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (propertyNames.Length == 0)
        {
            return declaredReferences
                .Select(reference => Resolve(projectPath, reference, EmptyProperties))
                .ToArray();
        }

        var properties = await ClosureMsBuildQuery.PropertiesAsync(
                repoRoot,
                projectPath,
                configuration,
                propertyNames,
                cancellationToken)
            .ConfigureAwait(false);
        return declaredReferences
            .Select(reference => Resolve(projectPath, reference, properties))
            .ToArray();
    }

    /// <summary>Include の property の置き換え</summary>
    /// <param name="projectPath">solution root からの project 相対パス</param>
    /// <param name="reference">csproj に書かれた Include</param>
    /// <param name="properties">評価した property の値</param>
    /// <returns>property を置き換えた Include</returns>
    /// <exception cref="InvalidOperationException">空の property か置き換えられない記述を含む Include</exception>
    private static string Resolve(
        string projectPath,
        string reference,
        IReadOnlyDictionary<string, string> properties)
    {
        var resolved = PropertyToken().Replace(
            reference,
            token => properties[token.Groups["name"].Value]);
        var hasEmptyProperty = PropertyToken().Matches(reference)
            .Any(token => properties[token.Groups["name"].Value].Length == 0);
        if (hasEmptyProperty || resolved.Contains("$(", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{projectPath}: inventoryが静的に解決できないProjectReference: " +
                reference);
        }

        return resolved;
    }

    /// <summary>property を使わない project の評価済み property</summary>
    private static readonly IReadOnlyDictionary<string, string> EmptyProperties =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>MSBuild の <c>$(Name)</c> の形の property 参照</summary>
    /// <returns>property 名を name の group に取る正規表現</returns>
    [GeneratedRegex(@"\$\((?<name>[A-Za-z_][A-Za-z0-9_\-]*)\)")]
    private static partial Regex PropertyToken();

    /// <summary>solution からの managed project 列挙</summary>
    /// <param name="repoRoot">solution root の絶対パス</param>
    /// <param name="solutionFileName">solution ファイル名</param>
    /// <returns>solution が宣言する project パス</returns>
    internal static string[] SolutionProjectPaths(
        string repoRoot,
        string solutionFileName)
    {
        var projectPaths = XDocument.Load(Path.Combine(repoRoot, solutionFileName))
            .Descendants()
            .Where(element => element.Name.LocalName == "Project")
            .Select(project => project.Attribute("Path")?.Value.Replace('\\', '/'))
            .Where(path => path?.EndsWith(".csproj", StringComparison.Ordinal) == true)
            .Cast<string>()
            .ToArray();
        if (projectPaths.Length == 0)
        {
            throw new InvalidOperationException(
                $"{solutionFileName} にmanaged .csprojがない");
        }

        return projectPaths;
    }

    /// <summary>solution root 相対 project path への正規化</summary>
    /// <param name="repoRoot">solution root の絶対パス</param>
    /// <param name="projectPath">正規化対象の project パス</param>
    /// <returns>solution root からの project 相対パス</returns>
    internal static string NormalizeProjectPath(string repoRoot, string projectPath)
    {
        var fullPath = Path.GetFullPath(Path.IsPathRooted(projectPath)
            ? projectPath
            : Path.Combine(repoRoot, projectPath));
        return Path.GetRelativePath(repoRoot, fullPath).Replace('\\', '/');
    }
}
