namespace TypeModeling.Domain;

/// <summary>検証を経た基底値を包む値オブジェクトの構築定型の生成を求める標識</summary>
/// <remarks><c>Parse</c> の検証を経た構築だけを生成で許す parse-don't-validate の言語機能化</remarks>
/// <typeparam name="TUnderlying">包む基底値の型</typeparam>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ValueObjectAttribute<TUnderlying> : Attribute
{
    /// <summary>値オブジェクトが包む基底値の型</summary>
    public Type Underlying { get; } = typeof(TUnderlying);
}
