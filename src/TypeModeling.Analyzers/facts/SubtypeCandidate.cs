using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.Facts;

/// <summary>使用位置における直接の派生の構築の可否</summary>
internal readonly struct SubtypeCandidate
{
    /// <summary>構築の可否と構築形による初期化</summary>
    /// <param name="kind">構築の可否</param>
    /// <param name="type">構築できる場合の構築形</param>
    private SubtypeCandidate(SubtypeCandidateKind kind, INamedTypeSymbol? type)
    {
        Kind = kind;
        Type = type;
    }

    /// <summary>どの構築形も閉じた class の使用位置の型に一致しない派生</summary>
    internal static SubtypeCandidate Impossible { get; } = new(SubtypeCandidateKind.Impossible, null);

    /// <summary>使用位置の型引数によって存在が変わり、名前で網羅できない派生</summary>
    internal static SubtypeCandidate Unspeakable { get; } = new(SubtypeCandidateKind.Unspeakable, null);

    /// <summary>構築の可否</summary>
    internal SubtypeCandidateKind Kind { get; }

    /// <summary>構築できる場合の構築形</summary>
    internal INamedTypeSymbol? Type { get; }

    /// <summary>構築できる派生</summary>
    /// <param name="type">使用位置の型に合わせた構築形</param>
    /// <returns>構築できる派生の候補</returns>
    internal static SubtypeCandidate Possible(INamedTypeSymbol type) => new(SubtypeCandidateKind.Possible, type);
}

/// <summary>直接の派生の構築の可否の区分</summary>
internal enum SubtypeCandidateKind
{
    /// <summary>使用位置の型に合わせて構築できる</summary>
    Possible,

    /// <summary>使用位置の型では存在しない</summary>
    Impossible,

    /// <summary>使用位置の型引数に依存して名前で網羅できない</summary>
    Unspeakable,
}
