using System.Xml.Linq;

namespace TypeModeling.Testing.Attach;

/// <summary>repository authored import の固定点走査</summary>
internal static class RepoAuthoredImportTraversal
{
    /// <summary>未処理 import graph の固定点走査</summary>
    /// <param name="pending">未処理 file queue</param>
    /// <param name="files">到達済み file 目録</param>
    /// <param name="propertyValues">property 値目録</param>
    /// <param name="unresolvedImports">未解決 import 集合</param>
    /// <param name="projectPath">起点 project の絶対 path</param>
    /// <param name="authoredRoot">repository authored path の root</param>
    internal static void Drain(
        Queue<RepoAuthoredMsBuildFile> pending,
        Dictionary<string, bool> files,
        Dictionary<string, List<RepoAuthoredPropertyValue>> propertyValues,
        HashSet<RepoAuthoredImport> unresolvedImports,
        string projectPath,
        string authoredRoot)
    {
        var lastRetryPropertyCount = -1;
        while (true)
        {
            DrainPendingFiles(
                pending,
                files,
                propertyValues,
                unresolvedImports,
                projectPath,
                authoredRoot);

            var propertyCount = propertyValues.Values.Sum(values => values.Count);
            if (unresolvedImports.Count == 0 || propertyCount == lastRetryPropertyCount)
                break;
            lastRetryPropertyCount = propertyCount;
            foreach (var request in unresolvedImports.ToArray())
            {
                var resolution = Resolve(
                    request,
                    projectPath,
                    authoredRoot,
                    propertyValues);
                EnqueueResolution(pending, request, resolution);
                if (resolution.UnresolvedPatterns.Count == 0)
                    unresolvedImports.Remove(request);
            }
        }
    }

    /// <summary>queue 内の repository authored file を走査</summary>
    /// <param name="pending">未処理 file queue</param>
    /// <param name="files">到達済み file 目録</param>
    /// <param name="propertyValues">property 値目録</param>
    /// <param name="unresolvedImports">未解決 import 集合</param>
    /// <param name="projectPath">起点 project の絶対 path</param>
    /// <param name="authoredRoot">repository authored path の root</param>
    private static void DrainPendingFiles(
        Queue<RepoAuthoredMsBuildFile> pending,
        Dictionary<string, bool> files,
        Dictionary<string, List<RepoAuthoredPropertyValue>> propertyValues,
        HashSet<RepoAuthoredImport> unresolvedImports,
        string projectPath,
        string authoredRoot)
    {
        while (pending.TryDequeue(out var candidate))
        {
            if (files.TryGetValue(candidate.Path, out var existingCondition) &&
                (!existingCondition || candidate.IsConditionallyImported))
            {
                continue;
            }

            files[candidate.Path] = candidate.IsConditionallyImported;
            var document = XDocument.Load(candidate.Path, LoadOptions.PreserveWhitespace);
            RepoAuthoredPropertyCatalog.AddValues(
                document,
                candidate.Path,
                propertyValues);
            foreach (var import in document.Descendants()
                         .Where(element => element.Name.LocalName == "Import"))
            {
                var importedProject = import.Attribute("Project")?.Value;
                if (string.IsNullOrWhiteSpace(importedProject))
                    continue;
                var request = new RepoAuthoredImport(
                    importedProject,
                    candidate.Path,
                    candidate.IsConditionallyImported ||
                    RepoAuthoredMsBuildElement.HasCondition(import) ||
                    RepoAuthoredMsBuildElement.IsInsideTarget(import));
                var resolution = Resolve(
                    request,
                    projectPath,
                    authoredRoot,
                    propertyValues);
                EnqueueResolution(pending, request, resolution);
                if (resolution.UnresolvedPatterns.Count > 0)
                    unresolvedImports.Add(request);
                else
                    unresolvedImports.Remove(request);
            }
        }
    }

    /// <summary>現在の property 目録による import 解決</summary>
    /// <param name="request">import 宣言</param>
    /// <param name="projectPath">起点 project の絶対 path</param>
    /// <param name="authoredRoot">repository authored path の root</param>
    /// <param name="propertyValues">property 値目録</param>
    /// <returns>import 解決結果</returns>
    private static RepoAuthoredImportResolution Resolve(
        RepoAuthoredImport request,
        string projectPath,
        string authoredRoot,
        IReadOnlyDictionary<string, List<RepoAuthoredPropertyValue>> propertyValues) =>
        RepoAuthoredImportResolver.Resolve(
            request.Project,
            request.ImportingFile,
            projectPath,
            authoredRoot,
            propertyValues);

    /// <summary>解決済み import path の走査待ち登録</summary>
    /// <param name="pending">未処理 file queue</param>
    /// <param name="request">import 宣言</param>
    /// <param name="resolution">import 解決結果</param>
    private static void EnqueueResolution(
        Queue<RepoAuthoredMsBuildFile> pending,
        RepoAuthoredImport request,
        RepoAuthoredImportResolution resolution)
    {
        foreach (var importedPath in resolution.Paths)
        {
            pending.Enqueue(new RepoAuthoredMsBuildFile(
                importedPath,
                request.IsConditional));
        }
    }
}
