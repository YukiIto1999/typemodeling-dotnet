namespace TypeModeling.Domain;

/// <summary>構築できないことで到達不能と不在を型に現す無人の型</summary>
/// <remarks>起こり得ない型引数の位置に置く標識</remarks>
public sealed class Never
{
    /// <summary>インスタンス生成の拒否</summary>
    private Never() => throw new InvalidOperationException("Never は構築できない。");
}
