[English](README.md) | [日本語](README.ja.md)

# TypeModeling

TypeModeling is a C# type-modeling foundation that makes invalid states unrepresentable. This document is for consuming developers and covers installation and usage.

## Problem

In plain C#, exhaustive matching over a discriminated union collapses through the default branch, and nothing stops values from being constructed without validation. A missing registration or an out-of-hierarchy derivation stays invisible until it becomes a serialization exception at run time.

## What it provides

- `[Closed]` writes closed hierarchies with the rules of the C# 15 `closed` modifier, and `[ValueObject<TUnderlying>]` writes value objects that only allow validated construction — both close to a language feature.
- `Result<TValue, TFailure>`, `Unit`, and `Never` express success and failure of pure computation in types.
- The compile-time engine (TYPMOD002 and the exhaustiveness suppressor) enforces the discrimination and construction discipline at build time.
- [TypeModeling.Testing](./src/TypeModeling.Testing/) executes closure and attach obligations from declarations alone.

## Installation

Check out the repository into the same workspace and reference it by relative path. Pin the version with a `vX.Y.Z` tag on `main`.

```xml
<ItemGroup>
  <ProjectReference Include="../typemodeling-dotnet/src/TypeModeling/TypeModeling.csproj" />
  <ProjectReference Include="../typemodeling-dotnet/src/TypeModeling.Analyzers/TypeModeling.Analyzers.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

Every project that declares or discriminates closed types attaches the analyzer. Architecture-test projects also reference [TypeModeling.Testing](./src/TypeModeling.Testing/).

## Usage

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

`[Closed]` corresponds to the C# 15 `closed` modifier.
Direct subtypes may be declared anywhere in the same assembly, and a switch expression that handles every direct subtype is exhaustive whatever the shape of its patterns.
To migrate to C# 15, replace the attribute with the `closed` modifier and remove `abstract`.

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

`[Closed]` on an enum corresponds to the `closed` enum planned for C# 16.
A switch expression that handles every named member is exhaustive.
TYPMOD018 requires a member for 0, TYPMOD019 allows a conversion to the enum only from a constant of a declared member, and TYPMOD020 allows an operator that returns the enum only over constants that produce a declared member.

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

The declaration corresponds to the C# 15 union declaration `public union Pet(Cat, Dog);`.
The generator adds the same members as the C# 15 lowering, namely the constructor bodies, the `Value` property, and `IUnion`, and before C# 15 it also adds the implicit conversions from each case type.
Before .NET 11, it adds `UnionAttribute` and `IUnion` to each assembly as embedded types.
A switch on `Value` that handles every case type is exhaustive.
C# 15 treats the generated type as a language union, so a switch can also match the union itself, as in `pet switch { Cat cat => ... }`.

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

Construction goes through the generated `Create`, which returns the validation result:

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

`Result<TValue, TFailure>` is a C# 15 union type whose case types are `Succeeded<TValue>` and `Failed<TFailure>`.
`Failed<TFailure>` carries only the failure type, so `IsFailed` passes a failure to a result of another value type unchanged.
`Result.Succeeded` and `Result.Failed` infer the type arguments, and a new failure of a closed hierarchy names its base type, as in `Result.Failed<OrderFailure>(...)`.
`Map`, `Bind`, `MapFailure`, query expressions, `Match`, `Sequence`, which stops at the first failure, and `Accumulate` and `Zip`, which collect every failure in a `NonEmptyList<TFailure>`, compose results.
Its internal tag fixes 0 as uninitialized, so observing a `default` result throws, and TYPMOD015 rejects a `default` result at compile time.
TYPMOD016 rejects casting `Value` to a case type, and TYPMOD017 warns when a result is discarded, as Rust does for `#[must_use]`.
In C# 15, a switch can match the result itself, as in `result switch { Succeeded<Order>(var order) => ... }`.

## Consumer checks

Consumer test projects reference `TypeModeling.Testing` and run the setup obligations from declarations alone.

### Closure check of closed hierarchies

```csharp
using TypeModeling.Testing.Closure;

var excluded = new Dictionary<string, string>
{
    ["tools/CodeGen/CodeGen.csproj"] = "a build-time-only generation tool, outside the shipped closure",
};
var closure = await SolutionClosureLoader.LoadAsync(
    new SolutionClosureScan(repoRoot, "App.slnx", "Debug", excluded),
    CancellationToken.None);
await Assert.That(SolutionClosureConformance.InventoryViolations(
    closure, SolutionClosureConformance.RepoProjectPaths(repoRoot), excluded)).IsEmpty();
await Assert.That(SolutionClosureConformance.ClosedHierarchyViolations(closure)).IsEmpty();
```

### Analyzer attach check

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

Several checks can be bundled into `AnalyzerAttachmentCheck` and judged in one scan.
Project evaluation and compilation do not depend on the detector, so scanning once per check compiles the same project once per check.
Passing `cacheDirectory` reuses a detector's verdict while the project's sources, references and build definitions are unchanged, and recompiles only the projects that changed.

```csharp
var results = await AnalyzerAttachmentConformance.ViolationsAsync(
    repoRoot,
    [
        new AnalyzerAttachmentCheck(typeModelingRequirement, new ClosedTypeUsageDetector()),
        new AnalyzerAttachmentCheck(otherRequirement, otherDetector),
    ],
    cacheDirectory: ".cache/attach-verdicts",
    CancellationToken.None);
```

## Dependencies

The runtime `src/TypeModeling/` depends on no other library; effectsystem-dotnet references it. Only the check executor `src/TypeModeling.Testing/` depends on Roslyn's Microsoft.CodeAnalysis.CSharp, for MSBuild evaluation and probe compilation.

## Development

This repository applies its shipped checks to itself ([SelfAuditTests](./tests/TypeModeling.Testing.Tests/root/SelfAuditTests.cs)). Verification runs through three devenv entries, split by time budget:

- `devenv shell verify` (within 2 minutes, before every commit) builds the solution with every analyzer warning as an error, including cognitive complexity (SonarAnalyzer S3776, threshold 15), and runs the in-process tests of the runtime and the analyzers.
- `devenv shell verify-push` (within 15 minutes, before every push) runs `verify`, the self-audit tests, and mutation testing of the runtime and the analyzers on the lines changed since the push base. One surviving or uncovered mutant on a changed line fails it.
- `devenv shell verify-full` (no budget, not a gate, before every release) runs `verify`, all TypeModeling.Testing tests, which evaluate MSBuild projects on the file system, and mutation testing over all three source projects. Undetected mutants that the previous full run did not report are appended to the backlog.

Mutation testing uses [mutation-dotnet](https://github.com/YukiIto1999/mutation-dotnet), built from the commit of its v0.4.1 release tag pinned in `devenv.yaml`. For `verify-push`, its `--changed-lines` option generates only the mutants on changed lines, and its `changed-lines` command selects the undetected ones. Branch, commit, and release conventions are described in [CONTRIBUTING.md](./CONTRIBUTING.md), and released changes in [CHANGELOG.md](./CHANGELOG.md).

## Architecture standard

The architecture standard is at `/home/nixos/environment/architecture-standard`.
Conformance is always judged against the current text of the standard.
Application follows the four rules of application and the usage procedures in the standard's README.
Decision records are kept in `docs/decisions/`, outside version control.

## License

MIT
