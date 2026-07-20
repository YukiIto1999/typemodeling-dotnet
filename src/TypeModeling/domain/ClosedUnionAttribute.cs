namespace TypeModeling.Domain;

/// <summary>外部派生や追加メンバを封じた判別共用体の基底を示す標識</summary>
/// <remarks>全 variant を扱う switch 式の網羅性の保証の根拠</remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
public sealed class ClosedUnionAttribute : Attribute;
