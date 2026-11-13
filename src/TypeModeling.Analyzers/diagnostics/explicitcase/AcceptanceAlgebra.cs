using System;

namespace TypeModeling.Analyzers.Diagnostics.ExplicitCase;

/// <summary>受け取る度合いの三値の論理</summary>
/// <remarks>Never・Maybe・Always の順序による受理範囲の合成</remarks>
internal static class AcceptanceAlgebra
{
    /// <summary>三値の否定</summary>
    /// <param name="acceptance">否定する度合い</param>
    /// <returns>Maybe を保つ Never と Always の反転結果</returns>
    internal static Acceptance Not(Acceptance acceptance) =>
        acceptance switch
        {
            Acceptance.Never => Acceptance.Always,
            Acceptance.Always => Acceptance.Never,
            _ => Acceptance.Maybe,
        };

    /// <summary>三値の or</summary>
    /// <param name="left">左の度合い</param>
    /// <param name="right">右の度合い</param>
    /// <returns>受け取る可能性の大きい方</returns>
    internal static Acceptance Or(Acceptance left, Acceptance right) =>
        (Acceptance)Math.Max((int)left, (int)right);

    /// <summary>三値の and</summary>
    /// <param name="left">左の度合い</param>
    /// <param name="right">右の度合い</param>
    /// <returns>受け取る可能性の小さい方</returns>
    internal static Acceptance And(Acceptance left, Acceptance right) =>
        (Acceptance)Math.Min((int)left, (int)right);
}
