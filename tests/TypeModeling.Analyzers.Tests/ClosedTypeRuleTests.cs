using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.ClosedTypeDiagnosticFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>閉じた class の宣言と、到達できない arm の C# 15 と同じ規則</summary>
public sealed class ClosedTypeRuleTests
{
    /// <summary>abstract でない閉じた class の TYPMOD003</summary>
    [Test]
    public async Task A_closed_class_must_be_abstract()
    {
        var diagnostics = await Run("""
            using TypeModeling.Domain;

            [Closed]
            public class Concrete;

            [Closed]
            public abstract class Abstract;
            """);

        await Assert.That(Active(diagnostics, "TYPMOD003").Select(diagnostic => diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)))
            .IsEquivalentTo(["閉じた class 'Concrete' は abstract で宣言する。"]);
    }

    /// <summary>別の assembly の閉じた class からの直接の派生の TYPMOD004 と、間接の派生の許可</summary>
    [Test]
    public async Task Direct_subtypes_of_a_closed_class_from_another_assembly_are_rejected()
    {
        var library = Library("""
            using TypeModeling.Domain;

            [Closed]
            public abstract class Shared;
            public class Open : Shared;
            """);

        var diagnostics = await Run("""
            public sealed class Direct : Shared;
            public sealed class Indirect : Open;
            """, library);

        await Assert.That(Active(diagnostics, "TYPMOD004").Select(diagnostic => diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)))
            .IsEquivalentTo(["'Direct' は、別の assembly の閉じた class 'Shared' を基底型にできない。"]);
    }

    /// <summary>閉じた基底型で参照しない型引数の TYPMOD005</summary>
    [Test]
    public async Task Type_parameters_of_a_subtype_must_appear_in_the_closed_base()
    {
        var diagnostics = await Run("""
            using TypeModeling.Domain;

            [Closed]
            public abstract class Box<T>;
            public sealed class Kept<U> : Box<U>;
            public sealed class Wrapped<V> : Box<V[]>;
            public sealed class Dropped<W> : Box<int>;
            public sealed class Dynamic<X> : Box<dynamic>;
            """);
        var reported = Active(diagnostics, "TYPMOD005");

        await Assert.That(reported.Select(diagnostic => diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)))
            .IsEquivalentTo([
                "'Dropped' の型引数 'W' は、閉じた基底型 'Box' で参照する。",
                "'Dynamic' の型引数 'X' は、閉じた基底型 'Box' で参照する。",
            ]);
        await Assert.That(reported.Select(diagnostic => diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan)))
            .IsEquivalentTo(["W", "X"]);
    }

    /// <summary>全ての派生の後の基底型の arm の TYPMOD006 と、null を拾う discard の arm の許可</summary>
    [Test]
    public async Task A_base_type_arm_after_every_subtype_is_unreachable()
    {
        var diagnostics = await Run("""
            using TypeModeling.Domain;

            [Closed]
            public abstract record Light
            {
                private Light() { }
                public sealed record On : Light;
                public sealed record Off : Light;
            }

            internal static class Handler
            {
                internal static int Base(Light light) => light switch { Light.On => 1, Light.Off => 2, Light => 3 };
                internal static int Discard(Light light) => light switch { Light.On => 1, Light.Off => 2, _ => 3 };
                internal static int Partial(Light light) => light switch { Light.On => 1, Light => 3 };
            }
            """);

        await Assert.That(Active(diagnostics, "TYPMOD006").Select(diagnostic => diagnostic.Location.SourceTree!
                .GetText().GetSubText(diagnostic.Location.SourceSpan).ToString()))
            .IsEquivalentTo(["Light"]);
        await Assert.That(Active(diagnostics, "TYPMOD006").Length).IsEqualTo(1);
    }
}
