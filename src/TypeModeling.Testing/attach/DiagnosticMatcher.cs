namespace TypeModeling.Testing.Attach;

/// <summary>保護対象診断 ID の照合器</summary>
internal sealed class DiagnosticMatcher
{
    /// <summary>完全一致で保護する診断 ID 集合</summary>
    private readonly IReadOnlySet<string> _diagnosticIds;

    /// <summary>前方一致で保護する診断 ID prefix 集合</summary>
    private readonly IReadOnlySet<string> _diagnosticPrefixes;

    /// <summary>完全一致で保護する診断 category 集合</summary>
    private readonly IReadOnlySet<string> _diagnosticCategories;

    /// <summary>診断保護要件からの照合器構築</summary>
    /// <param name="diagnosticIds">保護対象の診断 ID 集合</param>
    /// <param name="diagnosticPrefixes">保護対象の診断 prefix 集合</param>
    /// <param name="diagnosticCategories">保護対象の診断 category 集合</param>
    internal DiagnosticMatcher(
        IReadOnlySet<string> diagnosticIds,
        IReadOnlySet<string> diagnosticPrefixes,
        IReadOnlySet<string> diagnosticCategories)
    {
        _diagnosticIds = diagnosticIds;
        _diagnosticPrefixes = diagnosticPrefixes;
        _diagnosticCategories = diagnosticCategories;
    }

    /// <summary>診断 ID または wildcard による保護対象との重なり判定</summary>
    /// <param name="value">診断 ID または末尾 wildcard</param>
    /// <returns>保護対象を覆う場合に true</returns>
    internal bool Matches(string value)
    {
        var candidate = value.Trim();
        var wildcard = candidate.EndsWith('*');
        var stem = wildcard ? candidate[..^1] : candidate;
        if (wildcard)
        {
            return _diagnosticIds.Any(id => id.StartsWith(stem, StringComparison.OrdinalIgnoreCase)) ||
                _diagnosticPrefixes.Any(prefix =>
                    prefix.StartsWith(stem, StringComparison.OrdinalIgnoreCase) ||
                    stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        return _diagnosticIds.Contains(candidate) || _diagnosticIds.Any(id =>
                   string.Equals(id, candidate, StringComparison.OrdinalIgnoreCase)) ||
               _diagnosticPrefixes.Any(prefix =>
                   candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>診断 category による保護対象との一致判定</summary>
    /// <param name="category">診断 category</param>
    /// <returns>保護対象 category の場合に true</returns>
    internal bool MatchesCategory(string category) =>
        _diagnosticCategories.Contains(category) || _diagnosticCategories.Any(candidate =>
            string.Equals(candidate, category, StringComparison.OrdinalIgnoreCase));
}
