using System.Diagnostics;

namespace TypeModeling.Testing.Closure;

/// <summary>repository が所有する C# project の棚卸し</summary>
internal static class RepoProjectInventory
{
    /// <summary>git の除外規則に従う project の相対パス列挙</summary>
    /// <param name="repoRoot">棚卸し対象の repository root</param>
    /// <returns>序数順の project 相対パス</returns>
    internal static IReadOnlyList<string> ProjectPaths(string repoRoot)
    {
        if (!File.Exists(Path.Combine(repoRoot, ".git"))
            && !Directory.Exists(Path.Combine(repoRoot, ".git")))
        {
            return Directory.EnumerateFiles(
                    repoRoot,
                    "*.csproj",
                    new EnumerationOptions
                    {
                        RecurseSubdirectories = true,
                        AttributesToSkip = FileAttributes.ReparsePoint,
                    })
                .Select(path => Path.GetRelativePath(repoRoot, path).Replace('\\', '/'))
                .Where(path => !path.Split('/').Any(segment =>
                    segment is "bin" or "obj" or ".git" or "node_modules" or "StrykerOutput"))
                .Order(StringComparer.Ordinal)
                .ToArray();
        }

        var gitPath = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, OperatingSystem.IsWindows() ? "git.exe" : "git"))
            .FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException("PATH から git を見つけられない");
        var start = new ProcessStartInfo(gitPath)
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var name in start.Environment.Keys.Where(name =>
                     name.StartsWith("GIT_", StringComparison.Ordinal)).ToArray())
            start.Environment.Remove(name);
        foreach (var argument in new[]
                 { "ls-files", "--cached", "--others", "--exclude-standard", "-z", "--", "*.csproj" })
            start.ArgumentList.Add(argument);
        using var git = Process.Start(start)
            ?? throw new InvalidOperationException("git による project 棚卸しを開始できない");
        var paths = git.StandardOutput.ReadToEnd();
        var error = git.StandardError.ReadToEnd();
        git.WaitForExit();
        if (git.ExitCode != 0)
            throw new InvalidOperationException($"git による project 棚卸しに失敗した: {error}");

        return paths.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(path => File.Exists(Path.Combine(repoRoot, path)))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
