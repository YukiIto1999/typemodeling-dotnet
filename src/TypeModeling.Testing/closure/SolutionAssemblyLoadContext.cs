using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

namespace TypeModeling.Testing.Closure;

/// <summary>solution 閉包の出力だけを優先する assembly load context</summary>
internal sealed class SolutionAssemblyLoadContext : AssemblyLoadContext
{
    /// <summary>project assembly 名から出力 path への対応</summary>
    private readonly Dictionary<string, string> _projectTargetPaths;

    /// <summary>依存 assembly を探索する出力 directory 集合</summary>
    private readonly IReadOnlyList<string> _probeDirectories;

    /// <summary>評価済み project 出力に対応する load context の構築</summary>
    /// <param name="projects">MSBuild 評価済みの managed project 出力</param>
    internal SolutionAssemblyLoadContext(IReadOnlyList<EvaluatedSolutionProject> projects)
        : base("type-modeling-solution-closure", isCollectible: false)
    {
        _projectTargetPaths = projects.ToDictionary(
            project => project.AssemblyName,
            project => Path.GetFullPath(project.TargetPath),
            StringComparer.OrdinalIgnoreCase);
        _probeDirectories = projects
            .Select(project => Path.GetDirectoryName(project.TargetPath))
            .OfType<string>()
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>出力ファイルの同一性を前後照合する project assembly のロード</summary>
    /// <param name="targetPath">MSBuild が評価した出力 assembly の絶対パス</param>
    /// <returns>ロードした assembly と照合済み MVID</returns>
    internal (Assembly Assembly, Guid TargetMvid) LoadProject(string targetPath)
    {
        var fullPath = Path.GetFullPath(targetPath);
        var mvidBeforeLoad = ReadModuleVersionId(fullPath);
        var assembly = LoadFromAssemblyPath(fullPath);
        var mvidAfterLoad = ReadModuleVersionId(fullPath);
        if (mvidBeforeLoad != mvidAfterLoad
            || assembly.ManifestModule.ModuleVersionId != mvidAfterLoad)
        {
            throw new InvalidOperationException(
                $"TargetPathがassembly load中に更新された: {fullPath} " +
                $"(before={mvidBeforeLoad}, loaded={assembly.ManifestModule.ModuleVersionId}, " +
                $"after={mvidAfterLoad})");
        }

        return (assembly, mvidAfterLoad);
    }

    /// <summary>project 出力と同じ出力 directory にある依存 assembly の解決</summary>
    /// <param name="assemblyName">解決する assembly identity</param>
    /// <returns>一致した依存 assembly、見つからない場合は null</returns>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (assemblyName.Name is null)
            return null;
        if (_projectTargetPaths.TryGetValue(assemblyName.Name, out var projectTargetPath)
            && Matches(projectTargetPath, assemblyName))
        {
            return LoadFromAssemblyPath(projectTargetPath);
        }

        var dependencyPath = _probeDirectories
            .Select(directory => Path.Combine(directory, assemblyName.Name + ".dll"))
            .Where(File.Exists)
            .FirstOrDefault(path => Matches(path, assemblyName));
        return dependencyPath is null ? null : LoadFromAssemblyPath(dependencyPath);
    }

    /// <summary>出力 assembly の MVID 読み取り</summary>
    /// <param name="path">assembly の絶対 path</param>
    /// <returns>module version ID</returns>
    private static Guid ReadModuleVersionId(string path)
    {
        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();
        return metadata.GetGuid(metadata.GetModuleDefinition().Mvid);
    }

    /// <summary>要求された assembly identity との照合</summary>
    /// <param name="path">候補 assembly の絶対 path</param>
    /// <param name="requested">要求された assembly identity</param>
    /// <returns>一致する場合に true</returns>
    private static bool Matches(string path, AssemblyName requested) =>
        AssemblyName.ReferenceMatchesDefinition(AssemblyName.GetAssemblyName(path), requested);
}
