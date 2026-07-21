using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TypeModeling.Analyzers.ValueObject;

namespace TypeModeling.Analyzers;

/// <summary>ValueObject の標識が付いた partial record への検証付き構築定型の生成</summary>
[Generator]
public sealed class ValueObjectGenerator : IIncrementalGenerator
{
    /// <summary>属性付き宣言を生成 source へ変換する pipeline の登録</summary>
    /// <param name="context">incremental generator の初期化 context</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var descriptions = context.SyntaxProvider.ForAttributeWithMetadataName(
            ValueObjectDescriptionBuilder.AttributeMetadataName,
            predicate: static (node, _) => node is TypeDeclarationSyntax,
            transform: static (attributeContext, _) =>
                ValueObjectDescriptionBuilder.Describe(attributeContext));

        context.RegisterSourceOutput(
            descriptions.Where(static description => description is not null),
            static (production, description) =>
                ValueObjectSourceEmitter.Produce(production, description!));
    }
}
