using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Tests;

/// <summary>compiler probe が共有する実行環境の metadata 参照</summary>
internal static class CompilationReferences
{
    /// <summary>同一 process 内の probe compilation に再利用する framework assembly の参照</summary>
    /// <value>実行環境が列挙した順の共有 metadata 参照</value>
    internal static ImmutableArray<MetadataReference> Platform { get; } =
        [.. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
}
