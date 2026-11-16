using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace TypeModeling.Analysis.Tests;

/// <summary>TypeModeling.Analysis.props を Directory.Build.props から取り込む利用側 repository の一時 fixture</summary>
internal sealed partial class ConsumerFixture : IDisposable
{
    /// <summary>fixture の build に共通の引数。node と compiler server を残さず、audit の通信を避ける</summary>
    private static readonly string[] BuildArguments =
    [
        "-nologo",
        "-v:m",
        "-nodeReuse:false",
        "-p:UseSharedCompilation=false",
        "-p:NuGetAudit=false",
    ];

    /// <summary>build の終了を待つ時間</summary>
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(5);

    /// <summary>閉じた型の宣言と、規則に従う使い方</summary>
    internal const string CleanSource = """
        namespace TypeModeling.Domain
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Enum)]
            public sealed class ClosedAttribute : System.Attribute;

            [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
            public sealed class ClosedSubtypeCountAttribute(System.Type closedType, int directSubtypeCount) : System.Attribute
            {
                public System.Type ClosedType { get; } = closedType;
                public int DirectSubtypeCount { get; } = directSubtypeCount;
            }
        }

        namespace Fixture.Consumer
        {
            using TypeModeling.Domain;

            [Closed]
            public abstract class Shape
            {
                private Shape() { }

                public sealed class Circle : Shape;
            }
        }
        """;

    /// <summary>閉じた class を abstract で宣言しない違反。TYPMOD003 になる</summary>
    internal const string ViolatingSource =
        CleanSource
        + """


            namespace Fixture.Consumer
            {
                [TypeModeling.Domain.Closed]
                public class Concrete;
            }
            """;

    /// <summary>一時 repository の root</summary>
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "typemodeling-analysis-" + Guid.NewGuid().ToString("N")
    );

    /// <summary>利用側の repository を、取り込む props を指して構築する</summary>
    /// <param name="checkout">取り込む TypeModeling の checkout。省略すると、この test が属する checkout</param>
    internal ConsumerFixture(string? checkout = null)
    {
        Checkout = checkout ?? CurrentCheckout();
        Directory.CreateDirectory(Path.Combine(_root, "consumer"));
        File.WriteAllText(
            Path.Combine(_root, "Directory.Build.props"),
            $"""
            <Project>
              <Import Project="{Path.Combine(Checkout, "TypeModeling.Analysis.props")}" />
            </Project>
            """
        );
        WriteProject();
        WriteSource(CleanSource);
    }

    /// <summary>取り込む TypeModeling の checkout の root</summary>
    internal string Checkout { get; }

    /// <summary>取り込んだ checkout の analyzer project の絶対 path</summary>
    internal string AnalyzerProject =>
        Path.Combine(Checkout, "src", "TypeModeling.Analyzers", "TypeModeling.Analyzers.csproj");

    /// <summary>利用側の repository の root</summary>
    internal string Root => _root;

    /// <summary>利用側の project を書く</summary>
    /// <param name="body">PropertyGroup と ItemGroup などの本体。{Analyzer} は analyzer project の絶対 path に置き換える</param>
    /// <param name="frameworks">TargetFramework か TargetFrameworks の宣言</param>
    internal void WriteProject(
        string body = "",
        string frameworks = "<TargetFramework>net10.0</TargetFramework>"
    ) =>
        File.WriteAllText(
            Path.Combine(_root, "consumer", "Consumer.csproj"),
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                {frameworks}
                <AssemblyName>Fixture.Consumer</AssemblyName>
                <ImplicitUsings>disable</ImplicitUsings>
                <Nullable>disable</Nullable>
              </PropertyGroup>
              {body.Replace("{Analyzer}", AnalyzerProject, StringComparison.Ordinal)}
            </Project>
            """
        );

    /// <summary>利用側の source を書く</summary>
    /// <param name="source">C# source</param>
    internal void WriteSource(string source) => WriteFile("consumer/Source.cs", source);

    /// <summary>利用側の repository の中に file を書く</summary>
    /// <param name="relativePath">root からの相対 path</param>
    /// <param name="content">file の内容。{Analyzer} は analyzer project の絶対 path に置き換える</param>
    internal void WriteFile(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            content.Replace("{Analyzer}", AnalyzerProject, StringComparison.Ordinal)
        );
    }

    /// <summary>利用側の project を build する</summary>
    /// <param name="arguments">dotnet build に足す引数</param>
    /// <returns>終了 code と、build が報告した error</returns>
    internal Task<BuildResult> BuildAsync(params string[] arguments) =>
        RunAsync(Path.Combine(_root, "consumer"), "Consumer.csproj", arguments);

    /// <summary>一時 repository の削除</summary>
    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>build が報告する error の行</summary>
    /// <returns>error の ID と message を取る正規表現</returns>
    [GeneratedRegex(@"error [A-Za-z]+\d+: [^\[\r\n]*")]
    private static partial Regex ErrorPattern();

    /// <summary>dotnet host の実行 path の解決</summary>
    /// <returns>dotnet host の path。見つからなければ PATH で引く名前</returns>
    private static string DotnetExecutable()
    {
        var configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;

        var dotnetRoot = new DirectoryInfo(RuntimeEnvironment.GetRuntimeDirectory())
            .Parent
            ?.Parent
            ?.Parent;
        var host = dotnetRoot is null ? null : Path.Combine(dotnetRoot.FullName, "dotnet");
        return host is not null && File.Exists(host) ? host : "dotnet";
    }

    /// <summary>この test が属する TypeModeling の checkout の root</summary>
    /// <param name="sourcePath">呼び出し元 source の絶対 path</param>
    /// <returns>checkout の root</returns>
    internal static string CurrentCheckout([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, "..", ".."));
}

/// <summary>build の結果</summary>
/// <param name="ExitCode">dotnet build の終了 code</param>
/// <param name="Errors">build が報告した error の ID と message</param>
internal sealed record BuildResult(int ExitCode, IReadOnlyList<string> Errors)
{
    /// <summary>build が成功したか</summary>
    internal bool Succeeded => ExitCode == 0;

    /// <summary>指定した ID の error があるか</summary>
    /// <param name="id">診断か build の error の ID</param>
    /// <returns>ある場合に true</returns>
    internal bool Reports(string id) =>
        Errors.Any(error => error.StartsWith($"error {id}:", StringComparison.Ordinal));

    /// <summary>失敗の説明に使う、error の一覧</summary>
    /// <returns>改行で区切った error</returns>
    internal string Describe() => $"exit {ExitCode}\n" + string.Join("\n", Errors);
}
