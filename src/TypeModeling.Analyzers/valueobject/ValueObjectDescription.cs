using System;
using Microsoft.CodeAnalysis;

namespace TypeModeling.Analyzers.ValueObject;

/// <summary>値オブジェクトの生成記述</summary>
/// <param name="Model">生成可能な値オブジェクト model</param>
/// <param name="Diagnostic">生成を妨げる宣言の診断</param>
/// <param name="Location">診断対象の位置</param>
/// <param name="MessageArguments">診断文の置換値</param>
internal sealed record ValueObjectDescription(
    ValueObjectModel? Model,
    DiagnosticDescriptor? Diagnostic,
    Location Location,
    object[] MessageArguments)
{
    /// <summary>生成可能な記述の構築</summary>
    /// <param name="model">値オブジェクト model</param>
    /// <returns>生成可能な記述</returns>
    internal static ValueObjectDescription Succeeded(ValueObjectModel model) =>
        new(model, null, Location.None, Array.Empty<object>());

    /// <summary>診断を持つ記述の構築</summary>
    /// <param name="descriptor">診断記述子</param>
    /// <param name="location">診断対象の位置</param>
    /// <param name="messageArguments">診断文の置換値</param>
    /// <returns>診断を持つ記述</returns>
    internal static ValueObjectDescription Failed(
        DiagnosticDescriptor descriptor,
        Location location,
        params object[] messageArguments) =>
        new(null, descriptor, location, messageArguments);
}
