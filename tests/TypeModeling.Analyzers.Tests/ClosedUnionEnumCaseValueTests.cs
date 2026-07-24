using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ClosedTypeDiagnosticFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>enum case 値に対する値保持変換の判定</summary>
public sealed class ClosedUnionEnumCaseValueTests
{
    /// <summary>値を保つ enum cast と算術 case 値の区別</summary>
    [Test]
    public async Task Typmod001_distinguishes_value_preserving_enum_casts_from_arithmetic_case_values()
    {
        var diagnostics = await RunSuppressor("""
            using TypeModeling.Domain;

            namespace Probe;

            [ClosedUnion]
            internal enum Status
            {
                Open,
                Closed,
            }

            internal static class Handler
            {
                internal static void Converted(int status)
                {
                    switch (status)
                    {
                        case (int)Status.Open:
                            break;
                    }
                }

                internal static void Arithmetic(int status)
                {
                    switch (status)
                    {
                        case (int)Status.Open + 100:
                            break;
                    }
                }
            }
            """);

        var statements = diagnostics.Where(diagnostic => diagnostic.Id == "TYPMOD001").ToImmutableArray();
        await Assert.That(statements.Length).IsEqualTo(1);
    }

}
