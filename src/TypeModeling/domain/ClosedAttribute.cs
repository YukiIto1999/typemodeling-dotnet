namespace TypeModeling.Domain;

/// <summary>C# の closed 修飾子に対応する閉じた型の標識</summary>
/// <remarks>abstract class では直接の派生を同じ assembly に閉じ、enum では値を名前付きの member に閉じる</remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum, Inherited = false, AllowMultiple = false)]
public sealed class ClosedAttribute : Attribute;
