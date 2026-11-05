using System.ComponentModel;

namespace TypeModeling.Domain;

/// <summary>閉じた class の直接の派生の数を metadata に残す、generator が付ける assembly の標識</summary>
/// <remarks>
/// 別の assembly からは非公開の派生が見えないので、見える派生の数がこの数より少なければ網羅に数えない。
/// C# 15 の compiler が IsClosedType 属性の DerivedTypes に派生を残すのと同じ役割を持つ。
/// </remarks>
/// <param name="closedType">閉じた class</param>
/// <param name="directSubtypeCount">閉じた class の直接の派生の数</param>
[EditorBrowsable(EditorBrowsableState.Never)]
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class ClosedSubtypeCountAttribute(Type closedType, int directSubtypeCount) : Attribute
{
    /// <summary>閉じた class</summary>
    public Type ClosedType { get; } = closedType;

    /// <summary>閉じた class の直接の派生の数</summary>
    public int DirectSubtypeCount { get; } = directSubtypeCount;
}
