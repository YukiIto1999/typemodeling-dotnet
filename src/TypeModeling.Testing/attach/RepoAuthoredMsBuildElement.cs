using System.Xml.Linq;

namespace TypeModeling.Testing.Attach;

/// <summary>repository authored MSBuild element の判定</summary>
internal static class RepoAuthoredMsBuildElement
{
    /// <summary>MSBuild condition の祖先を含む判定</summary>
    /// <param name="element">検査対象 element</param>
    /// <returns>condition がある場合に true</returns>
    internal static bool HasCondition(XElement element) =>
        element.AncestorsAndSelf().Any(candidate =>
            candidate.Attribute("Condition") is { Value: var condition } &&
            !string.IsNullOrWhiteSpace(condition));

    /// <summary>MSBuild Target 配下の判定</summary>
    /// <param name="element">検査対象 element</param>
    /// <returns>Target 配下の場合に true</returns>
    internal static bool IsInsideTarget(XElement element) =>
        element.Ancestors().Any(ancestor => ancestor.Name.LocalName == "Target");
}
