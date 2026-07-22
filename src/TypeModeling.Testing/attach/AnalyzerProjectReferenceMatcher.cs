using System.Xml.Linq;

namespace TypeModeling.Testing.Attach;

/// <summary>companion analyzer ProjectReference 照合器</summary>
internal static class AnalyzerProjectReferenceMatcher
{
    /// <summary>ProjectReference item spec の analyzer 対象可能性判定</summary>
    /// <param name="itemSpec">ProjectReference item spec</param>
    /// <param name="projectPath">参照元 project の絶対 path</param>
    /// <param name="analyzerPath">analyzer project の絶対 path</param>
    /// <param name="conservativeForUnresolvedItemSpec">未解決 item spec の保守判定</param>
    /// <returns>analyzer 対象可能性</returns>
    internal static bool PotentiallyTargetsAnalyzer(
        string itemSpec,
        string projectPath,
        string analyzerPath,
        bool conservativeForUnresolvedItemSpec)
    {
        var analyzerFileName = Path.GetFileName(analyzerPath);
        foreach (var item in itemSpec.Split(
                     ';',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (item.Contains(analyzerFileName, StringComparison.OrdinalIgnoreCase))
                return true;
            if (item.Contains("$(", StringComparison.Ordinal) ||
                item.Contains("@(", StringComparison.Ordinal) ||
                item.Contains('*', StringComparison.Ordinal))
            {
                if (conservativeForUnresolvedItemSpec)
                    return true;
                continue;
            }
            var fullPath = Path.GetFullPath(
                Path.IsPathRooted(item)
                    ? item
                    : Path.Combine(Path.GetDirectoryName(projectPath)!, item));
            if (PathsEqual(fullPath, analyzerPath))
                return true;
        }
        return false;
    }

    /// <summary>project path の等価判定</summary>
    /// <param name="left">左辺の project path</param>
    /// <param name="right">右辺の project path</param>
    /// <returns>platform 規則による path 等価判定</returns>
    internal static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    /// <summary>ProjectReference 操作属性の取得</summary>
    /// <param name="reference">ProjectReference 要素</param>
    /// <returns>XML 属性順で最初の操作名</returns>
    internal static string? ProjectReferenceOperation(XElement reference) =>
        reference.Attributes()
            .Select(attribute => attribute.Name.LocalName)
            .FirstOrDefault(name => name is "Include" or "Remove" or "Update");

    /// <summary>ProjectReference metadata 値の取得</summary>
    /// <param name="reference">ProjectReference 要素</param>
    /// <param name="name">metadata 名</param>
    /// <returns>属性優先の無条件 metadata 値</returns>
    internal static string ProjectReferenceMetadata(XElement reference, string name) =>
        reference.Attribute(name)?.Value ??
        reference.Elements().FirstOrDefault(element =>
            element.Name.LocalName == name &&
            !RepoAuthoredMsBuildElement.HasCondition(element))?.Value ??
        "";
}
