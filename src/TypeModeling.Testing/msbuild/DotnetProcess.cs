using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TypeModeling.Testing.MsBuild;

/// <summary>dotnet process の実行機</summary>
internal static class DotnetProcess
{
    /// <summary>process kill 後の drain timeout</summary>
    private const int KillDrainTimeoutMilliseconds = 5_000;

    /// <summary>指定 timeout による dotnet process の実行</summary>
    /// <param name="workingDirectory">process の作業 directory</param>
    /// <param name="arguments">dotnet host へ渡す引数</param>
    /// <param name="timeoutMilliseconds">終了を待つ timeout</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>dotnet process の非同期完了結果</returns>
    internal static async Task<DotnetProcessResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(DotnetExecutable())
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        cancellationToken.ThrowIfCancellationRequested();
        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("dotnet process を開始できない");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
        using var timeoutSource = new CancellationTokenSource(timeoutMilliseconds);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        try
        {
            await Task.WhenAll(
                    process.WaitForExitAsync(CancellationToken.None),
                    stdoutTask,
                    stderrTask)
                .WaitAsync(linkedSource.Token)
                .ConfigureAwait(false);
            var standardOutput = await stdoutTask.ConfigureAwait(false);
            var standardError = await stderrTask.ConfigureAwait(false);
            return new DotnetProcessResult(
                process.ExitCode,
                standardOutput,
                standardError);
        }
        catch (OperationCanceledException) when (linkedSource.IsCancellationRequested)
        {
            var callerCancellationRequested = cancellationToken.IsCancellationRequested;
            var processTerminationException = KillProcessTree(process);
            await DrainAfterKillAsync(process, stdoutTask, stderrTask).ConfigureAwait(false);

            throw InterruptionException(
                callerCancellationRequested,
                arguments,
                timeoutMilliseconds,
                processTerminationException,
                cancellationToken);
        }
    }

    /// <summary>実行中 process tree の終了</summary>
    /// <param name="process">終了対象 process</param>
    /// <returns>process tree 終了失敗</returns>
    private static Exception? KillProcessTree(Process process)
    {
        try
        {
            if (process.HasExited)
                return null;

            process.Kill(entireProcessTree: true);
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (Win32Exception exception)
        {
            return exception;
        }
        catch (AggregateException exception)
        {
            return exception;
        }
        catch (NotSupportedException exception)
        {
            return exception;
        }
    }

    /// <summary>process kill 後の非同期出力 drain</summary>
    /// <param name="process">終了対象 process</param>
    /// <param name="stdoutTask">標準出力読取 task</param>
    /// <param name="stderrTask">標準エラー読取 task</param>
    /// <returns>drain の非同期完了</returns>
    private static async Task DrainAfterKillAsync(
        Process process,
        Task<string> stdoutTask,
        Task<string> stderrTask)
    {
        try
        {
            await Task.WhenAll(
                    process.WaitForExitAsync(),
                    stdoutTask,
                    stderrTask)
                .WaitAsync(TimeSpan.FromMilliseconds(KillDrainTimeoutMilliseconds))
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // caller cancellation または timeout の送出を優先するための drain 例外破棄
        }
    }

    /// <summary>process 中断例外の生成</summary>
    /// <param name="callerCancellationRequested">caller cancellation の発生状態</param>
    /// <param name="arguments">dotnet host へ渡した引数</param>
    /// <param name="timeoutMilliseconds">終了を待った timeout</param>
    /// <param name="processTerminationException">process tree 終了失敗</param>
    /// <param name="cancellationToken">処理中止 token</param>
    /// <returns>中断要因に対応する例外</returns>
    private static Exception InterruptionException(
        bool callerCancellationRequested,
        IReadOnlyList<string> arguments,
        int timeoutMilliseconds,
        Exception? processTerminationException,
        CancellationToken cancellationToken)
    {
        if (callerCancellationRequested)
        {
            return processTerminationException is null
                ? new OperationCanceledException(cancellationToken)
                : new OperationCanceledException(
                    "dotnet process の実行が取り消された",
                    processTerminationException,
                    cancellationToken);
        }

        var message =
            $"dotnet {string.Join(' ', arguments)} が {timeoutMilliseconds}ms 以内に終了しなかった";
        return processTerminationException is null
            ? new TimeoutException(message)
            : new TimeoutException(message, processTerminationException);
    }

    /// <summary>dotnet host 実行パスの解決</summary>
    /// <returns>dotnet host 実行パス</returns>
    private static string DotnetExecutable()
    {
        var configuredPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;

        var runtimeDirectory = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory());
        var dotnetRoot = runtimeDirectory.Parent?.Parent?.Parent;
        if (dotnetRoot is not null)
        {
            var executableName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
            var runtimeHostPath = Path.Combine(dotnetRoot.FullName, executableName);
            if (File.Exists(runtimeHostPath))
                return runtimeHostPath;
        }

        return "dotnet";
    }
}
