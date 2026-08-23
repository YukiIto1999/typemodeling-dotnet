[English](README.md) | [日本語](README.ja.md)

# TypeModeling

TypeModeling is a C# type-modeling foundation that makes invalid states unrepresentable. This document is for consuming developers and covers installation and usage.

## Problem

In plain C#, exhaustive matching over a discriminated union collapses through the default branch, and nothing stops values from being constructed without validation. A missing registration or an out-of-hierarchy derivation stays invisible until it becomes a serialization exception at run time.

## What it provides

- `[ClosedUnion]` writes discriminated unions with external derivation sealed off, and `[ValueObject<TUnderlying>]` writes value objects that only allow validated construction — both close to a language feature.
- `Result<TValue, TFailure>`, `Unit`, and `Never` express success and failure of pure computation in types.
- The compile-time engine (TYPMOD001, TYPMOD002, and the exhaustiveness suppressor) enforces the discrimination and construction discipline at build time.
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

Discriminate closed variants with a switch expression.

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
        ProtectedDiagnosticIds: new HashSet<string> { "TYPMOD001" },
        ProtectedDiagnosticPrefixes: new HashSet<string> { "TYPMOD" },
        ExpectedProjectPaths: new HashSet<string> { "src/App.Domain/App.Domain.csproj" },
        ProtectedDiagnosticCategories: new HashSet<string>()),
    new ClosedTypeUsageDetector(),
    CancellationToken.None);
await Assert.That(violations).IsEmpty();
```

## Dependencies

The runtime `src/TypeModeling/` depends on no other library; effectsystem-dotnet references it. Only the check executor `src/TypeModeling.Testing/` depends on Roslyn's Microsoft.CodeAnalysis.CSharp, for MSBuild evaluation and probe compilation.

## Development

This repository applies its shipped checks to itself ([SelfAuditTests](./tests/TypeModeling.Testing.Tests/root/SelfAuditTests.cs)). `devenv shell verify` runs the build, all tests, and mutation testing of the whole runtime with [mutation-dotnet](../mutation-dotnet) as the gate. Branch, commit, and release conventions are described in [CONTRIBUTING.md](./CONTRIBUTING.md), and released changes in [CHANGELOG.md](./CHANGELOG.md).

## License

MIT
