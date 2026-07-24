# typemodeling

不正な状態を型で表現できなくする、C# の型モデリング基盤。
この文書の読み手は消費側の開発者で、導入と使い方を示す。

## 問題

素の C# では、判別共用体の網羅は既定の分岐で崩れ、検証を経ない値の構築を型で塞げない。
派生の登録漏れや階層外の派生は、実行時の直列化例外になるまで見えない。

## 提供するもの

`[ClosedUnion]` が外部派生を封じた判別共用体を、`[ValueObject<TUnderlying>]` が検証を経た構築だけを許す値オブジェクトを、言語機能のように書かせる。
`Result<TValue, TFailure>` と `Unit` と `Never` が純粋な計算の成功と失敗を型に現す。
compile-time engine(TYPMOD001 と TYPMOD002 と網羅の suppressor)が、判別と生成の規律を build で強制する。
[TypeModeling.Testing](./src/TypeModeling.Testing/) が、閉包と attach の設営義務を宣言だけで実行する。

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

閉じた型を宣言または判別する全 project が analyzer を attach する。
arch test の project は [TypeModeling.Testing](./src/TypeModeling.Testing/) も参照する。

## 使い方

### ClosedUnion

```csharp
using TypeModeling.Domain;

[ClosedUnion]
public abstract record PaymentState
{
    private PaymentState() { }
    public sealed record Pending : PaymentState;
    public sealed record Settled(DateTimeOffset At) : PaymentState;
}
```

閉じた variant の判別は switch 式で書く。

### ValueObject

```csharp
using TypeModeling.Domain;

[ValueObject<Guid>]
public sealed partial record OrderId
{
    private static Result<Guid, OrderIdFailure> Parse(Guid raw) =>
        raw == Guid.Empty
            ? new Result<Guid, OrderIdFailure>.Failed(new OrderIdFailure.Empty())
            : new Result<Guid, OrderIdFailure>.Succeeded(raw);
}
```


### Result

```csharp
using TypeModeling.Domain;

Result<int, ParseFailure> parsed = ParsePort(input);
var doubled = parsed.Map(n => n * 2);
```


## 消費側の検査

消費側の test project は `TypeModeling.Testing` を参照する。
設営義務の検査は宣言だけを書いて実行する。

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
        ProtectedDiagnosticIds: new HashSet<string> { "TYPMOD001" },
        ProtectedDiagnosticPrefixes: new HashSet<string> { "TYPMOD" },
        ExpectedProjectPaths: new HashSet<string> { "src/App.Domain/App.Domain.csproj" },
        ProtectedDiagnosticCategories: new HashSet<string>()),
    new ClosedTypeUsageDetector(),
    CancellationToken.None);
await Assert.That(violations).IsEmpty();
```

## 依存

本体 `src/TypeModeling/` は他のライブラリに依存しない。
effectsystem-dotnet が本体を参照する。
検査の実行機 `src/TypeModeling.Testing/` だけが MSBuild 評価と probe compile のために Roslyn の Microsoft.CodeAnalysis.CSharp へ依存する。

restore 後の build、全 test、mutation の一括検証は `devenv shell verify` で実行する。

## 開発

このリポジトリは、出荷する検査を自分自身へ適用する([SelfAuditTests](./tests/TypeModeling.Testing.Tests/root/SelfAuditTests.cs))。
runtime 全域が mutation の対象で、基準は 100 を保つ。
build と全 test と mutation の一括検証は `devenv shell verify` で実行する。
枝と commit と release の規約は [CONTRIBUTING.md](./CONTRIBUTING.md) に、版の記録は [CHANGELOG.md](./CHANGELOG.md) に置く。
