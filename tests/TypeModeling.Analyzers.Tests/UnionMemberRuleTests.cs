using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static TypeModeling.Analyzers.Tests.UnionFixture;

namespace TypeModeling.Analyzers.Tests;

/// <summary>union 宣言の形の member を、C# 15 の union 宣言と同じく状態の有無で判定すること</summary>
public sealed class UnionMemberRuleTests
{
    /// <summary>case 型の宣言</summary>
    private const string Cases = """
        using System.Runtime.CompilerServices;

        public sealed record Cat(string Name);
        public sealed record Dog(string Name);
        """;

    /// <summary>状態を持つ member の TYPMOD008</summary>
    /// <param name="member">union 宣言の形に置く member</param>
    [Test]
    [Arguments("public int Semi { get => field; }")]
    [Arguments("public event System.Action? Changed;")]
    public async Task Members_with_state_are_rejected(string member)
    {
        var diagnostics = await Run(Cases + $$"""
            [Union]
            public readonly partial struct Pet
            {
                public partial Pet(Cat value);
                {{member}}
            }
            """);

        await Assert.That(diagnostics.Where(diagnostic => diagnostic.Id.StartsWith("TYPMOD", StringComparison.Ordinal)).Select(diagnostic => diagnostic.Id))
            .IsEquivalentTo(["TYPMOD008"]);
    }

    /// <summary>状態を持たない member と、this 初期化子で union 構築子を呼ぶコンストラクタの許可</summary>
    /// <remarks>compiler は readonly struct に partial event の定義を許さないので、readonly を付けない</remarks>
    [Test]
    public async Task Members_without_state_are_allowed()
    {
        var diagnostics = await Run(Cases + """
            [Union]
            public partial struct Pet
            {
                public const int Legs = 4;
                public static int Count { get; }
                public partial Pet(Cat value);
                public partial Pet(Dog value);
                public int Computed => 1;
                public int Accessor { get => 1; }
                public int Block { get { return 1; } }
                public partial int Partial { get; }
                public partial int Partial { get => 2; }
                public event System.Action Custom { add { } remove { } }
                public partial event System.Action Split;
                public partial event System.Action Split { add { } remove { } }
                internal Pet(string name) : this(new Cat(name)) { }
                public Pet(string name, int unused) : this(new Dog(name)) { }
                public partial Pet(string name, bool unused);
                public partial Pet(string name, bool unused) : this(new Cat(name)) { }
            }
            """);

        await Assert.That(Errors(diagnostics)).IsEmpty();
    }

    /// <summary>union member provider を使う union 宣言の TYPMOD011</summary>
    [Test]
    public async Task Union_declarations_reject_member_providers()
    {
        var diagnostics = await Run(Cases + """
            [Union]
            public readonly partial struct Pet : Pet.IUnionMembers
            {
                public partial Pet(Cat value);

                public interface IUnionMembers;
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).Contains("TYPMOD011");
    }

    /// <summary>一部の宣言だけが partial の型に入れ子の union 宣言の TYPMOD014</summary>
    [Test]
    public async Task Every_declaration_of_every_container_must_be_partial()
    {
        var diagnostics = await Run(Cases + """
            public partial class Outer
            {
                public partial class Middle;
            }

            public partial class Outer
            {
                public class Middle
                {
                    [Union]
                    public readonly partial struct Pet
                    {
                        public partial Pet(Cat value);
                    }
                }
            }
            """);

        await Assert.That(diagnostics.Select(diagnostic => diagnostic.Id)).Contains("TYPMOD014");
    }
}
