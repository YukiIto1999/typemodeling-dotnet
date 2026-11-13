namespace TypeModeling.Analyzers.Diagnostics.ExplicitCase;

/// <summary>pattern が後から足す case の値を受け取るかの三値</summary>
/// <remarks>値の大小は、受け取る可能性の大小と同じ。or は大きい方、and は小さい方、not は Never と Always の入れ替えになる</remarks>
internal enum Acceptance
{
    /// <summary>後から足す case の値を受け取らない</summary>
    Never,

    /// <summary>後から足す case の値を受け取るかを、pattern の形から決められない</summary>
    Maybe,

    /// <summary>後から足す case の値を全て受け取る</summary>
    Always,
}
