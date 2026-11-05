[English](README.md) | [日本語](README.ja.md)

# TypeModeling

TypeModeling は、不正な状態を型で表現できなくする C# の型モデリング基盤。この文書の読み手は消費側の開発者で、導入と使い方を示す。

## 問題

素の C# では、判別共用体の網羅は既定の分岐で崩れ、検証を経ない値の構築を型で塞げない。派生の登録漏れや階層外の派生は、実行時の直列化例外になるまで見えない。

## 提供するもの

- `[Closed]` が C# 15 の `closed` 修飾子と同じ規則の閉じた階層を、`[ValueObject<TUnderlying>]` が検証を経た構築だけを許す値オブジェクトを、言語機能のように書かせる
- `Result<TValue, TFailure>` と `Unit` と `Never` が純粋な計算の成功と失敗を型に現す
- compile-time engine(TYPMOD002 と網羅の suppressor)が、判別と生成の規律を build で強制する
- [TypeModeling.Testing](./src/TypeModeling.Testing/) が、閉包と attach の設営義務を宣言だけで実行する

## 取り込み

同一 workspace に checkout し、相対参照で取り込む。バージョンは `main` の `vX.Y.Z` タグで指す。

```xml
<ItemGroup>
  <ProjectReference Include="../typemodeling-dotnet/src/TypeModeling/TypeModeling.csproj" />
  <ProjectReference Include="../typemodeling-dotnet/src/TypeModeling.Analyzers/TypeModeling.Analyzers.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

閉じた型を宣言または判別する全 project が analyzer を attach する。arch test の project は [TypeModeling.Testing](./src/TypeModeling.Testing/) も参照する。

## 使い方

### Closed

```csharp
using TypeModeling.Domain;

[Closed]
public abstract record PaymentState
{
    private PaymentState() { }
    public sealed record Pending : PaymentState;
    public sealed record Settled(DateTimeOffset At) : PaymentState;
}
```

`[Closed]` は C# 15 の `closed` 修飾子に対応する。
直接の派生は同じ assembly のどこに宣言してもよく、全ての直接の派生を扱う switch 式は pattern の形を問わず網羅になる。
C# 15 へ移るときは、属性を `closed` 修飾子に替えて `abstract` を外す。

### Closed enum

```csharp
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
TYPMOD018 は 0 に当たる member を求め、TYPMOD019 は宣言した member に当たる定数からの変換だけを許し、TYPMOD020 は enum を返す演算を宣言した member になる定数どうしだけに許す。

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
generator は、コンストラクタの本体、`Value` property、`IUnion` という C# 15 の lowering と同じ member を足し、C# 15 より前では case 型からの暗黙の変換も足す。
.NET 11 より前では、`UnionAttribute` と `IUnion` を埋め込みの型として assembly ごとに足す。
全ての case 型を扱う `Value` への switch は網羅になる。
C# 15 は生成した型を言語の union として扱うので、`pet switch { Cat cat => ... }` のように union そのものも照合できる。

### ValueObject

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
```

構築は生成された `Create` を通し、検証の結果が返る。

```csharp
Result<OrderId, OrderIdFailure> id = OrderId.Create(input);
```

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
`Map`、`Bind`、`MapFailure`、query 式、`Match`、最初の失敗で止める `Sequence`、全ての失敗を `NonEmptyList<TFailure>` に集める `Accumulate` と `Zip` が結果を組み合わせる。
内部の tag は 0 を未初期化に固定するので、`default` の結果の観測は例外を送出し、TYPMOD015 は `default` の結果を compile 時に拒否する。
TYPMOD016 は `Value` の case 型への cast を拒否し、TYPMOD017 は Rust の `#[must_use]` と同じく結果を捨てる呼び出しを警告する。
C# 15 では、`result switch { Succeeded<Order>(var order) => ... }` のように結果そのものも照合できる。

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

### analyzer の attach 検査

```csharp
using TypeModeling.Testing.Attach;

var violations = await AnalyzerAttachmentConformance.ViolationsAsync(
    repoRoot,
    new AnalyzerAttachmentRequirement(
        AnalyzerProjectPath: "vendor/typemodeling-dotnet/src/TypeModeling.Analyzers/TypeModeling.Analyzers.csproj",
        ProtectedDiagnosticIds: new HashSet<string> { "TYPMOD002" },
        ProtectedDiagnosticPrefixes: new HashSet<string> { "TYPMOD" },
        ExpectedProjectPaths: new HashSet<string> { "src/App.Domain/App.Domain.csproj" },
        ProtectedDiagnosticCategories: new HashSet<string>()),
    new ClosedTypeUsageDetector(),
    CancellationToken.None);
await Assert.That(violations).IsEmpty();
```

## 依存

本体 `src/TypeModeling/` は他のライブラリに依存せず、effectsystem-dotnet が本体を参照する。検査の実行機 `src/TypeModeling.Testing/` だけが MSBuild 評価と probe compile のために Roslyn の Microsoft.CodeAnalysis.CSharp へ依存する。

## 開発

このリポジトリは、出荷する検査を自分自身へ適用する([SelfAuditTests](./tests/TypeModeling.Testing.Tests/root/SelfAuditTests.cs))。検証は、時間の予算で分けた devenv の三つの入口で行う。

- `devenv shell verify`(2 分以内、commit の前)が、analyzer の警告をエラー扱いにした build と、runtime と analyzer の同一プロセス内のテストを実行する。build は関数の認知的複雑度(SonarAnalyzer の S3776、上限 15)も判定する
- `devenv shell verify-push`(15 分以内、push の前)が、`verify`、自己監査のテスト、push の基点から変更した行への runtime と analyzer の mutation testing を実行する。変更した行に生存または未被覆の mutant が一件でもあれば失敗する
- `devenv shell verify-full`(予算なし、gate にしない、release の前)が、`verify`、filesystem 上で MSBuild project を評価する TypeModeling.Testing のテストの全件、三つの source project 全量の mutation testing を実行し、前回の全量に無かった未検出 mutant を backlog へ追記する

mutation testing には、`devenv.yaml` で v0.4.1 の release タグの commit に固定して build した [mutation-dotnet](https://github.com/YukiIto1999/mutation-dotnet) を使い、`verify-push` ではその `--changed-lines` で変更した行の mutant だけを生成し、未検出の mutant を `changed-lines` command で取り出す。枝と commit と release の規約は [CONTRIBUTING.md](./CONTRIBUTING.md) に、版の記録は [CHANGELOG.md](./CHANGELOG.md) に置く。

## アーキテクチャの標準

アーキテクチャの標準は `/home/nixos/environment/architecture-standard` にある。
準拠の基準は、常に現在の標準本文である。
適用は、標準の README の適用の4則と利用の手順に従う。
決定の記録は git 管理外の `docs/decisions/` にある。

## ライセンス

MIT
