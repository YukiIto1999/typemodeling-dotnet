using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using TypeModeling.Analyzers;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ClosedTypeDiagnosticFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>閉じた型を判別する switch 文の禁止判定</summary>
public sealed class ClosedTypeSwitchStatementTests
{
    private const string ProbeSource = """
        using System.Text.Json.Serialization;
        using TypeModeling.Domain;

        namespace Probe;

        [ClosedUnion]
        internal abstract record ClosedOutcome
        {
            internal sealed record A : ClosedOutcome;
            internal sealed record B : ClosedOutcome;
        }

        [JsonPolymorphic]
        [JsonDerivedType(typeof(JsonA), "a")]
        [JsonDerivedType(typeof(JsonB), "b")]
        internal abstract record JsonOutcome;

        internal sealed record JsonA : JsonOutcome;
        internal sealed record JsonB : JsonOutcome;

        [ClosedUnion]
        internal enum Status
        {
            Open,
            Closed,
        }

        internal static class Handler
        {
            internal static void ClosedStatement(ClosedOutcome outcome)
            {
                switch (outcome)
                {
                    case ClosedOutcome.A:
                        break;
                    case ClosedOutcome.B:
                        break;
                }
            }

            internal static void JsonStatement(JsonOutcome outcome)
            {
                switch (outcome)
                {
                    case JsonA:
                        break;
                    case JsonB:
                        break;
                }
            }

            internal static void EnumStatement(Status status)
            {
                switch (status)
                {
                    case Status.Open:
                        break;
                    case Status.Closed:
                        break;
                }
            }

            internal static void ObjectWidenedStatement(ClosedOutcome outcome)
            {
                object widened = outcome;
                switch (widened)
                {
                    case ClosedOutcome.A:
                        break;
                    case ClosedOutcome.B:
                        break;
                }
            }

            internal static void BranchWidenedStatement(JsonOutcome outcome, bool useOutcome)
            {
                object widened;
                if (useOutcome)
                    widened = outcome;
                else
                    widened = "ordinary";
                switch (widened)
                {
                    case JsonA:
                        break;
                    case JsonB:
                        break;
                }
            }

            internal static void OrdinaryObjectStatement(object value)
            {
                switch (value)
                {
                    case string:
                        break;
                    case int:
                        break;
                }
            }

            internal static int ClosedExpression(ClosedOutcome outcome) => outcome switch
            {
                ClosedOutcome.A => 1,
                ClosedOutcome.B => 2,
            };

            internal static int JsonExpression(JsonOutcome outcome) => outcome switch
            {
                JsonA => 1,
                JsonB => 2,
            };
        }
        """;

    /// <summary>閉じた型を判別する switch 文の診断</summary>
    [Test]
    public async Task Typmod001_prohibits_switch_statements_on_closed_union_json_polymorphic_and_closed_enum_types()
    {
        var diagnostics = await RunSuppressor(ProbeSource);

        var statements = diagnostics.Where(diagnostic => diagnostic.Id == "TYPMOD001").ToImmutableArray();
        await Assert.That(statements.Length).IsEqualTo(5);
    }

}
