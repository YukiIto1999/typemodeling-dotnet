using System.Diagnostics;

namespace TypeModeling.Analysis.Tests;

/// <summary>solution の root で build して、props の届かない project を検出する fixture の部分</summary>
internal sealed partial class ConsumerFixture
{
    /// <summary>solution の root に置く、届かない project を検出する targets の取り込み</summary>
    /// <param name="projects">solution に載せる project の、root からの相対 path</param>
    internal void WriteSolution(params string[] projects)
    {
        WriteFile(
            "Directory.Solution.targets",
            $"""
            <Project>
              <Import Project="{Path.Combine(Checkout, "TypeModeling.Solution.targets")}" />
            </Project>
            """
        );
        var entries = string.Join(
            "\n  ",
            projects.Select(project => $"<Project Path=\"{project}\" />")
        );
        WriteFile("Fixture.slnx", $"<Solution>\n  {entries}\n</Solution>");
    }

    /// <summary>solution を build する</summary>
    /// <param name="arguments">dotnet build に足す引数</param>
    /// <returns>終了 code と、build が報告した error</returns>
    internal Task<BuildResult> BuildSolutionAsync(params string[] arguments) =>
        RunAsync(_root, "Fixture.slnx", arguments);

    /// <summary>dotnet build の実行</summary>
    /// <param name="workingDirectory">build の作業 directory</param>
    /// <param name="project">build する project か solution の file 名</param>
    /// <param name="arguments">dotnet build に足す引数</param>
    /// <returns>終了 code と、build が報告した error</returns>
    private static async Task<BuildResult> RunAsync(
        string workingDirectory,
        string project,
        string[] arguments
    )
    {
        var start = new ProcessStartInfo(DotnetExecutable())
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("build");
        start.ArgumentList.Add(project);
        foreach (var argument in BuildArguments.Concat(arguments))
            start.ArgumentList.Add(argument);

        using var process =
            Process.Start(start) ?? throw new InvalidOperationException("dotnet を起動できない");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(BuildTimeout);
        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        var text = await output.ConfigureAwait(false) + await error.ConfigureAwait(false);
        return new BuildResult(
            process.ExitCode,
            ErrorPattern().Matches(text).Select(match => match.Value.Trim()).Distinct().ToArray()
        );
    }
}
