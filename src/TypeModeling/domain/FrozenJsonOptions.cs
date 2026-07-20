using System.Text.Json;

namespace TypeModeling.Domain;

/// <summary>JSON serializer 設定を共有前に読取専用化する freeze factory</summary>
public static class FrozenJsonOptions
{
    /// <summary>JSON serializer 設定の同一 instance の読取専用化</summary>
    /// <param name="options">公開前の JSON serializer 設定</param>
    /// <returns>読取専用化した同一 instance</returns>
    public static JsonSerializerOptions Frozen(JsonSerializerOptions options)
    {
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
