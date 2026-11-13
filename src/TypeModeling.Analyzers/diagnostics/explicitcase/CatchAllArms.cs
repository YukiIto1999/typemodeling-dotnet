using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Exhaustiveness;
using TypeModeling.Analyzers.Facts;

namespace TypeModeling.Analyzers.Diagnostics.ExplicitCase;

/// <summary>switch の arm から、後から足す case を受け取るものの探索</summary>
/// <remarks>明示した位置と暗黙に受理する下位の位置を対象とする将来の case の保護</remarks>
internal sealed class CatchAllArms
{
    /// <summary>型名を解決する switch の位置の文脈</summary>
    private readonly TypeText _typeText;

    /// <summary>型の重なりを判定する変換</summary>
    private readonly TypeConversions _conversions;

    /// <summary>switch の入力の位置の節</summary>
    private readonly PositionNode _root;

    /// <summary>最上位の部分 pattern ごとの位置の節</summary>
    private readonly Dictionary<IPatternOperation, PositionNode> _tops = new();

    /// <summary>報告するかを判定する、位置に当てた pattern</summary>
    private readonly List<Candidate> _candidates = [];

    /// <summary>下位の位置の暗黙の受理を判定する pattern</summary>
    private readonly List<Covering> _coverings = [];

    /// <summary>入力位置を起点とする判定の初期化</summary>
    /// <param name="typeText">型名を解決する switch の位置の文脈</param>
    /// <param name="conversions">型の重なりを判定する変換</param>
    /// <param name="root">switch の入力の位置の節</param>
    private CatchAllArms(TypeText typeText, TypeConversions conversions, PositionNode root)
    {
        _typeText = typeText;
        _conversions = conversions;
        _root = root;
    }

    /// <summary>後から足す case を受け取る arm の探索</summary>
    /// <param name="value">switch の入力の式</param>
    /// <param name="arms">switch の全ての arm</param>
    /// <param name="typeText">型名を解決する switch の位置の文脈</param>
    /// <param name="conversions">型の重なりを判定する変換</param>
    /// <returns>報告する arm</returns>
    internal static ImmutableArray<CatchAll> Find(
        IOperation value,
        ImmutableArray<MatchArm> arms,
        TypeText typeText,
        TypeConversions conversions
    )
    {
        var scan = new CatchAllArms(
            typeText,
            conversions,
            new PositionNode(ScrutineePosition(value.Type, value), default, null)
        );
        RegisterScrutinee(scan._root, value.Type, value);
        foreach (var arm in arms.Where(candidate => candidate.Pattern is not null))
            scan.Scan(arm);

        return scan.Report(arms);
    }

    /// <summary>入力から取り出す位置の閉じた case の特定</summary>
    /// <param name="type">判定対象の型か型を持たない式を表す null</param>
    /// <param name="operation">判定対象の式か不明な組要素を表す null</param>
    /// <returns>閉じた case を持つ位置か null</returns>
    private static CasePosition? ScrutineePosition(ITypeSymbol? type, IOperation? operation) =>
        CasePosition.At(type, PositionCollector.UnionOfValue(operation), isScrutinee: true);

    /// <summary>switch の入力と、組の入力の各要素の位置の登録</summary>
    /// <param name="node">位置の節</param>
    /// <param name="type">位置の型。型のない式なら null</param>
    /// <param name="operation">位置の式。組の要素の式が分からなければ null</param>
    /// <remarks>組は式の形でなく型で辿るので、組を返す呼び出しや入れ子の組の要素も入力の位置になる</remarks>
    private static void RegisterScrutinee(
        PositionNode node,
        ITypeSymbol? type,
        IOperation? operation
    )
    {
        node.Reveal();

        if (type is not INamedTypeSymbol { IsTupleType: true } tuple)
            return;

        var elements = (operation as ITupleOperation)?.Elements;
        for (var index = 0; index < tuple.TupleElements.Length; index++)
        {
            var element = tuple.TupleElements[index];
            var elementOperation = elements is { } known ? known[index] : null;
            var child = node.Add(
                ScrutineePosition(element.Type, elementOperation),
                PositionStep.TupleElement(index, tuple)
            );
            RegisterScrutinee(child, element.Type, elementOperation);
        }
    }

    /// <summary>arm の pattern が当たる位置と、その位置の case の記録</summary>
    /// <param name="arm">pattern を持つ arm</param>
    private void Scan(MatchArm arm) =>
        PatternWalk.Run(
            arm.Pattern!,
            PatternPath.Root,
            _typeText,
            intoOpaque: true,
            visit => Record(visit, arm.Exempt)
        );

    /// <summary>位置に当てた pattern の記録</summary>
    /// <param name="visit">巡回で訪れた pattern</param>
    /// <param name="exempt">報告しない arm の pattern なら true</param>
    /// <remarks>受理範囲を確定できない不透明な pattern の下位位置判定からの除外</remarks>
    private void Record(PatternVisit visit, bool exempt)
    {
        if (!visit.IsPosition)
            return;

        var node = NodeAt(visit.Pattern);
        if (!visit.Opaque)
            node.Reveal();

        if (!exempt && !visit.Opaque)
            _coverings.Add(new Covering(node, visit.Pattern, _conversions));

        if (node.Position is not { } position)
            return;

        position.Note(visit.Pattern);
        if (!exempt)
            _candidates.Add(
                new Candidate(node, position, visit.Pattern, visit.Opaque, _conversions)
            );
    }

    /// <summary>pattern が当たる位置の節</summary>
    /// <param name="pattern">部分値に当てた pattern の最上位</param>
    /// <returns>必要なら親の下に追加した対応する位置の節</returns>
    private PositionNode NodeAt(IPatternOperation pattern)
    {
        if (pattern.Parent is ISlicePatternOperation slice)
            return Remember(pattern, _tops[slice]);

        if (PositionStep.Of(pattern) is not { } origin)
            return Remember(pattern, _root);

        var parent = _tops[TopOf(origin.Owner)];
        return Remember(
            pattern,
            parent.Find(origin.Step)
                ?? parent.Add(
                    CasePosition.At(pattern.InputType, null, isScrutinee: false),
                    origin.Step
                )
        );
    }

    /// <summary>位置に当てた pattern の最上位と節の対応の記録</summary>
    /// <param name="top">位置に当てた pattern の最上位</param>
    /// <param name="node">pattern が当たる位置の節</param>
    /// <returns>記録した節</returns>
    private PositionNode Remember(IPatternOperation top, PositionNode node)
    {
        _tops[top] = node;
        return node;
    }

    /// <summary>同じ位置を判定する最上位の pattern の特定</summary>
    /// <param name="pattern">recursive pattern か list pattern</param>
    /// <returns>論理 pattern の外側にある位置の最上位の pattern</returns>
    private static IPatternOperation TopOf(IPatternOperation pattern)
    {
        var top = pattern;
        while (
            top.Parent
                is (IBinaryPatternOperation or INegatedPatternOperation)
                    and IPatternOperation parent
        )
            top = parent;

        return top;
    }

    /// <summary>記録した arm の判定に基づく報告の収集</summary>
    /// <param name="arms">switch の全ての arm</param>
    /// <returns>報告する arm</returns>
    /// <remarks>入力と保護された下位の位置が持つ閉じた型ごとの default の重複しない報告</remarks>
    private ImmutableArray<CatchAll> Report(ImmutableArray<MatchArm> arms)
    {
        var reports = _candidates
            .Select(candidate => candidate.Report())
            .OfType<CatchAll>()
            .Concat(_coverings.SelectMany(covering => covering.Report()))
            .ToList();
        foreach (var name in _root.ProtectedNames().Distinct())
            reports.AddRange(
                arms.Where(arm => arm.Pattern is null).Select(arm => new CatchAll(arm.Syntax, name))
            );

        return [.. reports.Distinct()];
    }

    /// <summary>明示した位置の将来の case の受理を判定する候補</summary>
    /// <param name="Node">pattern が当たる位置の節</param>
    /// <param name="Position">pattern が当たる位置</param>
    /// <param name="Pattern">位置に当てた pattern の最上位</param>
    /// <param name="Opaque">not か list pattern の内側の位置なら true</param>
    /// <param name="Conversions">型の重なりを判定する変換</param>
    private readonly record struct Candidate(
        PositionNode Node,
        CasePosition Position,
        IPatternOperation Pattern,
        bool Opaque,
        TypeConversions Conversions
    )
    {
        /// <summary>pattern が後から足す case を受け取る場合の報告</summary>
        /// <returns>保護対象の将来の case を受理する pattern の報告か null</returns>
        /// <remarks>別経路で共有する値の保護を含む不透明な pattern の保守的な判定</remarks>
        internal CatchAll? Report()
        {
            var pattern = Pattern;
            var families = Position.Families(
                Node.Overlapping(Conversions).Select(peer => peer.Position).OfType<CasePosition>()
            );
            var family = Opaque
                ? families.FirstOrDefault()
                : families.FirstOrDefault(candidate =>
                    candidate.Accepts(pattern) != Acceptance.Never
                );
            return family is null ? null : new CatchAll(pattern.Syntax, family.Name);
        }
    }

    /// <summary>下位の位置を暗黙に受理する pattern の判定候補</summary>
    /// <param name="Node">pattern が当たる位置の節</param>
    /// <param name="Pattern">位置に当てた pattern の最上位</param>
    /// <param name="Conversions">型の重なりを判定する変換</param>
    private readonly record struct Covering(
        PositionNode Node,
        IPatternOperation Pattern,
        TypeConversions Conversions
    )
    {
        /// <summary>暗黙に受理される保護対象の位置の報告</summary>
        /// <returns>この pattern の構文に対応付けた保護対象の型ごとの報告</returns>
        /// <remarks>同じ値を取り出す別経路の下位位置を含む判定</remarks>
        internal IEnumerable<CatchAll> Report()
        {
            var pattern = Pattern;
            var conversions = Conversions;
            return Node.Overlapping(conversions)
                .Prepend(Node)
                .SelectMany(owner => owner.Children)
                .Where(part =>
                    PatternSilence.Toward(pattern, part, conversions) != Acceptance.Never
                )
                .SelectMany(part => part.ProtectedNames())
                .Distinct()
                .Select(name => new CatchAll(pattern.Syntax, name));
        }
    }
}
