using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TypeModeling.Testing.Attach;

/// <summary>guarded type の semantic 利用走査</summary>
public sealed class GuardedTypeUsageWalk
{
    /// <summary>名前付き guarded type の葉述語</summary>
    private readonly Func<ITypeSymbol, bool> leafPredicate;

    /// <summary>宣言 symbol の走査指定</summary>
    private readonly bool includeDeclaredTypes;

    /// <summary>delegate signature の走査指定</summary>
    private readonly bool includeDelegateSignatures;

    /// <summary>guarded type 利用走査の初期化</summary>
    /// <param name="leafPredicate">名前付き guarded type の葉述語</param>
    /// <param name="includeDeclaredTypes">宣言 symbol を走査する場合に true</param>
    /// <param name="includeDelegateSignatures">delegate signature を走査する場合に true</param>
    public GuardedTypeUsageWalk(
        Func<ITypeSymbol, bool> leafPredicate,
        bool includeDeclaredTypes,
        bool includeDelegateSignatures)
    {
        ArgumentNullException.ThrowIfNull(leafPredicate);

        this.leafPredicate = leafPredicate;
        this.includeDeclaredTypes = includeDeclaredTypes;
        this.includeDelegateSignatures = includeDelegateSignatures;
    }

    /// <summary>guarded type の利用判定</summary>
    /// <param name="compilation">検査対象 compilation</param>
    /// <returns>guarded type を使う場合に true</returns>
    [SuppressMessage("Critical Code Smell", "S3776:Cognitive Complexity of methods should not be too high",
        Justification = "S3776 の導入前からある複雑度 16 の既存違反。基線台帳 S3776-006 に記録し、15 以下へ分割した時点で抑止を外す")]
    public bool UsesGuardedFeature(Compilation compilation)
    {
        ArgumentNullException.ThrowIfNull(compilation);

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            var semanticModel = compilation.GetSemanticModel(tree);
            if (includeDeclaredTypes &&
                root.DescendantNodesAndSelf()
                    .OfType<BaseTypeDeclarationSyntax>()
                    .Select(declaration => semanticModel.GetDeclaredSymbol(declaration))
                    .OfType<INamedTypeSymbol>()
                    .Any(ContainsGuardedType))
            {
                return true;
            }

            foreach (var expression in root.DescendantNodesAndSelf().OfType<ExpressionSyntax>())
            {
                var typeInfo = semanticModel.GetTypeInfo(expression);
                if (ContainsGuardedType(typeInfo.Type) ||
                    ContainsGuardedType(typeInfo.ConvertedType))
                {
                    return true;
                }
            }

            foreach (var typeSyntax in root.DescendantNodesAndSelf().OfType<TypeSyntax>())
            {
                var typeInfo = semanticModel.GetTypeInfo(typeSyntax);
                if (ContainsGuardedType(typeInfo.Type) ||
                    ContainsGuardedType(typeInfo.ConvertedType))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>構成型に含まれる guarded type の判定</summary>
    /// <param name="type">検査対象の型</param>
    /// <returns>guarded type を含む場合に true</returns>
    private bool ContainsGuardedType(ITypeSymbol? type) =>
        type is not null && ContainsGuardedType(
            type,
            new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));

    /// <summary>訪問済み集合を使う guarded type の再帰判定</summary>
    /// <param name="type">検査対象の型</param>
    /// <param name="visited">訪問済みの型</param>
    /// <returns>guarded type を含む場合に true</returns>
    private bool ContainsGuardedType(
        ITypeSymbol type,
        HashSet<ITypeSymbol> visited)
    {
        if (!visited.Add(type))
            return false;
        if (type is IArrayTypeSymbol array)
            return ContainsGuardedType(array.ElementType, visited);
        if (type is IPointerTypeSymbol pointer)
            return ContainsGuardedType(pointer.PointedAtType, visited);
        if (type is ITypeParameterSymbol parameter)
        {
            return parameter.ConstraintTypes.Any(constraint =>
                ContainsGuardedType(constraint, visited));
        }
        if (type is not INamedTypeSymbol named)
            return false;

        if (leafPredicate(named))
            return true;
        if (named.TypeArguments.Any(argument => ContainsGuardedType(argument, visited)))
            return true;
        return includeDelegateSignatures &&
               named.TypeKind == TypeKind.Delegate &&
               named.DelegateInvokeMethod is { } invoke &&
               (ContainsGuardedType(invoke.ReturnType, visited) ||
                invoke.Parameters.Any(parameterSymbol =>
                    ContainsGuardedType(parameterSymbol.Type, visited)));
    }
}
