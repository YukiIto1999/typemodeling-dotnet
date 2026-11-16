namespace TypeModeling.Analysis.Tests;

/// <summary>contract-generator が出す C# project の形を、利用側の repository へ書く fixture の部分</summary>
internal sealed partial class ConsumerFixture
{
    /// <summary>generator が出す project に共通の property</summary>
    private const string GeneratedProperties = """
        <TargetFramework>net10.0</TargetFramework>
        <LangVersion>14</LangVersion>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
        """;

    /// <summary>generator が出す Types と Provider の project を書く</summary>
    /// <remarks>
    /// contract-generator の src/targets/csharp/project-files.ts が出す形に従う。
    /// Types は実行時のライブラリを property で参照し、Provider は Types を相対 path で参照する。
    /// 利用側の repository が、ライブラリの path を Directory.Build.props の property に 1 度だけ書く。
    /// analyzer への参照は書かない。
    /// </remarks>
    /// <param name="directory">生成 project を置く、root からの相対 directory</param>
    /// <param name="reached">
    /// true なら root の Directory.Build.props が届く位置に置く。
    /// false なら、property だけを持ち親を import しない Directory.Build.props の下に置く
    /// </param>
    internal void WriteGeneratedContractProjects(string directory = "generated", bool reached = true)
    {
        var runtimeProject = Path.Combine(Checkout, "src", "TypeModeling", "TypeModeling.csproj");
        var runtimePath =
            $"<PropertyGroup><TypeModelingProjectPath>{runtimeProject}</TypeModelingProjectPath></PropertyGroup>";
        WriteFile(
            "Directory.Build.props",
            $"""
            <Project>
              {runtimePath}
              <Import Project="{Path.Combine(Checkout, "TypeModeling.Analysis.props")}" />
            </Project>
            """
        );
        if (!reached)
            WriteFile($"{directory}/Directory.Build.props", $"<Project>{runtimePath}</Project>");

        WriteFile(
            $"{directory}/types/Contract.Types.csproj",
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                {GeneratedProperties}
                <AssemblyName>Contract.Types</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="$(TypeModelingProjectPath)" />
              </ItemGroup>
            </Project>
            """
        );
        WriteFile(
            $"{directory}/types/Order.cs",
            "namespace Contract.Types; public sealed record Order(string Id);"
        );
        WriteFile(
            $"{directory}/provider/Contract.Provider.csproj",
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                {GeneratedProperties}
                <AssemblyName>Contract.Provider</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../types/Contract.Types.csproj" />
              </ItemGroup>
            </Project>
            """
        );
        WriteFile(
            $"{directory}/provider/Provider.cs",
            "namespace Contract.Provider; public static class Provider { public static Contract.Types.Order Make() => new(\"1\"); }"
        );
    }
}
