namespace TypeModeling.Testing.Tests.Closure;

/// <summary>contract-generator が出す C# project の形の fixture</summary>
internal sealed partial class TemporarySolutionFixture
{
    /// <summary>contract-generator が出す C# project の形への変更。App が Provider を参照し、Provider が Types を参照する</summary>
    /// <remarks>
    /// Types は実行時のライブラリを `$(TypeModelingProjectPath)` で参照し、Provider は Types を相対 path で参照する。
    /// ライブラリの path は、利用側の repository の Directory.Build.props が property として 1 度だけ書く。
    /// ここでは External が実行時のライブラリに当たる
    /// </remarks>
    internal void UseGeneratedContractShape()
    {
        Directory.Delete(Path.Combine(RepoRoot, "Library"), recursive: true);
        WriteRepositoryFile(
            "Directory.Build.props",
            $"""
            <Project>
              <PropertyGroup>
                <TypeModelingProjectPath>{Path.Combine(
                _workspaceRoot,
                "external",
                "External.csproj"
            )}</TypeModelingProjectPath>
              </PropertyGroup>
            </Project>
            """
        );
        WriteProjectWithReference(
            "App/App.csproj",
            "Fixture.App",
            "../generated/provider/Contract.Provider.csproj"
        );
        WriteRepositoryFile(
            "App/AppType.cs",
            "namespace Fixture.App; public sealed class AppType : Fixture.Contract.Provider.ProviderType;"
        );
        WriteProjectWithReference(
            "generated/types/Contract.Types.csproj",
            "Fixture.Contract.Types",
            "$(TypeModelingProjectPath)"
        );
        WriteRepositoryFile(
            "generated/types/ContractType.cs",
            "namespace Fixture.Contract.Types; public class ContractType : Fixture.External.ExternalType;"
        );
        WriteProjectWithReference(
            "generated/provider/Contract.Provider.csproj",
            "Fixture.Contract.Provider",
            "../types/Contract.Types.csproj"
        );
        WriteRepositoryFile(
            "generated/provider/ProviderType.cs",
            "namespace Fixture.Contract.Provider; public class ProviderType : Fixture.Contract.Types.ContractType;"
        );
    }

    /// <summary>ProjectReference を 1 つ持つ project の書き込み</summary>
    /// <param name="relativePath">repository root からの project の相対パス</param>
    /// <param name="assemblyName">project の AssemblyName</param>
    /// <param name="reference">ProjectReference の Include</param>
    private void WriteProjectWithReference(
        string relativePath,
        string assemblyName,
        string reference
    ) =>
        WriteRepositoryFile(
            relativePath,
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>{assemblyName}</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="{reference}" />
              </ItemGroup>
            </Project>
            """
        );

    /// <summary>repository 内の file の書き込み</summary>
    /// <param name="relativePath">repository root からの相対パス</param>
    /// <param name="content">file の内容</param>
    private void WriteRepositoryFile(string relativePath, string content)
    {
        var path = Path.Combine(RepoRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
