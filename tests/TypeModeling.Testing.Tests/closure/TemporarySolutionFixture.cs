namespace TypeModeling.Testing.Tests.Closure;

/// <summary>一時ディレクトリに構築する最小の推移 ProjectReference solution</summary>
internal sealed class TemporarySolutionFixture : IDisposable
{
    private const string FixtureSolutionFileName = "Fixture.slnx";

    /// <summary>一時 solution fixture の構築</summary>
    internal TemporarySolutionFixture()
    {
        _workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            "typemodeling-testing-closure-" + Guid.NewGuid().ToString("N"));
        RepoRoot = Path.Combine(_workspaceRoot, "repo");
        Directory.CreateDirectory(Path.Combine(RepoRoot, "App"));
        Directory.CreateDirectory(Path.Combine(RepoRoot, "Library"));
        Directory.CreateDirectory(Path.Combine(_workspaceRoot, "external"));
        WriteRepositoryFixture();
        WriteExternalFixture();
    }

    private readonly string _workspaceRoot;

    /// <summary>fixture repository の絶対パス</summary>
    internal string RepoRoot { get; }

    /// <summary>fixture の solution ファイル名</summary>
    internal string SolutionFileName { get; } = FixtureSolutionFileName;

    /// <summary>solution と repository 内 project の書き込み</summary>
    private void WriteRepositoryFixture()
    {
        File.WriteAllText(
            Path.Combine(RepoRoot, FixtureSolutionFileName),
            """
            <Solution>
              <Project Path="App/App.csproj" />
            </Solution>
            """);
        File.WriteAllText(
            Path.Combine(RepoRoot, "App", "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.App</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../Library/Library.csproj" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(RepoRoot, "App", "AppType.cs"),
            """
            namespace Fixture.App;

            public sealed class AppType : Fixture.Library.LibraryType;
            """);
        File.WriteAllText(
            Path.Combine(RepoRoot, "Library", "Library.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.Library</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../../external/External.csproj" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(RepoRoot, "Library", "LibraryType.cs"),
            """
            namespace Fixture.Library;

            public class LibraryType : Fixture.External.ExternalType;
            """);
    }

    /// <summary>repository 外の推移参照 project の書き込み</summary>
    private void WriteExternalFixture()
    {
        File.WriteAllText(
            Path.Combine(_workspaceRoot, "external", "External.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.External</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(_workspaceRoot, "external", "ExternalType.cs"),
            """
            namespace Fixture.External;

            public class ExternalType;
            """);
    }

    /// <summary>静的解決不能な参照を持つ build 不可 fixture への変更</summary>
    internal void UseStaticallyUnresolvableReferenceWithBrokenBuild()
    {
        File.WriteAllText(
            Path.Combine(RepoRoot, "App", "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Fixture.App</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="$(LibraryProject)" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(RepoRoot, "App", "AppType.cs"),
            "this source does not compile");
    }

    /// <summary>一時 solution fixture の削除</summary>
    public void Dispose() =>
        Directory.Delete(_workspaceRoot, recursive: true);
}
