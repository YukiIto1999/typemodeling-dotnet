namespace TypeModeling.Testing.Attach;

/// <summary>MSBuild dimension 候補値の確定</summary>
internal static class ProjectBuildDimensionValues
{
    /// <summary>MSBuild dimension 評価候補の生成</summary>
    /// <param name="declaredValues">宣言済み dimension 値</param>
    /// <param name="currentValue">現在の dimension 値</param>
    /// <param name="fallback">候補未発見時の既定値</param>
    /// <param name="includeEmpty">空値を含める場合に true</param>
    /// <returns>評価対象の dimension 値集合</returns>
    internal static string[] Resolve(
        string declaredValues,
        string currentValue,
        string fallback,
        bool includeEmpty = false)
    {
        var values = MsBuildValueSplitter.SplitSemicolonList(declaredValues).ToList();
        if (currentValue.Length > 0 &&
            !values.Contains(currentValue, StringComparer.OrdinalIgnoreCase))
        {
            values.Add(currentValue);
        }
        if (includeEmpty && !values.Contains("", StringComparer.Ordinal))
            values.Insert(0, "");
        if (values.Count == 0)
            values.Add(fallback);
        return values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
