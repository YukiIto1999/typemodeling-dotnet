using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Generation.Union;

/// <summary>C# 15 の union 宣言と同じ形を、C# 14 以前の partial struct に生成する generator</summary>
[Generator]
public sealed class UnionGenerator : IIncrementalGenerator
{
    /// <summary>polyfill と union 宣言の生成の登録</summary>
    /// <param name="context">incremental generator の初期化 context</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static output =>
        {
            output.AddEmbeddedAttributeDefinition();
            output.AddSource(UnionPolyfillSource.HintName, UnionPolyfillSource.Text);
        });

        var unions = context.SyntaxProvider.ForAttributeWithMetadataName(
            UnionTypeSet.UnionAttributeMetadataName,
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (attributeContext, _) => attributeContext.TargetSymbol is INamedTypeSymbol type
                ? UnionDeclarationSource.Build(type, (TypeDeclarationSyntax)attributeContext.TargetNode)
                : null);

        context.RegisterSourceOutput(
            unions.Where(static union => union is not null),
            static (production, union) => production.AddSource(union!.HintName, union.Text));
    }
}
