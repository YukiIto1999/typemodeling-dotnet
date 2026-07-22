namespace TypeModeling.Testing.MsBuild;

/// <summary>dotnet process の完了結果</summary>
/// <param name="ExitCode">process の終了 code</param>
/// <param name="StandardOutput">標準出力の全文</param>
/// <param name="StandardError">標準エラー出力の全文</param>
internal readonly record struct DotnetProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
