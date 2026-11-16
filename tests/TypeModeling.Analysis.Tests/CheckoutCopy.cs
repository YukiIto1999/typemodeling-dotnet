namespace TypeModeling.Analysis.Tests;

/// <summary>build の出力を持たない、analyzer だけの TypeModeling の checkout の写し</summary>
internal static class CheckoutCopy
{
    /// <summary>checkout の root にある、analyzer の build が読む file</summary>
    private static readonly string[] RootFiles =
    [
        "TypeModeling.Analysis.props",
        "Directory.Build.props",
        "Directory.Packages.props",
        "BannedSymbols.txt",
        "BannedSymbols.TypeModeling.txt",
        "SonarLint.xml",
        ".editorconfig",
    ];

    /// <summary>analyzer を build できる最小の checkout を、build の出力なしで写す</summary>
    /// <param name="source">写し元の checkout の root</param>
    /// <param name="destination">写し先の root。無ければ作る</param>
    internal static void Create(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in RootFiles)
            File.Copy(Path.Combine(source, file), Path.Combine(destination, file));

        CopyDirectory(
            Path.Combine(source, "src", "TypeModeling.Analyzers"),
            Path.Combine(destination, "src", "TypeModeling.Analyzers")
        );
    }

    /// <summary>bin と obj を除いた directory の再帰的な写し</summary>
    /// <param name="source">写し元の directory</param>
    /// <param name="destination">写し先の directory</param>
    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var name = Path.GetFileName(directory);
            if (name is not ("bin" or "obj"))
                CopyDirectory(directory, Path.Combine(destination, name));
        }
    }
}
