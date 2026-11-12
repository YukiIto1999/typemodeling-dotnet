using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Exhaustiveness;

namespace TypeModeling.Analyzers.Diagnostics.ExplicitCase;

/// <summary>switch の arm から、後から足す case を受け取るものの探索</summary>
/// <remarks>
/// 位置の対応は <see cref="PatternWalk"/> が与える経路で決め、経路が同じ位置の arm の case をまとめて数える。
/// 位置の判定は <see cref="CasePosition"/> と <see cref="CaseFamily"/> が持つ。
/// </remarks>
internal sealed class CatchAllArms
{
    /// <summary>位置を示す印。他の要素の discard と経路の文字列で区別できる</summary>
    private const string PositionMark = "^";

    /// <summary>型名を解決する switch の位置の文脈</summary>
    private readonly TypeText _typeText;

    /// <summary>経路ごとの位置。閉じた case を持たない位置は null</summary>
    private readonly Dictionary<string, CasePosition?> _positions = new();

    /// <summary>報告するかを判定する、位置に当てた pattern</summary>
    private readonly List<Candidate> _candidates = [];

    /// <summary>型名の文脈による初期化</summary>
    /// <param name="typeText">型名を解決する switch の位置の文脈</param>
    private CatchAllArms(TypeText typeText) => _typeText = typeText;

    /// <summary>後から足す case を受け取る arm の探索</summary>
    /// <param name="value">switch の入力の式</param>
    /// <param name="arms">switch の全ての arm</param>
    /// <param name="typeText">型名を解決する switch の位置の文脈</param>
    /// <returns>報告する arm</returns>
    internal static ImmutableArray<CatchAll> Find(IOperation value, ImmutableArray<MatchArm> arms, TypeText typeText)
    {
        var scan = new CatchAllArms(typeText);
        scan.RegisterScrutinee(PatternPath.Root, value.Type, value);
        foreach (var arm in arms.Where(candidate => candidate.Pattern is not null))
            scan.Scan(arm);

        return scan.Report(arms);
    }

    /// <summary>switch の入力と、組の入力の各要素の位置の登録</summary>
    /// <param name="path">位置への経路</param>
    /// <param name="type">位置の型。型のない式なら null</param>
    /// <param name="operation">位置の式。組の要素の式が分からなければ null</param>
    /// <remarks>組は式の形でなく型で辿るので、組を返す呼び出しや入れ子の組の要素も入力の位置になる</remarks>
    private void RegisterScrutinee(PatternPath path, ITypeSymbol? type, IOperation? operation)
    {
        _positions[path.Wrap(PositionMark)] = CasePosition.At(type, PositionCollector.UnionOfValue(operation), isScrutinee: true);
        if (type is not INamedTypeSymbol { IsTupleType: true } tuple)
            return;

        var elements = (operation as ITupleOperation)?.Elements;
        for (var index = 0; index < tuple.TupleElements.Length; index++)
        {
            var element = tuple.TupleElements[index];
            RegisterScrutinee(path.Element(null, index, tuple.TupleElements.Length), element.Type, elements is { } known ? known[index] : null);
        }
    }

    /// <summary>arm の pattern が当たる位置と、その位置の case の記録</summary>
    /// <param name="arm">pattern を持つ arm</param>
    private void Scan(MatchArm arm) =>
        PatternWalk.Run(arm.Pattern!, PatternPath.Root, _typeText, intoOpaque: true, visit => Record(visit, arm.Exempt));

    /// <summary>位置に当てた pattern の記録</summary>
    /// <param name="visit">巡回で訪れた pattern</param>
    /// <param name="exempt">報告しない arm の pattern なら true</param>
    private void Record(PatternVisit visit, bool exempt)
    {
        if (!visit.IsPosition)
            return;

        var key = visit.Path.Wrap(PositionMark);
        if (!_positions.TryGetValue(key, out var position))
            _positions[key] = position = CasePosition.At(visit.Pattern.InputType, null, isScrutinee: false);

        if (position is null)
            return;

        position.Note(visit.Pattern);
        if (!exempt)
            _candidates.Add(new Candidate(position, visit.Pattern, visit.Opaque));
    }

    /// <summary>記録した全ての arm の判定と、報告の収集</summary>
    /// <param name="arms">switch の全ての arm</param>
    /// <returns>報告する arm</returns>
    private ImmutableArray<CatchAll> Report(ImmutableArray<MatchArm> arms)
    {
        var reports = _candidates.Select(candidate => candidate.Report()).OfType<CatchAll>().ToList();
        foreach (var name in _positions.Values.OfType<CasePosition>()
            .Where(position => position.IsScrutinee).Select(position => position.Name).Distinct())
        {
            reports.AddRange(arms.Where(arm => arm.Pattern is null).Select(arm => new CatchAll(arm.Syntax, name)));
        }

        return [.. reports];
    }

    /// <summary>位置に当てた、報告するかを判定する pattern</summary>
    /// <param name="Position">pattern が当たる位置</param>
    /// <param name="Pattern">位置に当てた pattern の最上位</param>
    /// <param name="Opaque">not か list pattern の内側の位置なら true</param>
    private readonly record struct Candidate(CasePosition Position, IPatternOperation Pattern, bool Opaque)
    {
        /// <summary>pattern が後から足す case を受け取る場合の報告</summary>
        /// <returns>受け取る集まりがあれば報告。守る位置でなく、どの集まりも受け取らないなら null</returns>
        /// <remarks>not と list pattern の内側は受け取る範囲を確かめられないので、守る位置なら受け取るものとして扱う</remarks>
        internal CatchAll? Report()
        {
            var pattern = Pattern;
            var families = Position.Families();
            var family = Opaque
                ? families.FirstOrDefault()
                : families.FirstOrDefault(candidate => candidate.Accepts(pattern) != Acceptance.Never);
            return family is null ? null : new CatchAll(pattern.Syntax, family.Name);
        }
    }
}
