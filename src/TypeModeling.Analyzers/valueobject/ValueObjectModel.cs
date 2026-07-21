namespace TypeModeling.Analyzers.ValueObject;

/// <summary>適合済み宣言から抽出した source 生成 model</summary>
/// <param name="Namespace">生成先 namespace</param>
/// <param name="TypeName">生成対象の型名</param>
/// <param name="Underlying">基底型名</param>
/// <param name="Failure">失敗型名</param>
/// <param name="UnderlyingIsValueType">基底型の値型判定</param>
internal sealed record ValueObjectModel(
    string? Namespace,
    string TypeName,
    string Underlying,
    string Failure,
    bool UnderlyingIsValueType);
