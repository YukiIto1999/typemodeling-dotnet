using System.Diagnostics.CodeAnalysis;

namespace TypeModeling.Testing.Attach;

/// <summary>repository authored MSBuild property の展開</summary>
internal static class RepoAuthoredPropertyExpander
{
    /// <summary>property 展開候補数の上限</summary>
    private const int ExpansionCandidateLimit = 256;

    /// <summary>MSBuild property expression の展開</summary>
    /// <param name="value">展開対象 expression</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <param name="propertyValues">repository authored property 値集合</param>
    /// <returns>property expression の展開結果</returns>
    [SuppressMessage("Critical Code Smell", "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "S3776 の導入前からある複雑度 16 の既存違反。基線台帳 S3776-008 に記録し、15 以下へ分割した時点で抑止を外す")]
    internal static RepoAuthoredPropertyExpansion Expand(
        string value,
        string projectPath,
        IReadOnlyDictionary<string, List<RepoAuthoredPropertyValue>> propertyValues)
    {
        var pending = new Queue<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var expandedValues = new HashSet<string>(StringComparer.Ordinal);
        var unresolvedValues = new HashSet<string>(StringComparer.Ordinal);
        pending.Enqueue(value);
        while (pending.TryDequeue(out var candidate))
        {
            if (visited.Count >= ExpansionCandidateLimit)
            {
                unresolvedValues.Add(candidate);
                foreach (var unexpanded in pending)
                    unresolvedValues.Add(unexpanded);
                break;
            }
            if (!visited.Add(candidate))
                continue;
            var propertyStart = candidate.IndexOf("$(", StringComparison.Ordinal);
            if (propertyStart < 0)
            {
                expandedValues.Add(candidate);
                continue;
            }
            var propertyEnd = candidate.IndexOf(')', propertyStart + 2);
            if (propertyEnd < 0)
            {
                unresolvedValues.Add(candidate);
                continue;
            }
            var propertyName = candidate[(propertyStart + 2)..propertyEnd];
            if (!propertyValues.TryGetValue(propertyName, out var replacements))
            {
                unresolvedValues.Add(candidate);
                continue;
            }
            foreach (var replacement in replacements)
            {
                pending.Enqueue(
                    candidate[..propertyStart] +
                    ExpandBuiltIns(replacement, projectPath) +
                    candidate[(propertyEnd + 1)..]);
            }
        }
        return new RepoAuthoredPropertyExpansion(
            expandedValues.Order(StringComparer.Ordinal).ToArray(),
            unresolvedValues.Order(StringComparer.Ordinal).ToArray());
    }

    /// <summary>定義位置に依存する built-in property の展開</summary>
    /// <param name="replacement">展開対象 property 値</param>
    /// <param name="projectPath">project の絶対 path</param>
    /// <returns>built-in property 展開済みの値</returns>
    private static string ExpandBuiltIns(
        RepoAuthoredPropertyValue replacement,
        string projectPath) =>
        replacement.Value
            .Replace(
                "$(MSBuildThisFileDirectory)",
                Path.GetDirectoryName(replacement.DefiningFile)! + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase)
            .Replace(
                "$(MSBuildThisFileFullPath)",
                replacement.DefiningFile,
                StringComparison.OrdinalIgnoreCase)
            .Replace(
                "$(MSBuildProjectDirectory)",
                Path.GetDirectoryName(projectPath)!,
                StringComparison.OrdinalIgnoreCase)
            .Replace(
                "$(MSBuildProjectFullPath)",
                projectPath,
                StringComparison.OrdinalIgnoreCase);
}
