namespace TypeModeling.Domain;

/// <summary>値を使わずに捨てる呼び出しを警告させる標識</summary>
/// <remarks>Rust の must_use に当たる。型に付けるとその型を返す呼び出しが、method に付けるとその method の呼び出しが対象になる</remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Method,
    Inherited = false,
    AllowMultiple = false)]
public sealed class MustUseAttribute : Attribute;
