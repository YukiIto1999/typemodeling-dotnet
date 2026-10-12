using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace TypeModeling.Testing.Attach;

/// <summary>project の入力が同じなら検出の結果を使い回す控え</summary>
/// <remarks>
/// 検出は project の原文と参照先と build の定義だけで決まるので、その全てが同じなら compile を重ねない。
/// build の定義は版が持つ build の綴りの全てをまとめて一つの鍵にする。
/// どの綴りがどの project へ効くかを個別に追うと、追い損ねた綴りが古い結果を通す。
/// 控えは検出器ごとに分け、検出器と機構の組み上がりが変われば鍵が変わる
/// </remarks>
internal sealed class AttachVerdictCache
{
    /// <summary>build の定義を成す綴りの拡張子</summary>
    private static readonly string[] DefinitionExtensions =
        [".csproj", ".props", ".targets", ".globalconfig", ".editorconfig", ".ruleset"];

    /// <summary>build の定義を成す綴りの名</summary>
    private static readonly string[] DefinitionNames =
        ["packages.lock.json", "global.json", "nuget.config", "Directory.Build.rsp"];

    /// <summary>走査しない directory の名</summary>
    private static readonly string[] SkippedDirectories =
        ["bin", "obj", ".git", ".devenv", "node_modules", "StrykerOutput"];

    /// <summary>控えを置く directory</summary>
    private readonly string directory;

    /// <summary>build の定義の全てから導いた鍵</summary>
    private readonly string definitionKey;

    /// <summary>project の相対 path ごとの鍵</summary>
    private readonly ConcurrentDictionary<string, string> projectKeys = new(StringComparer.Ordinal);

    /// <summary>原文の内容の鍵</summary>
    private readonly ConcurrentDictionary<string, string> contentKeys = new(StringComparer.Ordinal);

    /// <summary>控えの構築</summary>
    /// <param name="directory">控えを置く directory</param>
    /// <param name="definitionKey">build の定義の全てから導いた鍵</param>
    private AttachVerdictCache(string directory, string definitionKey)
    {
        this.directory = directory;
        this.definitionKey = definitionKey;
        Directory.CreateDirectory(directory);
    }

    /// <summary>repository の build の定義から鍵を導いた控えの構築</summary>
    /// <param name="repoRoot">repository root の絶対 path</param>
    /// <param name="directory">控えを置く directory</param>
    /// <returns>この repository の控え</returns>
    internal static AttachVerdictCache Create(string repoRoot, string directory)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in DefinitionFiles(repoRoot).Order(StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(repoRoot, path) + "\0"));
            hash.AppendData(File.ReadAllBytes(path));
        }

        hash.AppendData(Encoding.UTF8.GetBytes(typeof(AttachVerdictCache).Module.ModuleVersionId + "\0"));
        hash.AppendData(
            Encoding.UTF8.GetBytes(
                System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
                    + typeof(Microsoft.CodeAnalysis.CSharp.CSharpCompilation).Module.ModuleVersionId
                    + "\0"));

        return new AttachVerdictCache(directory, Convert.ToHexString(hash.GetHashAndReset()));
    }

    /// <summary>その project の鍵</summary>
    /// <param name="project">project の相対 path</param>
    /// <param name="graph">評価済み project graph</param>
    /// <returns>build の定義と原文と参照先から導いた鍵</returns>
    internal string KeyOf(string project, AnalyzerAttachmentProjectGraph graph) =>
        KeyOf(project, graph, new HashSet<string>(graph.PathComparer));

    /// <summary>控えた検出の結果の取得</summary>
    /// <param name="key">project の鍵</param>
    /// <param name="detector">検出器</param>
    /// <returns>控えた結果で、控えが無ければ null</returns>
    internal bool? Read(string key, IGuardedFeatureDetector detector)
    {
        var path = EntryPath(key, detector);
        return File.Exists(path) ? File.ReadAllText(path) == "1" : null;
    }

    /// <summary>検出の結果の控え</summary>
    /// <param name="key">project の鍵</param>
    /// <param name="detector">検出器</param>
    /// <param name="uses">検出した結果</param>
    internal void Write(string key, IGuardedFeatureDetector detector, bool uses)
    {
        var path = EntryPath(key, detector);
        var pending = path + "." + Guid.NewGuid().ToString("N");
        File.WriteAllText(pending, uses ? "1" : "0");
        File.Move(pending, path, overwrite: true);
    }

    /// <summary>控えの一件の置き場</summary>
    /// <param name="key">project の鍵</param>
    /// <param name="detector">検出器</param>
    /// <returns>検出器と project の鍵で決まる置き場</returns>
    private string EntryPath(string key, IGuardedFeatureDetector detector)
    {
        var type = detector.GetType();
        var identity = type.FullName + "@" + type.Module.ModuleVersionId;
        var tag = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16];
        return Path.Combine(directory, tag + "-" + key + ".verdict");
    }

    /// <summary>その project の鍵の導出</summary>
    /// <param name="project">project の相対 path</param>
    /// <param name="graph">評価済み project graph</param>
    /// <param name="walking">いま辿っている project で、参照の輪を断つ</param>
    /// <returns>build の定義と原文と参照先から導いた鍵</returns>
    private string KeyOf(
        string project,
        AnalyzerAttachmentProjectGraph graph,
        HashSet<string> walking)
    {
        if (projectKeys.TryGetValue(project, out var known))
            return known;
        if (!walking.Add(project) || !graph.Evaluations.TryGetValue(project, out var evaluations))
            return "unresolved:" + project;
        var text = new StringBuilder(definitionKey).Append('\n').Append(project).Append('\n');
        foreach (var evaluation in evaluations.OrderBy(held => held.Label, StringComparer.Ordinal))
        {
            text.Append(evaluation.Label)
                .Append('|')
                .Append(evaluation.ParseOptions.LanguageVersion)
                .Append('|')
                .AppendJoin(',', evaluation.ParseOptions.PreprocessorSymbolNames.Order(StringComparer.Ordinal))
                .Append('\n');
            foreach (var path in evaluation.CompilePaths.Concat(evaluation.EditorConfigPaths)
                         .Distinct(StringComparer.Ordinal)
                         .Order(StringComparer.Ordinal))
                text.Append(path).Append('=').Append(ContentKey(path)).Append('\n');
            foreach (var reference in evaluation.ProjectReferences
                         .Select(held => held.RelativePath)
                         .Distinct(StringComparer.Ordinal)
                         .Order(StringComparer.Ordinal))
                text.Append("ref ").Append(KeyOf(reference, graph, walking)).Append('\n');
        }

        walking.Remove(project);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
        projectKeys[project] = key;
        return key;
    }

    /// <summary>原文の内容の鍵</summary>
    /// <param name="path">原文の絶対 path</param>
    /// <returns>内容の鍵で、原文が無ければ absent</returns>
    private string ContentKey(string path) =>
        contentKeys.GetOrAdd(
            path,
            held => File.Exists(held)
                ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(held)))
                : "absent");

    /// <summary>build の定義を成す綴りの全て</summary>
    /// <param name="repoRoot">repository root の絶対 path</param>
    /// <returns>走査しない directory を除いた定義の綴りの絶対 path</returns>
    private static IEnumerable<string> DefinitionFiles(string repoRoot)
    {
        var pending = new Stack<string>([repoRoot]);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var child in Directory.EnumerateDirectories(current)
                         .Where(held => !SkippedDirectories.Contains(
                             Path.GetFileName(held),
                             StringComparer.Ordinal)))
                pending.Push(child);
            foreach (var file in Directory.EnumerateFiles(current).Where(IsDefinition))
                yield return file;
        }
    }

    /// <summary>build の定義を成す綴りかの判定</summary>
    /// <param name="path">綴りの絶対 path</param>
    /// <returns>build の定義なら真</returns>
    private static bool IsDefinition(string path)
    {
        var name = Path.GetFileName(path);
        return DefinitionNames.Contains(name, StringComparer.OrdinalIgnoreCase)
            || DefinitionExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);
    }
}
