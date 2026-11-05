using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Facts;

/// <summary>閉じた class の直接の派生定義の索引</summary>
/// <remarks>C# 15 の closed と同じく、直接の派生は閉じた型と同じ assembly にだけ存在する</remarks>
internal static class ClosedSubtypes
{
    /// <summary>compilation ごとの assembly 別の派生索引</summary>
    private static readonly ConditionalWeakTable<Compilation, AssemblyIndexes> IndexesByCompilation = new();

    /// <summary>TypeModeling の generator が直接の派生の数を残す assembly 属性の metadata 名</summary>
    private const string SubtypeCountAttributeMetadataName = "TypeModeling.Domain.ClosedSubtypeCountAttribute";

    /// <summary>閉じた class の定義から見た直接の派生定義の取得</summary>
    /// <param name="closedDefinition">閉じた class の元定義</param>
    /// <param name="compilation">解析中の compilation</param>
    /// <param name="subtypes">見える直接の派生定義</param>
    /// <returns>全ての直接の派生が見える場合に true</returns>
    /// <remarks>別の assembly の非公開の派生は metadata から見えないので、記録された派生の数と照らして欠けを判定する</remarks>
    internal static bool TryGetDirectSubtypeDefinitions(
        INamedTypeSymbol closedDefinition,
        Compilation compilation,
        out ImmutableArray<INamedTypeSymbol> subtypes)
    {
        var index = IndexesByCompilation
            .GetValue(compilation, _ => new AssemblyIndexes())
            .For(closedDefinition.ContainingAssembly);
        subtypes = index.TryGetValue(closedDefinition, out var found) ? found : [];
        return SymbolEqualityComparer.Default.Equals(closedDefinition.ContainingAssembly, compilation.Assembly) ||
            RecordedSubtypeCount(closedDefinition) == subtypes.Length;
    }

    /// <summary>assembly 内の全ての名前付き型</summary>
    /// <param name="assembly">走査する assembly</param>
    /// <returns>入れ子を含む名前付き型</returns>
    internal static IEnumerable<INamedTypeSymbol> DeclaredTypes(IAssemblySymbol assembly)
    {
        var pending = new Stack<INamespaceOrTypeSymbol>();
        pending.Push(assembly.GlobalNamespace);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (current is INamedTypeSymbol type)
                yield return type;

            foreach (var member in current.GetMembers().OfType<INamespaceOrTypeSymbol>())
                pending.Push(member);
        }
    }

    /// <summary>閉じた class の assembly が記録した直接の派生の数</summary>
    /// <param name="closedDefinition">閉じた class の元定義</param>
    /// <returns>記録された数。記録がなければ null</returns>
    private static int? RecordedSubtypeCount(INamedTypeSymbol closedDefinition) =>
        closedDefinition.ContainingAssembly.GetAttributes()
            .Where(attribute =>
                attribute.AttributeClass?.ToDisplayString() == SubtypeCountAttributeMetadataName &&
                attribute.ConstructorArguments.Length == 2 &&
                attribute.ConstructorArguments[0].Value is INamedTypeSymbol recorded &&
                SymbolEqualityComparer.Default.Equals(recorded.OriginalDefinition, closedDefinition))
            .Select(attribute => attribute.ConstructorArguments[1].Value as int?)
            .FirstOrDefault();

    /// <summary>assembly ごとに一度だけ作る、基底定義から直接の派生定義への索引</summary>
    private sealed class AssemblyIndexes
    {
        /// <summary>作成済みの索引</summary>
        private readonly ConcurrentDictionary<IAssemblySymbol, ImmutableDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>> _indexes =
            new(SymbolEqualityComparer.Default);

        /// <summary>assembly の索引の取得または作成</summary>
        /// <param name="assembly">索引を引く assembly</param>
        /// <returns>基底定義から直接の派生定義への索引</returns>
        internal ImmutableDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> For(IAssemblySymbol assembly) =>
            _indexes.GetOrAdd(assembly, Build);

        /// <summary>assembly 内の型の直接の基底による分類</summary>
        /// <param name="assembly">走査する assembly</param>
        /// <returns>基底定義から直接の派生定義への索引</returns>
        private static ImmutableDictionary<INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>> Build(IAssemblySymbol assembly) =>
            DeclaredTypes(assembly)
                .Where(type => type.BaseType is not null)
                .GroupBy<INamedTypeSymbol, INamedTypeSymbol>(type => type.BaseType!.OriginalDefinition, SymbolEqualityComparer.Default)
                .ToImmutableDictionary<IGrouping<INamedTypeSymbol, INamedTypeSymbol>, INamedTypeSymbol, ImmutableArray<INamedTypeSymbol>>(
                    group => group.Key,
                    group => group.Select(type => type.OriginalDefinition).ToImmutableArray(),
                    SymbolEqualityComparer.Default);
    }
}
