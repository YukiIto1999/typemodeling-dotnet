using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TypeModeling.Analyzers.Facts;
using static TypeModeling.Analyzers.Diagnostics.ExplicitCase.AcceptanceAlgebra;

namespace TypeModeling.Analyzers.Diagnostics.ExplicitCase;

/// <summary>下位の部分値を明示しない pattern の受理範囲の判定</summary>
/// <remarks>明示した部分値の独立した位置判定との責任分離</remarks>
internal static class PatternSilence
{
    /// <summary>一段下の部分値を明示しない受理範囲の判定</summary>
    /// <param name="pattern">位置に当てた pattern</param>
    /// <param name="part">pattern が当たる位置の一段下の部分値の節</param>
    /// <returns>部分値を明示する場合と到達しない場合を Never とする受理の度合い</returns>
    internal static Acceptance Toward(IPatternOperation pattern, PositionNode part) => pattern switch
    {
        IDiscardPatternOperation => Acceptance.Always,
        IConstantPatternOperation => Acceptance.Never,
        IDeclarationPatternOperation declaration => Reach(declaration.MatchedType, part),
        ITypePatternOperation type => Reach(type.MatchedType, part),
        IRecursivePatternOperation recursive => Writes(recursive, part) ? Acceptance.Never : Reach(recursive.MatchedType, part),
        INegatedPatternOperation negated => Writes(negated.Pattern, part) ? Acceptance.Never : Not(Toward(negated.Pattern, part)),
        IBinaryPatternOperation { OperatorKind: BinaryOperatorKind.Or } binary =>
            Or(Toward(binary.LeftPattern, part), Toward(binary.RightPattern, part)),
        IBinaryPatternOperation binary => And(Toward(binary.LeftPattern, part), Toward(binary.RightPattern, part)),
        _ => Acceptance.Maybe,
    };

    /// <summary>pattern が部分値を部分 pattern で書くかの判定</summary>
    /// <param name="pattern">判定する pattern</param>
    /// <param name="part">pattern が当たる位置の一段下の部分値の節</param>
    /// <returns>論理 pattern の内部に同じ値を取り出す段があれば true</returns>
    private static bool Writes(IPatternOperation pattern, PositionNode part) => pattern switch
    {
        IBinaryPatternOperation binary => Writes(binary.LeftPattern, part) || Writes(binary.RightPattern, part),
        INegatedPatternOperation negated => Writes(negated.Pattern, part),
        IRecursivePatternOperation recursive => PositionStep.Written(recursive).Any(step => step.Overlaps(part.Step)),
        _ => false,
    };

    /// <summary>型の判定による部分値への到達範囲</summary>
    /// <param name="matched">明示された判定対象の型か null</param>
    /// <param name="part">部分値の節</param>
    /// <returns>必ず到達する型の Always と到達しうる型の Maybe と到達しない型の Never</returns>
    /// <remarks>要素名に依存しない組の型の到達判定</remarks>
    private static Acceptance Reach(ITypeSymbol? matched, PositionNode part)
    {
        if (TypeLineage.WithoutNullable(matched) is not { } tested)
            return Acceptance.Always;

        if (TypeLineage.WithoutNullable(part.Step.Scope) is not { } scope)
            return Acceptance.Maybe;

        if (tested.SpecialType == SpecialType.System_Object || (tested.IsTupleType && scope.IsTupleType) || TypeLineage.IsAtOrBelow(scope, tested))
            return Acceptance.Always;

        return tested.TypeKind is TypeKind.Interface or TypeKind.TypeParameter || TypeLineage.IsAtOrBelow(tested, scope)
            ? Acceptance.Maybe
            : Acceptance.Never;
    }
}
