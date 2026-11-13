using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ExplicitCaseFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>閉じた class への switch が、後から足す派生を受け取る arm を持たないことを、診断の設定で求めること</summary>
public sealed class ExplicitCaseClosedClassTests
{
    /// <summary>後から足す派生の宣言</summary>
    private const string NewVariant = "public sealed record Refunded : PaymentState;";

    /// <summary>三つの派生を一つずつ挙げる arm</summary>
    private const string ExplicitArms =
        "PaymentState.Pending => 1, PaymentState.Settled => 2, PaymentState.Failed => 3,";

    /// <summary>診断を設定しなければ C# の言語どおりに catch-all を許し、設定すれば同じ source を拒否すること</summary>
    [Test]
    public async Task The_policy_applies_only_when_the_diagnostic_is_configured()
    {
        var source = PaymentSwitch("PaymentState.Pending => 1, _ => 0,");

        var configured = await RunClosed(source);
        var unconfigured = await RunClosed(source, enabled: false);

        await Assert.That(Flagged(configured)).IsEquivalentTo(["_"]);
        await Assert.That(Flagged(unconfigured)).IsEmpty();
        await Assert.That(CompilerErrors(configured)).IsEmpty();
        await Assert.That(CompilerErrors(unconfigured)).IsEmpty();
    }

    /// <summary>派生を挙げた後の、他の全ての派生を受け取る arm の拒否</summary>
    /// <param name="arm">挙げた派生の後に置く arm</param>
    /// <param name="flagged">拒否される pattern の source</param>
    [Test]
    [Arguments("_ => 0", "_")]
    [Arguments("var other => 0", "var other")]
    [Arguments("PaymentState other => 0", "PaymentState other")]
    [Arguments("object other => 0", "object other")]
    [Arguments("PaymentState { } => 0", "PaymentState { }")]
    [Arguments("{ Label: \"x\" } => 0", "{ Label: \"x\" }")]
    [Arguments("not PaymentState.Pending => 0", "not PaymentState.Pending")]
    [Arguments(
        "PaymentState and not PaymentState.Failed => 0",
        "PaymentState and not PaymentState.Failed"
    )]
    [Arguments("PaymentState.Settled or _ => 0", "PaymentState.Settled or _")]
    [Arguments("not { Label: \"x\" } => 0", "not { Label: \"x\" }")]
    [Arguments(
        "System.IEquatable<PaymentState> other => 0",
        "System.IEquatable<PaymentState> other"
    )]
    [Arguments("System.IDisposable other => 0", "System.IDisposable other")]
    public async Task Arms_that_accept_every_other_case_are_rejected(string arm, string flagged)
    {
        var diagnostics = await RunClosed(PaymentSwitch($"PaymentState.Pending => 1, {arm},"));

        await Assert.That(Flagged(diagnostics)).IsEquivalentTo([flagged]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>派生を挙げる型、or、and、case の中の束縛と null の許可</summary>
    [Test]
    public async Task Explicit_cases_and_finite_patterns_are_accepted()
    {
        var diagnostics = await RunClosed(
            Payment()
                + """

                internal static class Handler
                {
                    internal static int Types(PaymentState state) => state switch
                    {
                        PaymentState.Pending => 1,
                        PaymentState.Settled settled => 2,
                        PaymentState.Failed => 3,
                    };

                    internal static int Alternatives(PaymentState state) => state switch
                    {
                        PaymentState.Pending or PaymentState.Failed => 1,
                        PaymentState.Settled => 2,
                    };

                    internal static int Payloads(PaymentState state) => state switch
                    {
                        PaymentState.Pending and var pending => pending.Label.Length,
                        PaymentState.Settled { Refunded: true } => 2,
                        PaymentState.Settled(var refunded) => refunded ? 3 : 4,
                        PaymentState.Failed and { Label: "Failed" } => 5,
                        PaymentState.Failed(var reason) => reason.Length,
                    };

                    internal static int Optional(PaymentState? state) => state switch
                    {
                        null => 0,
                        PaymentState.Pending => 1,
                        PaymentState.Settled => 2,
                        PaymentState.Failed => 3,
                    };

                    internal static int Statement(PaymentState state)
                    {
                        var result = 0;
                        switch (state)
                        {
                            case PaymentState.Pending: result = 1; break;
                            case PaymentState.Settled settled when settled.Refunded: result = 2; break;
                            case PaymentState.Settled: result = 3; break;
                            case PaymentState.Failed: result = 4; break;
                        }

                        return result;
                    }
                }
                """
        );

        await Assert.That(Flagged(diagnostics)).IsEmpty();
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>派生を足したとき、挙げた arm は compiler が報せ、catch-all は報せずに受け取ること</summary>
    [Test]
    public async Task A_catch_all_absorbs_a_new_variant_that_explicit_arms_report()
    {
        const string fallbackArms = "PaymentState.Pending => 1, PaymentState.Settled => 2, _ => 0,";

        var explicitBefore = await RunClosed(PaymentSwitch(ExplicitArms));
        var explicitAfter = await RunClosed(PaymentSwitch(ExplicitArms, NewVariant));
        var fallbackAfter = await RunClosed(PaymentSwitch(fallbackArms, NewVariant));

        await Assert.That(MissingCases(explicitBefore)).IsEmpty();
        await Assert.That(Flagged(explicitBefore)).IsEmpty();
        await Assert.That(MissingCases(explicitAfter)).IsNotEmpty();
        await Assert.That(Flagged(explicitAfter)).IsEmpty();
        await Assert.That(MissingCases(fallbackAfter)).IsEmpty();
        await Assert.That(Flagged(fallbackAfter)).IsEquivalentTo(["_"]);
    }

    /// <summary>どの arm も派生を挙げない switch でも、switch の入力の位置を守ること</summary>
    [Test]
    public async Task The_switch_input_is_protected_even_when_no_arm_names_a_case()
    {
        var diagnostics = await RunClosed(
            Payment()
                + """

                internal static class Handler
                {
                    internal static int Plain(PaymentState state) => state switch { _ => 1 };

                    internal static int Bound(PaymentState state) => state switch { var current => current.Label.Length };

                    internal static int Pair(PaymentState state, bool flag) => (state, flag) switch
                    {
                        (_, true) => 1,
                        (_, false) => 2,
                    };

                    internal static int Statement(PaymentState state)
                    {
                        var result = 0;
                        switch (state)
                        {
                            default: result = 1; break;
                        }

                        return result;
                    }
                }
                """
        );

        await Assert
            .That(Flagged(diagnostics))
            .IsEquivalentTo(["_", "var current", "_", "_", "default:"]);
        await Assert.That(CompilerErrors(diagnostics)).IsEmpty();
    }

    /// <summary>switch 式で compiler が数えない guard だけを許し、足した派生の報せを失わないこと</summary>
    [Test]
    public async Task A_switch_expression_guard_is_exempt_only_where_the_compiler_still_reports_a_missing_case()
    {
        const string guardedFirst = "_ when flag => 9, " + ExplicitArms;
        const string constantTrue = ExplicitArms + " _ when true => 9,";

        var guardedBefore = await RunClosed(PaymentSwitch(guardedFirst));
        var guardedAfter = await RunClosed(PaymentSwitch(guardedFirst, NewVariant));
        var constantBefore = await RunClosed(PaymentSwitch(constantTrue));
        var constantAfter = await RunClosed(PaymentSwitch(constantTrue, NewVariant));

        await Assert.That(Flagged(guardedBefore)).IsEmpty();
        await Assert.That(Flagged(guardedAfter)).IsEmpty();
        await Assert.That(MissingCases(guardedAfter)).IsNotEmpty();
        await Assert.That(Flagged(constantBefore)).IsEquivalentTo(["_"]);
        await Assert.That(Flagged(constantAfter)).IsEquivalentTo(["_"]);
        await Assert.That(MissingCases(constantAfter)).IsEmpty();
    }

    /// <summary>switch 文は compiler が case の欠落を報せないので、guard があっても catch-all を拒否すること</summary>
    [Test]
    public async Task A_switch_statement_catch_all_is_rejected_even_with_a_guard()
    {
        const string statementArms =
            "case PaymentState.Pending: result = 1; break; case PaymentState.Settled: result = 2; break; case PaymentState.Failed: result = 3; break;";

        var guarded = await RunClosed(
            PaymentStatement(
                "case PaymentState.Pending: result = 1; break; case PaymentState other when flag: result = 2; break;"
            )
        );
        var fallback = await RunClosed(
            PaymentStatement(
                "case PaymentState.Pending: result = 1; break; default: result = 2; break;"
            )
        );
        var explicitAfter = await RunClosed(PaymentStatement(statementArms, NewVariant));

        await Assert.That(Flagged(guarded)).IsEquivalentTo(["PaymentState other"]);
        await Assert.That(Flagged(fallback)).IsEquivalentTo(["default:"]);
        await Assert.That(MissingCases(explicitAfter)).IsEmpty();
        await Assert.That(Flagged(explicitAfter)).IsEmpty();
    }
}
