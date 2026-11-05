namespace TypeModeling.Domain;

/// <summary>Result の想定内の失敗の case</summary>
/// <remarks>失敗の型だけを持つので、成功の型が違う Result へもそのまま変換できる</remarks>
/// <typeparam name="TFailure">想定内の失敗の型</typeparam>
/// <param name="Failure">想定内の失敗</param>
public readonly record struct Failed<TFailure>(TFailure Failure);
