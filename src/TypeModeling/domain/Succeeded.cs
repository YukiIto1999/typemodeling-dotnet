namespace TypeModeling.Domain;

/// <summary>Result の成功の case</summary>
/// <remarks>成功の型だけを持つので、失敗の型が違う Result へもそのまま変換できる</remarks>
/// <typeparam name="TValue">成功の値の型</typeparam>
/// <param name="Value">成功の値</param>
public readonly record struct Succeeded<TValue>(TValue Value);
