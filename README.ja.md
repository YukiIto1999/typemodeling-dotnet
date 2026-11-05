[English](README.md) | [日本語](README.ja.md)

# TypeModeling

TypeModeling は、C# 15 以降の型機能を同じ形で提供し、Rust と F# の型機能を合わせて提供する C# 14 のライブラリである。
このライブラリで書いたコードは機械的な書き換えで言語機能へ移れ、その書き換えは機能ごとに下の表が示す。
この README の読み手は、自分のプロジェクトでこのライブラリを使う開発者である。

## 機能

| 機能 | C# 15 より前 | C# 15 以降 | 移行 |
|---|---|---|---|
| 閉じた階層 | `[Closed] public abstract record Shape` | `public closed record Shape` | 属性を `closed` に替えて `abstract` を外す |
| 閉じた enum | `[Closed] public enum Color` | `public closed enum Color`(C# 16 で予定) | 属性を `closed` に替える |
| union 宣言 | `[Union] public readonly partial struct Pet` と `public partial Pet(Cat value);` | `public union Pet(Cat, Dog);` | C# 15 が生成した型を union として扱うので不要 |
| union の照合 | `pet.Value switch { Cat cat => ... }` | `pet switch { Cat cat => ... }` | `.Value` を外す |
| Result | `result.Value switch { Succeeded<T>(var value) => ... }` | `result switch { Succeeded<T>(var value) => ... }` | `.Value` を外す |
| 空でない並び | `NonEmptyList<T>` | 同じ | なし |
| 値オブジェクト | `[ValueObject<Guid>] public sealed partial record OrderId` | 同じ | なし |
| Unit と Never | `Unit`、`Never` | 同じ | なし |
| 使う必要のある値 | `[MustUse]` | 同じ | なし |

## 取り込み

同一 workspace に checkout し、相対参照で取り込む。
バージョンは `main` の `vX.Y.Z` タグで指す。

```xml
<ItemGroup>
  <ProjectReference Include="../typemodeling-dotnet/src/TypeModeling/TypeModeling.csproj" />
  <ProjectReference Include="../typemodeling-dotnet/src/TypeModeling.Analyzers/TypeModeling.Analyzers.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

網羅の判定と生成は analyzer で動くので、閉じた型、union、結果を宣言または照合する全ての project が analyzer を attach する。
arch test の project は [TypeModeling.Testing](./src/TypeModeling.Testing/) も参照する。

## 使い方

### 閉じた階層

```csharp
using TypeModeling.Domain;

[Closed]
public abstract record PaymentState
{
    private PaymentState() { }
    public sealed record Pending : PaymentState;
    public sealed record Settled(DateTimeOffset At) : PaymentState;
}

string text = state switch
{
    PaymentState.Pending => "pending",
    PaymentState.Settled(var at) => $"settled at {at}",
};
```

`[Closed]` は C# 15 の `closed` 修飾子に対応する。
直接の派生は、入れ子かどうかも sealed かどうかも問わず、同じ assembly のどこに宣言してもよい。
全ての直接の派生を扱う switch 式は、C# 15 と同じく pattern の形を問わず網羅になる。
全ての派生の後の基底型の arm は、C# 15 と同じく到達できない。
別の assembly の閉じた class は、その assembly が直接の派生の数を記録する analyzer を attach し、C# 15 と同じく全ての直接の派生が見える場合にだけ尽くされる。

### 閉じた enum

```csharp
using TypeModeling.Domain;

[Closed]
public enum Color
{
    Red,
    Green,
    Blue,
}
```

enum に付けた `[Closed]` は、C# 16 で予定されている `closed` enum に対応する。
全ての名前付きの member を扱う switch 式は網羅になる。
閉じた enum は、0 に当たる member を持ち、member に当たる定数からだけ変換でき、member になる定数どうしの演算だけで自分の型を返す。

### Union

```csharp
using System.Runtime.CompilerServices;

[Union]
public readonly partial struct Pet
{
    public partial Pet(Cat value);
    public partial Pet(Dog value);
}

Pet pet = new Cat("Tama");
string name = pet.Value switch
{
    Cat cat => cat.Name,
    Dog dog => dog.Name,
};
```

この宣言は、C# 15 の union 宣言 `public union Pet(Cat, Dog);` に対応する。
generator は、コンストラクタの本体、`Value` property、`IUnion` という C# 15 の lowering と同じ member を足し、C# 15 より前では interface でない case 型からの暗黙の変換も足す。
.NET 11 より前では、`UnionAttribute` と `IUnion` を埋め込みの型として assembly ごとに足す。
.NET 10 の compiler は C# 15 まで union 型への property pattern を拒否するので、C# 15 より前は union を `Value` で照合する。
`Value` への switch は、単独でもタプルの中でも、全ての case 型を扱えば網羅になる。

### Result

```csharp
using TypeModeling.Domain;

Result<Order, OrderFailure> Place(Cart cart)
{
    if (Price(cart).IsFailed(out var failed, out var price))
        return failed;

    return price > Limit
        ? Result.Failed<OrderFailure>(new OrderFailure.OverLimit(price))
        : Result.Succeeded(new Order(cart, price));
}

string text = Place(cart).Value switch
{
    Succeeded<Order>(var order) => order.ToString(),
    Failed<OrderFailure>(var failure) => failure.ToString(),
};
```

`Result<TValue, TFailure>` は、`Succeeded<TValue>` と `Failed<TFailure>` を case 型に持つ C# 15 の union 型である。
`Failed<TFailure>` は失敗の型だけを持つので、`IsFailed` は失敗を成功の型が違う結果へそのまま渡せる。
`Result.Succeeded` と `Result.Failed` は型引数を推論し、閉じた階層の新しい失敗は `Result.Failed<OrderFailure>(...)` のように基底型を書いて作る。
`Map`、`Bind`、`MapFailure`、query 式、`Match` が結果を組み合わせる。
`Sequence` は最初の失敗で止まり、`Accumulate` と `Zip` は全ての失敗を `NonEmptyList<TFailure>` に集める。
内部の tag は 0 を未初期化に固定するので、`default` の結果の観測は例外を送出する。

### 値オブジェクト

```csharp
using TypeModeling.Domain;

[ValueObject<Guid>]
public sealed partial record OrderId
{
    private static Result<Guid, OrderIdFailure> Parse(Guid raw) =>
        raw == Guid.Empty
            ? Result.Failed<OrderIdFailure>(new OrderIdFailure.Empty())
            : Result.Succeeded(raw);
}

Result<OrderId, OrderIdFailure> id = OrderId.Create(input);
```

generator が private のコンストラクタと `Parse` の結果を返す `Create` を足すので、`OrderId` は検証を経た後にだけ存在する。

### その他の型

`NonEmptyList<T>` は構築の時点で少なくとも一つの要素を持つ不変の並びであり、`NonEmptyList.FromSequence` は空の並びに `null` を返す。
`Unit` は値のない成功の型であり、`Never` は値を持たない型である。
型か method に付けた `[MustUse]` は、Rust の `#[must_use]` と同じくその値を捨てる呼び出しを警告にし、`Result` もこの属性を持つ。

## 診断

| ID | 重大度 | 規則 | C# の対応 |
|---|---|---|---|
| TYPMOD002 | Error | 値オブジェクトは求める署名の `Parse` を持つ | なし |
| TYPMOD003 | Error | 閉じた class は `abstract` で宣言する | `closed` は暗黙に abstract |
| TYPMOD004 | Error | 閉じた class の直接の派生は同じ assembly に置く | CS9382 |
| TYPMOD005 | Error | 閉じた class の直接の派生は全ての型引数を基底型で参照する | CS9383 |
| TYPMOD006 | Error | 閉じた型を尽くした arm の後に arm を置かない | CS8510 |
| TYPMOD007 | Error | union 宣言は partial struct にする | union 宣言は struct |
| TYPMOD008 | Error | union 宣言は instance field、自動実装 property、field 形式の event を持たない | CS9373 |
| TYPMOD009 | Error | union 宣言は partial でない引数一つの公開コンストラクタを持たない | CS9374 |
| TYPMOD010 | Error | union 宣言のコンストラクタは `this(...)` で他のコンストラクタを呼ぶ | CS9375 |
| TYPMOD011 | Error | union 宣言は union member provider を使わない | CS9387 |
| TYPMOD012 | Error | union 型は union 構築子を持つ | CS9385 |
| TYPMOD013 | Error | union 型は `object` 型の公開の `Value` を持つ | CS9386 |
| TYPMOD014 | Error | union 宣言を入れ子にする型は partial にする | なし |
| TYPMOD015 | Error | `Result` を `default` や引数なしの構築で作らない | なし |
| TYPMOD016 | Error | `Result` の `Value` を case 型へ cast しない | なし |
| TYPMOD017 | Warning | `[MustUse]` の値を式文で捨てない | Rust の `#[must_use]` |
| TYPMOD018 | Error | 閉じた enum は 0 に当たる member を持つ | closed enum |
| TYPMOD019 | Error | 閉じた enum への変換は member に当たる定数から行う | closed enum |
| TYPMOD020 | Error | 閉じた enum を返す演算は定数どうしで member を作る | closed enum |

抑止の TYPMOD101、TYPMOD102、TYPMOD103 は、C# 15 が網羅と判定する switch 式から CS8509、CS8524、CS8846 を外す。

## 消費側の検査

消費側の test project は `TypeModeling.Testing` を参照し、設営義務の検査を宣言だけで実行する。

### 閉じた階層の閉包検査

```csharp
using TypeModeling.Testing.Closure;

var excluded = new Dictionary<string, string>
{
    ["tools/CodeGen/CodeGen.csproj"] = "build 時にだけ動く生成 tool で出荷閉包の外に置く",
};
var closure = await SolutionClosureLoader.LoadAsync(
    new SolutionClosureScan(repoRoot, "App.slnx", "Debug", excluded),
    CancellationToken.None);
await Assert.That(SolutionClosureConformance.InventoryViolations(
    closure, SolutionClosureConformance.RepoProjectPaths(repoRoot), excluded)).IsEmpty();
await Assert.That(SolutionClosureConformance.ClosedHierarchyViolations(closure)).IsEmpty();
```

閉じた階層の検査は、閉じた class の直接の派生のうち閉じた class の assembly の外にあるものと、`[JsonPolymorphic]` の型の派生のうち `[JsonDerivedType]` に登録されていないものを報告する。

### analyzer の attach 検査

```csharp
using TypeModeling.Testing.Attach;

var violations = await AnalyzerAttachmentConformance.ViolationsAsync(
    repoRoot,
    new AnalyzerAttachmentRequirement(
        AnalyzerProjectPath: "vendor/typemodeling-dotnet/src/TypeModeling.Analyzers/TypeModeling.Analyzers.csproj",
        ProtectedDiagnosticIds: new HashSet<string> { "TYPMOD101", "TYPMOD102", "TYPMOD103" },
        ProtectedDiagnosticPrefixes: new HashSet<string> { "TYPMOD" },
        ExpectedProjectPaths: new HashSet<string> { "src/App.Domain/App.Domain.csproj" },
        ProtectedDiagnosticCategories: new HashSet<string>()),
    new ClosedTypeUsageDetector(),
    CancellationToken.None);
await Assert.That(violations).IsEmpty();
```

`ClosedTypeUsageDetector` は、閉じた型か union 型を使う全ての project を数える。

## 依存

本体 `src/TypeModeling/` は他のライブラリに依存しない。
`src/TypeModeling.Testing/` は、MSBuild の project を評価し、C# 14 の構文と生成 source のまま compile するために Microsoft.CodeAnalysis.CSharp 5.9.0 へ依存する。

## アーキテクチャの標準

アーキテクチャの標準は `/home/nixos/environment/architecture-standard` にある。
準拠の基準は、常に現在の標準本文である。
適用は、標準の README の適用の4則と利用の手順に従う。
現在の契約と読者向け文書の入口は、この README にある。

## ライセンス

MIT
