using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using TypeModeling.Analyzers.Diagnostics.ClosedEnum;
using TypeModeling.Analyzers.Diagnostics.ClosedType;
using TypeModeling.Analyzers.Diagnostics.MustUse;
using TypeModeling.Analyzers.Diagnostics.ResultUsage;
using TypeModeling.Analyzers.Diagnostics.UnionType;
using TypeModeling.Analyzers.Diagnostics.UnreachableArm;
using TypeModeling.Analyzers.Suppression.Exhaustiveness;

namespace TypeModeling.Analyzers.Tests;

/// <summary>利用者へ公開する診断と抑止の ID、重大度、題、メッセージの契約</summary>
public sealed class DiagnosticContractTests
{
    /// <summary>ClosedEnumAnalyzer が公開する診断</summary>
    [Test]
    public Task ClosedEnumAnalyzer_publishes_its_diagnostics() => AssertDiagnostics(
        new ClosedEnumAnalyzer(),
        [
            "TYPMOD018 | Error | 0 に当たる member のない閉じた enum | 閉じた enum '{0}' には、既定値 0 に当たる member が要る。",
            "TYPMOD019 | Error | 閉じた enum への member に当たらない変換 | 閉じた enum '{0}' へは、名前付きの member に当たる定数だけを変換できる。",
            "TYPMOD020 | Error | 閉じた enum の member に当たらない演算 | 閉じた enum '{0}' を返す演算は、定数どうしで名前付きの member に当たる結果だけを許す。",
        ]);

    /// <summary>ClosedTypeDeclarationAnalyzer が公開する診断</summary>
    [Test]
    public Task ClosedTypeDeclarationAnalyzer_publishes_its_diagnostics() => AssertDiagnostics(
        new ClosedTypeDeclarationAnalyzer(),
        [
            "TYPMOD003 | Error | abstract でない閉じた class | 閉じた class '{0}' は abstract で宣言する。",
            "TYPMOD004 | Error | 別の assembly の閉じた class からの派生 | '{0}' は、別の assembly の閉じた class '{1}' を基底型にできない。",
            "TYPMOD005 | Error | 閉じた基底型で参照しない型引数 | '{0}' の型引数 '{1}' は、閉じた基底型 '{2}' で参照する。",
        ]);

    /// <summary>MustUseAnalyzer が公開する診断</summary>
    [Test]
    public Task MustUseAnalyzer_publishes_its_diagnostics() => AssertDiagnostics(
        new MustUseAnalyzer(),
        [
            "TYPMOD017 | Warning | 使わずに捨てた値 | '{0}' の値は使う必要がある。失敗を無視する場合は discard に代入して明示する。",
        ]);

    /// <summary>ResultUsageAnalyzer が公開する診断</summary>
    [Test]
    public Task ResultUsageAnalyzer_publishes_its_diagnostics() => AssertDiagnostics(
        new ResultUsageAnalyzer(),
        [
            "TYPMOD015 | Error | 未初期化の Result の構築 | default の Result は成功でも失敗でもない未初期化の値になる。Result.Succeeded か Result.Failed で構築する。",
            "TYPMOD016 | Error | case 型への cast による Result の取り出し | Result の Value を case 型へ cast すると、他の case で例外になる。網羅した switch 式、IsFailed、Match で取り出す。",
        ]);

    /// <summary>UnionTypeAnalyzer が公開する診断</summary>
    [Test]
    public Task UnionTypeAnalyzer_publishes_its_diagnostics() => AssertDiagnostics(
        new UnionTypeAnalyzer(),
        [
            "TYPMOD007 | Error | struct でない union 宣言 | union 宣言の形の '{0}' は partial struct で宣言する。",
            "TYPMOD008 | Error | union 宣言の instance field | union 宣言の形の '{0}' には、instance field、自動実装 property、field 形式の event を置けない。",
            "TYPMOD009 | Error | union 宣言の引数一つの公開コンストラクタ | union 宣言の形の '{0}' には、partial でない引数一つの公開コンストラクタを置けない。",
            "TYPMOD010 | Error | this 初期化子のない union 宣言のコンストラクタ | union 宣言の形の '{0}' のコンストラクタは、this 初期化子で他のコンストラクタを呼ぶ。",
            "TYPMOD011 | Error | union 宣言の union member provider | union 宣言の形の '{0}' は、union member provider の interface を使えない。",
            "TYPMOD012 | Error | union 構築子のない union 型 | union 型 '{0}' には、少なくとも一つの union 構築子が要る。",
            "TYPMOD013 | Error | Value のない union 型 | union 型 '{0}' には、公開の get を持つ object 型の instance property 'Value' が要る。",
            "TYPMOD014 | Error | partial でない型に入れ子の union 宣言 | union 宣言の形の '{0}' を入れ子にする型は、全て partial で宣言する。",
        ]);

    /// <summary>UnreachableArmAnalyzer が公開する診断</summary>
    [Test]
    public Task UnreachableArmAnalyzer_publishes_its_diagnostics() => AssertDiagnostics(
        new UnreachableArmAnalyzer(),
        [
            "TYPMOD006 | Error | 閉じた型の網羅で到達できない arm | 前の arm が閉じた型の全ての値を扱うので、この arm には到達できない。",
        ]);

    /// <summary>網羅の抑止が公開する抑止規則</summary>
    [Test]
    public async Task The_exhaustiveness_suppressor_publishes_its_suppressions()
    {
        var suppressions = new ExhaustivenessSuppressor().SupportedSuppressions
            .Select(rule => $"{rule.Id} | {rule.SuppressedDiagnosticId} | {rule.Justification}");

        await Assert.That(suppressions).IsEquivalentTo([
            "TYPMOD101 | CS8509 | 閉じた型の全ての値を扱う switch 式は網羅である。",
            "TYPMOD102 | CS8524 | 閉じた enum は名前のない値を取らないので、全ての member を扱う switch 式は網羅である。",
            "TYPMOD103 | CS8846 | 閉じた型の全ての値を when のない arm が扱う switch 式は網羅である。",
        ]);
    }

    /// <summary>診断の ID、重大度、題、メッセージ、分類、既定の有効の照合</summary>
    /// <param name="analyzer">照合する analyzer</param>
    /// <param name="expected">ID、重大度、題、メッセージの期待</param>
    /// <returns>照合の完了</returns>
    private static async Task AssertDiagnostics(DiagnosticAnalyzer analyzer, string[] expected)
    {
        var rules = analyzer.SupportedDiagnostics;
        var actual = rules.Select(rule => $"{rule.Id} | {rule.DefaultSeverity} | {rule.Title} | {rule.MessageFormat}");

        await Assert.That(actual).IsEquivalentTo(expected);
        await Assert.That(rules.All(rule => rule.Category == "TypeModeling" && rule.IsEnabledByDefault)).IsTrue();
    }
}
