using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Diagnostics.Guard;

/// <summary>assembly 全体を対象にして TypeModeling の診断を抑止する SuppressMessage の検出</summary>
/// <remarks>型や member に付けた局所の SuppressMessage と、Target で範囲を絞った抑止は許す</remarks>
internal static class AssemblyWideSuppressions
{
    /// <summary>SuppressMessage 属性の型名</summary>
    private const string SuppressMessageName =
        "System.Diagnostics.CodeAnalysis.SuppressMessageAttribute";

    /// <summary>assembly 全体の抑止の取り出し</summary>
    /// <param name="compilation">検査する compilation</param>
    /// <param name="cancellationToken">中止 token</param>
    /// <returns>抑止している診断の ID と、その属性の位置</returns>
    internal static IEnumerable<(string Id, Location Location)> Find(
        Compilation compilation,
        CancellationToken cancellationToken
    )
    {
        var attributes = compilation
            .Assembly.GetAttributes()
            .Concat(compilation.SourceModule.GetAttributes());
        foreach (var attribute in attributes)
        {
            if (
                attribute.AttributeClass?.ToDisplayString() != SuppressMessageName
                || attribute.ConstructorArguments.Length < 2
                || attribute.ConstructorArguments[1].Value is not string checkId
                || !IsAssemblyWide(attribute)
            )
                continue;

            var id = checkId.Split(':')[0].Trim();
            if (GuardedDiagnostics.IsGuarded(id))
                yield return (
                    id,
                    attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation()
                        ?? Location.None
                );
        }
    }

    /// <summary>Scope と Target で範囲を絞っていない抑止かの判定</summary>
    /// <param name="attribute">SuppressMessage 属性</param>
    /// <returns>assembly 全体を対象にする場合に true</returns>
    private static bool IsAssemblyWide(AttributeData attribute)
    {
        var target = Named(attribute, "Target");
        var scope = Named(attribute, "Scope");
        return string.IsNullOrEmpty(target)
            && (
                string.IsNullOrEmpty(scope)
                || string.Equals(scope, "module", StringComparison.OrdinalIgnoreCase)
            );
    }

    /// <summary>属性の名前付き引数の文字列値</summary>
    /// <param name="attribute">属性</param>
    /// <param name="name">名前付き引数の名前</param>
    /// <returns>文字列値。無ければ null</returns>
    private static string? Named(AttributeData attribute, string name) =>
        attribute.NamedArguments.FirstOrDefault(argument => argument.Key == name).Value.Value
        as string;
}
