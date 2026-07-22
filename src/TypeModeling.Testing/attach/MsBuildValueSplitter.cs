namespace TypeModeling.Testing.Attach;

/// <summary>MSBuild 値の区切り文字分割</summary>
internal static class MsBuildValueSplitter
{
    /// <summary>MSBuild list 値の分割</summary>
    /// <param name="value">MSBuild list 値</param>
    /// <returns>分割済み値</returns>
    internal static string[] SplitList(string value) => value.Split(
        [';', ',', ' ', '\t', '\r', '\n'],
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>semicolon 区切り MSBuild list 値の分割</summary>
    /// <param name="value">semicolon 区切り MSBuild list 値</param>
    /// <returns>semicolon 区切りで分割した MSBuild list 値集合</returns>
    internal static string[] SplitSemicolonList(string value) => value.Split(
        ';',
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
