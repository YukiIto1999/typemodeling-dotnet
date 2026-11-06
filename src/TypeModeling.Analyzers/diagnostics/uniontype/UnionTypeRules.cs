using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Diagnostics.UnionType;

/// <summary>union 型と union 宣言の形の診断規則</summary>
internal static class UnionTypeRules
{
    /// <summary>struct でない union 宣言の形の診断</summary>
    /// <remarks>C# 15 の union 宣言は struct を宣言する</remarks>
    internal static readonly DiagnosticDescriptor NotStruct = new(
        id: "TYPMOD007",
        title: "struct でない union 宣言",
        messageFormat: "union 宣言の形の '{0}' は partial struct で宣言する。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>union 宣言の形の instance field の診断</summary>
    /// <remarks>C# 15 の CS9373 に対応する</remarks>
    internal static readonly DiagnosticDescriptor InstanceField = new(
        id: "TYPMOD008",
        title: "union 宣言の instance field",
        messageFormat: "union 宣言の形の '{0}' には、instance field、自動実装 property、field 形式の event を置けない。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>union 宣言の形の、引数一つの公開コンストラクタの診断</summary>
    /// <remarks>C# 15 の CS9374 に対応する</remarks>
    internal static readonly DiagnosticDescriptor SingleParameterConstructor = new(
        id: "TYPMOD009",
        title: "union 宣言の引数一つの公開コンストラクタ",
        messageFormat: "union 宣言の形の '{0}' には、partial でない引数一つの公開コンストラクタを置けない。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>union 宣言の形の、this 初期化子のないコンストラクタの診断</summary>
    /// <remarks>C# 15 の CS9375 に対応する</remarks>
    internal static readonly DiagnosticDescriptor ConstructorWithoutThis = new(
        id: "TYPMOD010",
        title: "this 初期化子のない union 宣言のコンストラクタ",
        messageFormat: "union 宣言の形の '{0}' のコンストラクタは、this 初期化子で他のコンストラクタを呼ぶ。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>union 宣言の形の union member provider の診断</summary>
    /// <remarks>C# 15 の CS9387 に対応する</remarks>
    internal static readonly DiagnosticDescriptor MemberProvider = new(
        id: "TYPMOD011",
        title: "union 宣言の union member provider",
        messageFormat: "union 宣言の形の '{0}' は、union member provider の interface を使えない。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>union 構築子のない union 型の診断</summary>
    /// <remarks>C# 15 の CS9385 に対応する</remarks>
    internal static readonly DiagnosticDescriptor MissingCaseTypes = new(
        id: "TYPMOD012",
        title: "union 構築子のない union 型",
        messageFormat: "union 型 '{0}' には、少なくとも一つの union 構築子が要る。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>Value のない union 型の診断</summary>
    /// <remarks>C# 15 の CS9386 に対応する</remarks>
    internal static readonly DiagnosticDescriptor MissingValueProperty = new(
        id: "TYPMOD013",
        title: "Value のない union 型",
        messageFormat: "union 型 '{0}' には、公開の get を持つ object 型の instance property 'Value' が要る。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>partial でない型に入れ子の union 宣言の形の診断</summary>
    internal static readonly DiagnosticDescriptor NonPartialContainer = new(
        id: "TYPMOD014",
        title: "partial でない型に入れ子の union 宣言",
        messageFormat: "union 宣言の形の '{0}' を入れ子にする型は、全て partial で宣言する。",
        category: "TypeModeling",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>対応する全ての診断規則</summary>
    internal static ImmutableArray<DiagnosticDescriptor> All { get; } =
    [
        NotStruct, InstanceField, SingleParameterConstructor, ConstructorWithoutThis,
        MemberProvider, MissingCaseTypes, MissingValueProperty, NonPartialContainer,
    ];
}
