using System.Xml.Linq;

namespace TypeModeling.Testing.Attach;

/// <summary>repository authored MSBuild property の目録</summary>
internal static class RepoAuthoredPropertyCatalog
{
    /// <summary>repository authored property 値の収集</summary>
    /// <param name="document">MSBuild XML document</param>
    /// <param name="definingFile">定義元 file</param>
    /// <param name="propertyValues">property 値の追加先</param>
    internal static void AddValues(
        XDocument document,
        string definingFile,
        Dictionary<string, List<RepoAuthoredPropertyValue>> propertyValues)
    {
        foreach (var property in document.Descendants().Where(element =>
                     element.Parent?.Name.LocalName == "PropertyGroup" &&
                     !string.IsNullOrWhiteSpace(element.Value)))
        {
            if (!propertyValues.TryGetValue(property.Name.LocalName, out var values))
            {
                values = [];
                propertyValues[property.Name.LocalName] = values;
            }
            var candidate = new RepoAuthoredPropertyValue(property.Value.Trim(), definingFile);
            if (!values.Contains(candidate))
                values.Add(candidate);
        }
    }
}
