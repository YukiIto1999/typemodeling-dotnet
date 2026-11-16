using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Diagnostics.Guard;

/// <summary>設定で弱めてはならない TypeModeling の診断と、重大度の比較</summary>
/// <remarks>既定で無効な TYPMOD021 は、利用側が有効にしていないだけなので守らない</remarks>
internal static class GuardedDiagnostics
{
    /// <summary>TypeModeling の診断 ID の接頭辞</summary>
    private const string IdPrefix = "TYPMOD";

    /// <summary>既定で無効な診断の ID</summary>
    private const string OptInId = "TYPMOD021";

    /// <summary>守る診断の ID と、その既定の重大度。抑止規則は warning として数える</summary>
    private static readonly ImmutableDictionary<string, DiagnosticSeverity> Defaults = Build();

    /// <summary>守る診断の ID</summary>
    internal static IEnumerable<string> Ids => Defaults.Keys;

    /// <summary>診断の ID が設定で弱めてはならない対象かの判定</summary>
    /// <param name="id">診断の ID か、末尾に * を付けた接頭辞</param>
    /// <returns>TypeModeling の診断で、既定で有効な場合に true</returns>
    internal static bool IsGuarded(string id) =>
        id.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(id, OptInId, StringComparison.OrdinalIgnoreCase);

    /// <summary>設定された報告が、診断の既定より下げているかの判定</summary>
    /// <param name="id">診断の ID か、末尾に * を付けた接頭辞</param>
    /// <param name="configured">設定された報告</param>
    /// <returns>既定より下げている場合に true。既定の報告に戻す設定と、守らない診断は false</returns>
    internal static bool IsLowered(string id, ReportDiagnostic configured)
    {
        if (configured == ReportDiagnostic.Default || !IsGuarded(id))
            return false;

        var floor = Defaults.TryGetValue(id.ToUpperInvariant(), out var severity)
            ? Rank(severity)
            : Rank(DiagnosticSeverity.Warning);
        return Rank(configured) < floor;
    }

    /// <summary>一括の重大度の設定値が、診断を表示しないものかの判定</summary>
    /// <param name="value">editorconfig の重大度の値</param>
    /// <returns>none、silent、suggestion のいずれかである場合に true</returns>
    internal static bool HidesDiagnostics(string value) =>
        value.Trim().ToLowerInvariant() is "none" or "silent" or "suggestion";

    /// <summary>報告を editorconfig の重大度の表記にする</summary>
    /// <param name="report">compiler の報告</param>
    /// <returns>none、silent、suggestion、warning、error のいずれか</returns>
    internal static string Display(ReportDiagnostic report) =>
        report switch
        {
            ReportDiagnostic.Suppress => "none",
            ReportDiagnostic.Hidden => "silent",
            ReportDiagnostic.Info => "suggestion",
            ReportDiagnostic.Warn => "warning",
            ReportDiagnostic.Error => "error",
            _ => "default",
        };

    /// <summary>報告の強さ。大きいほど利用者に強く見える</summary>
    /// <param name="report">compiler の報告</param>
    /// <returns>0 から 4 の強さ</returns>
    private static int Rank(ReportDiagnostic report) =>
        report switch
        {
            ReportDiagnostic.Suppress => 0,
            ReportDiagnostic.Hidden => 1,
            ReportDiagnostic.Info => 2,
            ReportDiagnostic.Warn => 3,
            _ => 4,
        };

    /// <summary>診断の既定の重大度の強さ</summary>
    /// <param name="severity">診断の既定の重大度</param>
    /// <returns>0 から 4 の強さ</returns>
    private static int Rank(DiagnosticSeverity severity) =>
        severity switch
        {
            DiagnosticSeverity.Hidden => 1,
            DiagnosticSeverity.Info => 2,
            DiagnosticSeverity.Warning => 3,
            _ => 4,
        };

    /// <summary>守る診断の表の構築</summary>
    /// <returns>診断の ID と既定の重大度</returns>
    private static ImmutableDictionary<string, DiagnosticSeverity> Build()
    {
        var table = ImmutableDictionary.CreateBuilder<string, DiagnosticSeverity>(
            StringComparer.Ordinal
        );
        foreach (
            var number in new[]
            {
                2,
                3,
                4,
                5,
                6,
                7,
                8,
                9,
                10,
                11,
                12,
                13,
                14,
                15,
                16,
                18,
                19,
                20,
                22,
            }
        )
            table[
                IdPrefix + number.ToString("000", System.Globalization.CultureInfo.InvariantCulture)
            ] = DiagnosticSeverity.Error;
        table["TYPMOD017"] = DiagnosticSeverity.Warning;
        foreach (var number in new[] { 101, 102, 103 })
            table[
                IdPrefix + number.ToString("000", System.Globalization.CultureInfo.InvariantCulture)
            ] = DiagnosticSeverity.Warning;
        return table.ToImmutable();
    }
}
