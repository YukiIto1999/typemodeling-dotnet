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

同一 workspace に checkout する。
バージョンは `main` の `vX.Y.Z` タグで指す。

repository の全ての project を覆う `Directory.Build.props` から、analysis props を 1 か所で import する。

```xml
<Project>
  <Import Project="../typemodeling-dotnet/TypeModeling.Analysis.props" />
</Project>
```

この import は、その `Directory.Build.props` の下にある全ての C# project に analyzer を届けるので、project ごとに analyzer への参照を書かない。
網羅の判定、生成、全ての診断は analyzer で動く。
ライブラリを使う project は、実行時のライブラリを従来どおり参照する。

```xml
<ItemGroup>
  <ProjectReference Include="../typemodeling-dotnet/src/TypeModeling/TypeModeling.csproj" />
</ItemGroup>
```

analysis が欠けるか弱められると、build は error で失敗する。

| ID | 原因 |
|---|---|
| TYPMOD201 | analyzer の project が checkout に無い |
| TYPMOD202 | `RunAnalyzers` が `false` か、`RunAnalyzers` が `true` でないまま `RunAnalyzersDuringBuild` が `false` である |
| TYPMOD203 | analyzer が project の analyzer に含まれない。たとえば `ProjectReference` や `Analyzer` の item を外している |
| TYPMOD022 | TypeModeling の診断を弱める設定がある(下の節) |
| TYPMOD204 | solution の C# project か、solution の project が参照する C# project が props を取り込んでいない(下の説明) |

build は、build する構成だけを判定する。`Release` だけにある欠陥は、`Release` を build した時に見つかる。いずれの project の `Release` の build も、`Release` で analyzer が attach され、止められず、弱められていない場合にだけ通る。出荷する構成は、それぞれ build する。

`Directory.Build.props` を取り込まない project(SDK の props より前か global property で `ImportDirectoryBuildProps` を `false` にしたもの)や、最も近い `Directory.Build.props` が親を import しない project には、この import が届かず、その project 自身は検出できない。
solution の build で検出するには、solution の隣に `Directory.Solution.targets` を置いて、solution の検査を import する。

```xml
<Project>
  <Import Project="../typemodeling-dotnet/TypeModeling.Solution.targets" />
</Project>
```

solution の `dotnet build` は、何かを build する前に、props だけが定義する `TypeModelingAnalysisState` の target を持たない C# project を挙げて、TYPMOD204 で失敗する。
検査は solution に載っている C# project から始め、取り込みが届いた project の `ProjectReference` を、project が build する target framework ごとにたどる。
solution に載せていなくても、載っている project が直接または他の project を経て参照する project は検査され、エラーはその project を参照元と合わせて挙げる。
analyzer だけを届ける参照(`OutputItemType="Analyzer"`)はたどらない。
solution でなく単独の `.csproj` を build した場合と、載っている project のどれからも参照されない project は検査されない。
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
内部の tag は 0 を未初期化に固定する。
`HasValue` だけが未初期化でも安全に使える初期化状態の問い合わせであり、`default` の結果には `false` を返す。
この `false` は業務上の失敗や payload の欠如を表さない。
payload や case の取り出しと意味的な観測（`Value`、両方の `TryGetValue`、`IsFailed`、`Match`、等価比較、ハッシュ値、文字列変換）は、未初期化の結果に対して `InvalidOperationException` を送出する。
等価比較は case が違っても両辺の初期化を確認し、未初期化の結果は `null` や無関係な型の object との比較も拒否する。
型が許す `null` payload で構築した成功や失敗は初期化済みであり、それぞれの case を保つ。

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

generator は get だけの `Value` と private のコンストラクタと、`Parse` の結果を返す `Create` を足す。
`Create` は `Parse` が返した値を保つので、`Parse` は入力を正規化できる。
宣言の外のコードが `OrderId` を得る道は `Create` だけであり、`id with { }` は検証を経た値を変えずに複製する。
TYPMOD023 がこの性質を保つ。これは analyzer であり、別の generator が partial に足した code を含む最終の compilation を検査する。record は基底型のない `sealed partial record` にし、generator が出力する private のコンストラクタを除く全てのコンストラクタ（primary、copy、private を含む）、二つ目の `Value` という名前の member、コードが書き込める field、property、indexer（`set`、`init`、`readonly` でない field）を拒否する。
別の generator が足した copy コンストラクタも、`id with { }` が `new` を経ずに呼ぶので、この理由で拒否する。
TYPMOD024 は、別の generator が出した code や生成 code と印を付けた code を含め、source に書いた値オブジェクトの `new` を全て拒否する。`new OrderId(...)`、target-typed な `new(...)`、record の lambda や入れ子の型の中のどれでも同じなので、`public static OrderId Empty => new(Guid.Empty)` は compile できない。
許すのは生成した `Create` の中の `new` だけであり、analyzer はその署名 `public static Result<OrderId, TFailure> Create(Guid raw)` で見つける。この署名は record の他の宣言が繰り返せない。生成したコンストラクタも同じように署名で見つけ、TYPMOD023 は生成した `Value` を、その名前の唯一の member として許す。
reflection、serializer、unsafe code は未初期化の instance を作れる。

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
| TYPMOD021 | Error(既定で無効) | 閉じた型か union の `Value` への switch は case を一つずつ挙げ、後から足す case を受け取る arm を置かない | なし |
| TYPMOD022 | Error | TypeModeling の診断の重大度を下げる設定や、診断を抑止する設定を置かない | なし |
| TYPMOD023 | Error | 値オブジェクトは、基底型のない sealed partial record で、コンストラクタと `Value` と書き込める field、property、indexer を宣言しない | なし |
| TYPMOD024 | Error | 値オブジェクトは source で `new` せず、`Create` と `with` の複製だけで作る | なし |

抑止の TYPMOD101、TYPMOD102、TYPMOD103 は、C# 15 が網羅と判定する switch 式から CS8509、CS8524、CS8846 を外す。

### 弱められた診断

TYPMOD022 は、TypeModeling の診断を既定の重大度より下げる設定や、診断を隠す設定を報告する。
診断を切って build を通すことはできない。
compiler が解釈しないので効かない設定、たとえば `dotnet_diagnostic.TYPMOD*.severity` の wildcard は報告せず、それが指す診断は error のままである。
この診断は `NotConfigurable` なので、`NoWarn`、`.editorconfig`、ruleset、`#pragma` では抑止できない。
次の設定を報告する。

- `.editorconfig` や global config の `dotnet_diagnostic.<id>.severity` が診断を下げる設定。たとえば error の診断を `none`、`silent`、`suggestion`、`warning` にする。
- `dotnet_analyzer_diagnostic.severity` と `dotnet_analyzer_diagnostic.category-TypeModeling.severity` を `none`、`silent`、`suggestion` にする設定。
- 診断を抑止するか下げる `NoWarn` などの compiler の設定。
- 同じ file の中で `restore` しない、TypeModeling の診断の `#pragma warning disable` と、診断を指定しない `#pragma warning disable`。
- TypeModeling の診断を assembly 全体で抑止する `SuppressMessage`。

既定で無効な TYPMOD021 は守らない。
assembly 全体を対象にして TYPMOD022 自身を挙げる `SuppressMessage` は、compiler が全ての診断に適用するので、この診断も抑止する。

### case の明示

C# は閉じた型への switch の末尾に catch-all の arm を置くことを許し、TypeModeling は既定でこの挙動を変えない。
catch-all の arm は後から足した case も受け取るので、compiler は switch の case の欠落を報せなくなる。
TYPMOD021 はその arm を拒否する診断であり、既定で無効で、標準の診断設定で有効にする。
次は、プロジェクトが取り込む `.globalconfig` の例である。

```ini
is_global = true
dotnet_diagnostic.TYPMOD021.severity = error
```

TYPMOD021 は switch 式と switch 文を検査する。
switch の入力と、組の入力の要素は、型が閉じた class、閉じた enum、閉じた class に制約された型引数、`Result` のような union の `Value` のどれかなら、守る位置になる。
守る入力は、どの arm も case を挙げていなくても検査するので、`state switch { _ => 1 }` と `default:` は拒否される。
組への switch の `default:` は、一つの要素だけでなく、隠してしまう守る型の集まりをそれぞれ重複なく診断する。
case の中の位置、たとえば `Succeeded<T>(var value)` の中身や case の property は、どれかの arm がその位置の case を挙げて分岐するときだけ検査するので、switch が分岐しない中身は束縛しても discard してもよい。
pattern が下の守る位置を書かないときは、その位置でも後から足す case を受け取るので、その pattern で arm が拒否される。
守る要素を持つ組の入力への `_` と `var pair`、別の arm が `Done` の中身で分岐しているときの `Done` と `Done { }` が該当し、隠す守る型ごとに一度診断する。
`default:` は `_` と同じに判定するので、守らない入力でも、他の case が分岐した守る位置を隠すときは拒否され、隠す守る型の集まりを一つずつ重複なく診断する。
一つの位置で閉じた型の階層が入れ子になっている場合は、`default:` が case の明示した各階層を診断する。
位置を書く arm、たとえば `Done(var state)` はその位置で判定し、case に当たらない arm、たとえば `Skipped` は許される。
`Base` や `IDisposable` のように型だけを調べる arm は、他の arm が分岐した case の値が、その型の値でもありうるかで判定する。
判定には、C# の同一変換、参照変換、boxing 変換、unboxing 変換を、両方の向きで使う。
`Covariant` が共変の `IOut<T>` の `IOut<string>` を実装するとき、variance も数えるので、`case Covariant { State: ... }` の後の `case IOut<object>` は拒否される。
sealed でない class と interface は同じ値になりうる。
interface 同士も同じ値になりうる。
sealed な class、値型、配列は、変換できない interface と同じ値にならないので、その arm は許される。
数値変換とユーザー定義変換は数えず、型引数はどの型とも同じ値になりうるものとして扱う。
pattern は、同じ値を読むなら書き方によらず同じ位置になる。
組の要素は、位置、要素名、`Item1` のどれで書いても一つの位置になる。
派生の型が継承または override した property は、派生の型で書いても基底の型で書いても一つの位置になり、`{ A.B: p }` と `{ A: { B: p } }` は同じ位置を指す。
compiler が同じコンパイルで作った record の `Deconstruct` の引数は、同じ名前の property と一つの位置になる。
継承で重ならない二つの派生の型で書いた同じ property は、型ごとに別の位置になるので、一方の型で case を挙げても他方の型は守られない。
interface の property と、sealed な class か値型がそれを実装する public な property は、書く順によらず一つの位置になる。
sealed でない class は派生が interface を再実装でき、public な property は明示実装と別の値を返しうるので、それらの property は別の位置のままで、一方を書いて分岐中の他方を書かない arm は拒否される。
派生の型が `new` で隠した property、自分で書いた `Deconstruct`、拡張 `Deconstruct` は別の位置なので、一方を書いて分岐中の他方を書かない arm は拒否される。
別の assembly の record の `Deconstruct` も、引数の名前や `CompilerGenerated` が property と一致しても、compiler が作ったか返す値かを metadata から確かめられないので、同じ扱いになる。
そのような record への switch は、`Lamp(Signal.Red, _)` か `{ Light: Signal.Red }` のどちらか一方の書き方で各位置を書く必要があり、二つの書き方を混ぜた arm は拒否される。
pattern が後から足す case を受け取りうる arm は拒否される。
具体的には、`_`、`var x`、型そのもの、その基底型、`object`、enum への relational pattern、case への `not`、case を挙げない property pattern と positional pattern が、単独でも `or` と `and` の組合せでも該当する。
case を挙げる pattern は、`A or B`、`A and { ... }`、`Settled(var at)`、`null` のように許される。
閉じた class を含む閉じた class は各段で数えるので、`Circle => ..., Rect.Square => ..., Rect => ...` では最後の arm が拒否される。
定数 `true` でない `when` を持つ switch 式の arm は、compiler が網羅に数えず case の欠落を報せ続けるので許される。
switch 文には compiler のその検査がないので、guard を持つ catch-all の label も拒否される。
`not` の内側と list pattern の内側にある守る位置、および守る位置に当てた list pattern は、受け取る範囲を確かめられないので、switch がその位置で分岐するときは拒否される。
そのような case は、`not` と list pattern の外で肯定形に書く。
この診断は `is` 式と `if` の連なりを検査せず、union は switch の入力が `Value` の式そのものであるときだけ認識する。

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

loader は、各 project の file に書かれた `ProjectReference` を読み、`Include` の `$(Name)` を、その project で MSBuild が評価した property の値に置き換える。
`Directory.Build.props` が設定する property を使う参照も閉包に入り、contract-generator が出す project の `$(TypeModelingProjectPath)` がそれに当たる。
値が空の property と、単純な property でない式は、閉包から project が漏れるので拒否する。

## 依存

本体 `src/TypeModeling/` と `src/TypeModeling.Testing/` は他のライブラリに依存しない。

## アーキテクチャの標準

アーキテクチャの標準は `/home/nixos/environment/architecture-standard` にある。
準拠の基準は、常に現在の標準本文である。
適用は、標準の README の適用の4則と利用の手順に従う。
現在の契約と読者向け文書の入口は、この README にある。

## ライセンス

MIT
