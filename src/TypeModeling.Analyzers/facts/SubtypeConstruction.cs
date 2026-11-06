using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Facts;

/// <summary>閉じた class の構築形に対応する派生の構築形の導出</summary>
/// <remarks>C# 15 の closed の「Determining subtypes of a closed class」と同じく、基底の型引数から派生の型引数を逆算する</remarks>
internal static class SubtypeConstruction
{
    /// <summary>派生定義を閉じた class の構築形に合わせた構築</summary>
    /// <param name="subtypeDefinition">直接の派生の元定義</param>
    /// <param name="closedType">使用位置での閉じた class の型</param>
    /// <returns>構築の可否と構築形</returns>
    internal static SubtypeCandidate Construct(INamedTypeSymbol subtypeDefinition, INamedTypeSymbol closedType)
    {
        if (subtypeDefinition.BaseType is not { } baseType)
            return SubtypeCandidate.Impossible;

        if (IsNestedInGeneric(subtypeDefinition))
            return ConstructNested(subtypeDefinition, closedType);

        var variables = subtypeDefinition.TypeParameters;
        var bindings = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
        var verdict = Unify(baseType, closedType, variables, bindings);
        if (verdict != Unification.Matched)
            return verdict == Unification.Mismatched ? SubtypeCandidate.Impossible : SubtypeCandidate.Unspeakable;

        if (variables.IsEmpty)
            return SubtypeCandidate.Possible(subtypeDefinition);

        return variables.All(bindings.ContainsKey)
            ? SubtypeCandidate.Possible(subtypeDefinition.Construct([.. variables.Select(variable => bindings[variable])]))
            : SubtypeCandidate.Unspeakable;
    }

    /// <summary>generic な型に入れ子の派生の構築</summary>
    /// <param name="subtypeDefinition">入れ子の派生の元定義</param>
    /// <param name="closedType">使用位置での閉じた class の型</param>
    /// <returns>外側の型をそのまま基底にする派生なら構築形、それ以外は判定不能</returns>
    /// <remarks>外側の型を基底にする直接の派生の外側は閉じた class 自身なので、使用位置の閉じた class の入れ子として必ず名前を引ける</remarks>
    private static SubtypeCandidate ConstructNested(INamedTypeSymbol subtypeDefinition, INamedTypeSymbol closedType)
    {
        if (!SymbolEqualityComparer.Default.Equals(subtypeDefinition.BaseType, subtypeDefinition.ContainingType))
            return SubtypeCandidate.Unspeakable;

        return SubtypeCandidate.Possible(closedType.GetTypeMembers(subtypeDefinition.Name, subtypeDefinition.Arity)[0]);
    }

    /// <summary>generic な型に入れ子かの判定</summary>
    /// <param name="type">判定対象の型</param>
    /// <returns>外側の型のいずれかが generic なら true</returns>
    private static bool IsNestedInGeneric(INamedTypeSymbol type)
    {
        for (var container = type.ContainingType; container is not null; container = container.ContainingType)
        {
            if (container.IsGenericType)
                return true;
        }

        return false;
    }

    /// <summary>派生の基底の型引数と使用位置の型引数の単一化</summary>
    /// <param name="pattern">派生定義が宣言した基底側の型</param>
    /// <param name="actual">使用位置の型</param>
    /// <param name="variables">派生定義の型引数</param>
    /// <param name="bindings">確定した型引数の割当</param>
    /// <returns>単一化の結果</returns>
    private static Unification Unify(
        ITypeSymbol pattern,
        ITypeSymbol actual,
        IReadOnlyCollection<ITypeParameterSymbol> variables,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> bindings)
    {
        if (pattern is ITypeParameterSymbol variable && variables.Contains(variable, SymbolEqualityComparer.Default))
            return Bind(variable, actual, bindings);

        if (pattern is ITypeParameterSymbol || actual is ITypeParameterSymbol)
            return SymbolEqualityComparer.Default.Equals(pattern, actual) ? Unification.Matched : Unification.Undecidable;

        return (pattern, actual) switch
        {
            (INamedTypeSymbol named, INamedTypeSymbol other) => UnifyNamed(named, other, variables, bindings),
            (IArrayTypeSymbol array, IArrayTypeSymbol other) when array.Rank == other.Rank =>
                Unify(array.ElementType, other.ElementType, variables, bindings),
            _ => SymbolEqualityComparer.Default.Equals(pattern, actual) ? Unification.Matched : Unification.Mismatched,
        };
    }

    /// <summary>名前付き型どうしの型引数ごとの単一化</summary>
    /// <param name="pattern">派生定義が宣言した基底側の型</param>
    /// <param name="actual">使用位置の型</param>
    /// <param name="variables">派生定義の型引数</param>
    /// <param name="bindings">確定した型引数の割当</param>
    /// <returns>単一化の結果</returns>
    private static Unification UnifyNamed(
        INamedTypeSymbol pattern,
        INamedTypeSymbol actual,
        IReadOnlyCollection<ITypeParameterSymbol> variables,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> bindings)
    {
        if (!SymbolEqualityComparer.Default.Equals(pattern.OriginalDefinition, actual.OriginalDefinition))
            return Unification.Mismatched;

        var verdict = Unification.Matched;
        foreach (var (patternArgument, actualArgument) in pattern.TypeArguments.Zip(actual.TypeArguments, (left, right) => (left, right)))
        {
            var argumentVerdict = Unify(patternArgument, actualArgument, variables, bindings);
            if (argumentVerdict == Unification.Mismatched)
                return Unification.Mismatched;

            if (argumentVerdict == Unification.Undecidable)
                verdict = Unification.Undecidable;
        }

        return verdict;
    }

    /// <summary>派生の型引数への使用位置の型の割当</summary>
    /// <param name="variable">割り当てる派生の型引数</param>
    /// <param name="actual">使用位置の型</param>
    /// <param name="bindings">確定した型引数の割当</param>
    /// <returns>未割当か既存の割当と一致すれば一致、使用位置の型引数次第なら判定不能、それ以外は不一致</returns>
    private static Unification Bind(
        ITypeParameterSymbol variable,
        ITypeSymbol actual,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> bindings)
    {
        if (!bindings.TryGetValue(variable, out var bound))
        {
            bindings.Add(variable, actual);
            return Unification.Matched;
        }

        return Unify(bound, actual, [], bindings);
    }

    /// <summary>単一化の結果</summary>
    private enum Unification
    {
        /// <summary>割当が一つに定まった</summary>
        Matched,

        /// <summary>どの割当でも一致しない</summary>
        Mismatched,

        /// <summary>使用位置の型引数に依存して定まらない</summary>
        Undecidable,
    }
}
