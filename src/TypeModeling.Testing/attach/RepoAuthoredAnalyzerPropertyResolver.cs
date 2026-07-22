namespace TypeModeling.Testing.Attach;

/// <summary>repository authored analyzer property 解決器</summary>
internal static class RepoAuthoredAnalyzerPropertyResolver
{
    /// <summary>MSBuild 値から到達できる保護対象診断の列挙</summary>
    /// <param name="value">起点の MSBuild 値</param>
    /// <param name="propertyValues">property 名別の値集合</param>
    /// <param name="matcher">保護対象診断の照合器</param>
    /// <returns>保護対象診断 ID 集合</returns>
    internal static string[] DiagnosticsInMsBuildValue(
        string value,
        IReadOnlyDictionary<string, List<RepoAuthoredPropertyValue>> propertyValues,
        DiagnosticMatcher matcher)
    {
        var diagnostics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visitedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddDiagnostics(value, propertyValues, matcher, visitedProperties, diagnostics);
        return diagnostics.Order(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    /// <summary>false に到達できる MSBuild 値の判定</summary>
    /// <param name="value">起点の MSBuild 値</param>
    /// <param name="propertyValues">property 名別の値集合</param>
    /// <returns>false 到達可能性</returns>
    internal static bool CanResolveFalse(
        string value,
        IReadOnlyDictionary<string, List<RepoAuthoredPropertyValue>> propertyValues)
    {
        var visitedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return CanResolveFalse(value, propertyValues, visitedProperties);
    }

    /// <summary>true のみに解決される MSBuild 値の判定</summary>
    /// <param name="value">起点の MSBuild 値</param>
    /// <param name="propertyAssignments">property 名別の代入値集合</param>
    /// <returns>true のみへの解決判定</returns>
    internal static bool ResolvesOnlyToTrue(
        string value,
        IReadOnlyDictionary<string, List<string>> propertyAssignments)
    {
        var resolving = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return ResolvesOnlyToTrue(value, propertyAssignments, resolving);
    }

    /// <summary>MSBuild property 参照列挙</summary>
    /// <param name="value">探索対象の MSBuild 値</param>
    /// <returns>参照先 property 名集合</returns>
    private static IEnumerable<string> PropertyReferences(string value)
    {
        var searchStart = 0;
        while (searchStart < value.Length)
        {
            var propertyStart = value.IndexOf("$(", searchStart, StringComparison.Ordinal);
            if (propertyStart < 0)
                yield break;
            var propertyEnd = value.IndexOf(')', propertyStart + 2);
            if (propertyEnd < 0)
                yield break;
            var propertyName = value[(propertyStart + 2)..propertyEnd];
            if (propertyName.Length > 0 && propertyName[0] != '[')
                yield return propertyName;
            searchStart = propertyEnd + 1;
        }
    }

    /// <summary>false の MSBuild 値判定</summary>
    /// <param name="value">判定対象の MSBuild 値</param>
    /// <returns>false 値判定</returns>
    internal static bool IsFalse(string value) =>
        string.Equals(value.Trim(), "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>true の MSBuild 値判定</summary>
    /// <param name="value">判定対象の MSBuild 値</param>
    /// <returns>true 値判定</returns>
    private static bool IsTrue(string value) =>
        string.Equals(value.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>候補値から到達できる保護対象診断の追加</summary>
    /// <param name="candidate">探索対象の候補値</param>
    /// <param name="propertyValues">property 名別の値集合</param>
    /// <param name="matcher">保護対象診断の照合器</param>
    /// <param name="visitedProperties">探索済み property 名集合</param>
    /// <param name="diagnostics">診断 ID の追加先</param>
    private static void AddDiagnostics(
        string candidate,
        IReadOnlyDictionary<string, List<RepoAuthoredPropertyValue>> propertyValues,
        DiagnosticMatcher matcher,
        HashSet<string> visitedProperties,
        HashSet<string> diagnostics)
    {
        foreach (var diagnosticId in MsBuildValueSplitter.SplitList(candidate)
                     .Where(matcher.Matches))
        {
            diagnostics.Add(diagnosticId);
        }
        foreach (var propertyName in PropertyReferences(candidate))
        {
            if (!visitedProperties.Add(propertyName) ||
                !propertyValues.TryGetValue(propertyName, out var replacements))
            {
                continue;
            }
            foreach (var replacement in replacements)
            {
                AddDiagnostics(
                    replacement.Value,
                    propertyValues,
                    matcher,
                    visitedProperties,
                    diagnostics);
            }
        }
    }

    /// <summary>候補値からの false 到達可能性判定</summary>
    /// <param name="candidate">探索対象の候補値</param>
    /// <param name="propertyValues">property 名別の値集合</param>
    /// <param name="visitedProperties">探索済み property 名集合</param>
    /// <returns>false 到達可能性</returns>
    private static bool CanResolveFalse(
        string candidate,
        IReadOnlyDictionary<string, List<RepoAuthoredPropertyValue>> propertyValues,
        HashSet<string> visitedProperties)
    {
        if (IsFalse(candidate))
            return true;
        foreach (var propertyName in PropertyReferences(candidate))
        {
            if (!visitedProperties.Add(propertyName) ||
                !propertyValues.TryGetValue(propertyName, out var replacements))
            {
                continue;
            }
            if (replacements.Any(replacement =>
                    CanResolveFalse(replacement.Value, propertyValues, visitedProperties)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>候補値の true 限定解決判定</summary>
    /// <param name="candidate">探索対象の候補値</param>
    /// <param name="propertyAssignments">property 名別の代入値集合</param>
    /// <param name="resolving">解決中の property 名集合</param>
    /// <returns>true のみへの解決判定</returns>
    private static bool ResolvesOnlyToTrue(
        string candidate,
        IReadOnlyDictionary<string, List<string>> propertyAssignments,
        HashSet<string> resolving)
    {
        if (IsTrue(candidate))
            return true;
        var trimmed = candidate.Trim();
        if (!trimmed.StartsWith("$(", StringComparison.Ordinal) ||
            !trimmed.EndsWith(')') ||
            trimmed.IndexOf(')', 2) != trimmed.Length - 1)
        {
            return false;
        }
        var propertyName = trimmed[2..^1];
        if (!resolving.Add(propertyName) ||
            !propertyAssignments.TryGetValue(propertyName, out var replacements) ||
            replacements.Count == 0)
        {
            return false;
        }
        var result = replacements.All(replacement =>
            ResolvesOnlyToTrue(replacement, propertyAssignments, resolving));
        resolving.Remove(propertyName);
        return result;
    }
}
