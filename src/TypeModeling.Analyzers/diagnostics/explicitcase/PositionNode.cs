using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Diagnostics.ExplicitCase;

/// <summary>switch の入力から部分値へ至る位置の木の節</summary>
/// <remarks>共有する値への別経路を <see cref="Overlapping"/> で照合する位置表現</remarks>
internal sealed class PositionNode
{
    /// <summary>親から部分値を取り出す位置の初期化</summary>
    /// <param name="position">閉じた case の記録か null</param>
    /// <param name="step">親から値を取り出す段か入力位置を表す既定値</param>
    /// <param name="parent">親の位置か入力位置を表す null</param>
    internal PositionNode(CasePosition? position, PositionStep step, PositionNode? parent)
    {
        Position = position;
        Step = step;
        Parent = parent;
    }

    /// <summary>この位置が持つ閉じた case の記録</summary>
    /// <value>閉じた case を持たない位置では null</value>
    internal CasePosition? Position { get; }

    /// <summary>親からこの位置の値を取り出す段</summary>
    /// <value>入力位置では既定値</value>
    internal PositionStep Step { get; }

    /// <summary>この位置の親</summary>
    /// <value>入力位置では null</value>
    private PositionNode? Parent { get; }

    /// <summary>不透明な pattern の外からの到達状態</summary>
    /// <value>入力位置か不透明な pattern の外から訪れた位置なら true</value>
    internal bool IsTransparent { get; private set; }

    /// <summary>この位置の一段下の部分値の節</summary>
    /// <value>追加した順の子の節</value>
    internal ImmutableArray<PositionNode> Children { get; private set; } = [];

    /// <summary>switch の入力の節</summary>
    /// <value>親を辿って到達する入力位置の節</value>
    private PositionNode Root => Parent?.Root ?? this;

    /// <summary>同じ節になる一段下の部分値の節</summary>
    /// <param name="step">部分値を取り出す段</param>
    /// <returns>同じ段を持つ子の節か null</returns>
    internal PositionNode? Find(PositionStep step) => Children.FirstOrDefault(child => child.Step.IsSameAs(step));

    /// <summary>一段下の部分値の節の追加</summary>
    /// <param name="position">部分値が持つ閉じた case の記録か null</param>
    /// <param name="step">部分値を取り出す段</param>
    /// <returns>追加した節</returns>
    internal PositionNode Add(CasePosition? position, PositionStep step)
    {
        var child = new PositionNode(position, step, this);
        Children = Children.Add(child);
        return child;
    }

    /// <summary>not と list pattern の外から訪れた位置としての記録</summary>
    internal void Reveal() => IsTransparent = true;

    /// <summary>別経路で同じ値を取り出しうる位置の探索</summary>
    /// <param name="conversions">型の重なりを判定する変換</param>
    /// <returns>入力からの全ての段が重なる自身以外の節</returns>
    /// <remarks>互いに重ならない派生型の経路の除外</remarks>
    internal IEnumerable<PositionNode> Overlapping(TypeConversions conversions)
    {
        var level = new[] { Root };
        foreach (var step in Steps())
            level = [.. level.SelectMany(node => node.Children.Where(child => child.Step.Overlaps(step, conversions)))];

        return level.Where(node => node != this);
    }

    /// <summary>switch の入力からこの節へ辿る段</summary>
    /// <returns>入力位置を空列とする入力に近い順の段</returns>
    private IEnumerable<PositionStep> Steps() => Parent is null ? [] : Parent.Steps().Append(Step);

    /// <summary>この位置以下で将来の case を保護する型名の列挙</summary>
    /// <returns>不透明な pattern の外から到達した保護対象の型名の列</returns>
    /// <remarks>異なる位置に現れる同じ型名の重複を許す結果</remarks>
    internal IEnumerable<string> ProtectedNames()
    {
        var below = Children.SelectMany(child => child.ProtectedNames());
        return IsTransparent && Position is { IsProtected: true } position ? below.Prepend(position.Name) : below;
    }
}
