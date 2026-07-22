using System.Reflection;

namespace TypeModeling.Testing.Closure;

/// <summary>solution project assembly の materializer</summary>
internal static class SolutionAssemblyLoader
{
    /// <summary>単一 load context への project assembly ロード</summary>
    /// <param name="projects">MSBuild 評価済みの managed project 出力</param>
    /// <returns>ロード済み managed project</returns>
    internal static LoadedSolutionProject[] LoadProjects(
        IReadOnlyList<EvaluatedSolutionProject> projects)
    {
        var loadContext = new SolutionAssemblyLoadContext(projects);
        return projects
            .Select(project => LoadProjectAssembly(project, loadContext))
            .ToArray();
    }

    /// <summary>ロード済み project からの CLR 型取得</summary>
    /// <param name="projects">ロード済み managed project</param>
    /// <returns>重複を除いた CLR 型</returns>
    internal static Type[] GetReflectionTypes(IReadOnlyList<LoadedSolutionProject> projects) =>
        projects
            .SelectMany(GetProjectTypes)
            .Distinct()
            .ToArray();

    /// <summary>project assembly のロード結果生成</summary>
    /// <param name="project">MSBuild 評価済みの managed project 出力</param>
    /// <param name="loadContext">solution 専用 load context</param>
    /// <returns>ロード済み managed project</returns>
    private static LoadedSolutionProject LoadProjectAssembly(
        EvaluatedSolutionProject project,
        SolutionAssemblyLoadContext loadContext)
    {
        var (assembly, targetMvid) = loadContext.LoadProject(project.TargetPath);
        return new LoadedSolutionProject(
            project.ProjectPath,
            project.AssemblyName,
            project.TargetPath,
            targetMvid,
            assembly);
    }

    /// <summary>project が宣言する CLR 型取得</summary>
    /// <param name="project">ロード済み managed project</param>
    /// <returns>project assembly が宣言する CLR 型</returns>
    private static IReadOnlyList<Type> GetProjectTypes(LoadedSolutionProject project)
    {
        try
        {
            return project.Assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            var loaderExceptions = string.Join(
                "\n  ",
                exception.LoaderExceptions
                    .Where(loaderException => loaderException is not null)
                    .Select(loaderException =>
                        $"{loaderException!.GetType().Name}: {loaderException.Message}"));
            throw new InvalidOperationException(
                $"{project.ProjectPath} (assembly {project.AssemblyName}, " +
                $"TargetPath {project.TargetPath}) の型ロードに失敗した。LoaderExceptions:\n  " +
                loaderExceptions,
                exception);
        }
    }
}
