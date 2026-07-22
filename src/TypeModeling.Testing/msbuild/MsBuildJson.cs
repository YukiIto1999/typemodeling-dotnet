using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace TypeModeling.Testing.MsBuild;

/// <summary>MSBuild 標準出力 JSON の解析機</summary>
internal static class MsBuildJson
{
    /// <summary>MSBuild 標準出力に含まれる JSON の解析</summary>
    /// <param name="standardOutput">MSBuild の標準出力</param>
    /// <param name="evaluation">解析済み MSBuild 評価 JSON</param>
    /// <returns>JSON の存在有無</returns>
    internal static bool TryParse(
        string standardOutput,
        [NotNullWhen(true)] out JsonDocument? evaluation)
    {
        var rootMarker = standardOutput.LastIndexOf("\n{", StringComparison.Ordinal);
        var jsonStart = rootMarker >= 0 ? rootMarker + 1 : -1;
        if (standardOutput.StartsWith('{'))
            jsonStart = 0;
        if (jsonStart < 0)
        {
            evaluation = null;
            return false;
        }

        evaluation = JsonDocument.Parse(standardOutput[jsonStart..]);
        return true;
    }

    /// <summary>MSBuild property 値の取得</summary>
    /// <param name="evaluation">MSBuild 評価 JSON</param>
    /// <param name="propertyName">property 名</param>
    /// <returns>property 値</returns>
    internal static string Property(JsonDocument evaluation, string propertyName) =>
        evaluation.RootElement
            .GetProperty("Properties")
            .GetProperty(propertyName)
            .GetString() ?? "";

    /// <summary>MSBuild item 集合の取得</summary>
    /// <param name="evaluation">MSBuild 評価 JSON</param>
    /// <param name="itemName">item 名</param>
    /// <returns>item JSON 集合</returns>
    internal static IReadOnlyList<JsonElement> Items(
        JsonDocument evaluation,
        string itemName) =>
        evaluation.RootElement.TryGetProperty("Items", out var items) &&
        items.TryGetProperty(itemName, out var values)
            ? values.EnumerateArray().ToArray()
            : [];

    /// <summary>MSBuild item metadata 値の取得</summary>
    /// <param name="item">item JSON</param>
    /// <param name="name">metadata 名</param>
    /// <returns>metadata 値</returns>
    internal static string ItemValue(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value)
            ? value.GetString() ?? ""
            : "";
}
