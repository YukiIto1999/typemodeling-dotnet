[English](README.md) | [日本語](README.ja.md)

# TypeModeling

TypeModeling is a C# 14 library that provides the type features of C# 15 and later in the same form, together with type features from Rust and F#.
Code written with it moves to the language features by a mechanical rewrite, and the table below names that rewrite for each feature.
This README is for developers who use the library in their projects.

## Features

| Feature | Before C# 15 | In C# 15 and later | Migration |
|---|---|---|---|
| Closed hierarchy | `[Closed] public abstract record Shape` | `public closed record Shape` | Replace the attribute with `closed` and remove `abstract` |
| Closed enum | `[Closed] public enum Color` | `public closed enum Color` (planned for C# 16) | Replace the attribute with `closed` |
| Union declaration | `[Union] public readonly partial struct Pet` with `public partial Pet(Cat value);` | `public union Pet(Cat, Dog);` | Not required, because C# 15 treats the generated type as a union |
| Union matching | `pet.Value switch { Cat cat => ... }` | `pet switch { Cat cat => ... }` | Remove `.Value` |
| Result | `result.Value switch { Succeeded<T>(var value) => ... }` | `result switch { Succeeded<T>(var value) => ... }` | Remove `.Value` |
| Non-empty list | `NonEmptyList<T>` | Same | None |
| Value object | `[ValueObject<Guid>] public sealed partial record OrderId` | Same | None |
| Unit and Never | `Unit`, `Never` | Same | None |
| Must-use value | `[MustUse]` | Same | None |

## Installation

Check out the repository into the same workspace and reference it by a relative path.
Pin the version with a `vX.Y.Z` tag on `main`.

```xml
<ItemGroup>
  <ProjectReference Include="../typemodeling-dotnet/src/TypeModeling/TypeModeling.csproj" />
  <ProjectReference Include="../typemodeling-dotnet/src/TypeModeling.Analyzers/TypeModeling.Analyzers.csproj"
                    OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

Every project that declares or matches closed types, unions, or results attaches the analyzer, because exhaustiveness and generation run there.
Architecture-test projects also reference [TypeModeling.Testing](./src/TypeModeling.Testing/).

## Usage

### Closed hierarchy

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

`[Closed]` corresponds to the C# 15 `closed` modifier.
Direct subtypes may be declared anywhere in the same assembly, nested or not and sealed or not.
A switch expression that handles every direct subtype is exhaustive, whatever the shape of its patterns, as in C# 15.
An arm for the base type after every subtype is unreachable, as in C# 15.
A closed class from another assembly is exhausted only when that assembly attaches the analyzer, which records the number of its direct subtypes, and every direct subtype is visible, as C# 15 requires.

### Closed enum

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

`[Closed]` on an enum corresponds to the `closed` enum planned for C# 16.
A switch expression that handles every named member is exhaustive.
A closed enum needs a member for 0, converts only from constants of its members, and allows an operator that returns it only over constants that produce a member.

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
The generator adds the members of the C# 15 lowering, which are the constructor bodies, the `Value` property, and `IUnion`, and before C# 15 it also adds an implicit conversion from each case type that is not an interface.
Before .NET 11, it adds `UnionAttribute` and `IUnion` to each assembly as embedded types.
Before C# 15, match a union through its `Value`, because the .NET 10 compiler rejects a property pattern on a union type until C# 15.
A switch on `Value`, alone or inside a tuple, is exhaustive when it handles every case type.

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
`Map`, `Bind`, `MapFailure`, query expressions, and `Match` compose results.
`Sequence` stops at the first failure, while `Accumulate` and `Zip` collect every failure in a `NonEmptyList<TFailure>`.
The internal tag fixes 0 as uninitialized, so observing a `default` result throws.

### Value object

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

The generator adds a private constructor and a `Create` method that returns the result of `Parse`, so an `OrderId` exists only after validation.

### Other types

`NonEmptyList<T>` is an immutable list that has at least one item by construction, and `NonEmptyList.FromSequence` returns `null` for an empty sequence.
`Unit` is the type of a success without a value, and `Never` is a type that has no value.
`[MustUse]` on a type or a method makes discarding its value a warning, as `#[must_use]` does in Rust, and `Result` carries it.

## Diagnostics

| ID | Severity | Rule | C# counterpart |
|---|---|---|---|
| TYPMOD002 | Error | A value object needs a `Parse` with the required signature | None |
| TYPMOD003 | Error | A closed class is declared `abstract` | `closed` is implicitly abstract |
| TYPMOD004 | Error | A direct subtype of a closed class is in the same assembly | CS9382 |
| TYPMOD005 | Error | A direct subtype of a closed class uses every type parameter in its base type | CS9383 |
| TYPMOD006 | Error | No arm follows arms that already exhaust a closed type | CS8510 |
| TYPMOD007 | Error | A union declaration is a partial struct | Union declarations are structs |
| TYPMOD008 | Error | A union declaration has no instance field, auto-property, or field-like event | CS9373 |
| TYPMOD009 | Error | A union declaration has no public non-partial constructor with one parameter | CS9374 |
| TYPMOD010 | Error | A constructor of a union declaration calls another constructor with `this(...)` | CS9375 |
| TYPMOD011 | Error | A union declaration does not use a union member provider | CS9387 |
| TYPMOD012 | Error | A union type has a union creation member | CS9385 |
| TYPMOD013 | Error | A union type has a public `object` property named `Value` | CS9386 |
| TYPMOD014 | Error | The types that contain a union declaration are partial | None |
| TYPMOD015 | Error | A `Result` is not built by `default` or a parameterless construction | None |
| TYPMOD016 | Error | The `Value` of a `Result` is not cast to a case type | None |
| TYPMOD017 | Warning | A `[MustUse]` value is not discarded by an expression statement | Rust `#[must_use]` |
| TYPMOD018 | Error | A closed enum has a member for 0 | Closed enums |
| TYPMOD019 | Error | A conversion to a closed enum comes from a constant of a member | Closed enums |
| TYPMOD020 | Error | An operator that returns a closed enum produces a member from constants | Closed enums |

The suppressions TYPMOD101, TYPMOD102, and TYPMOD103 remove CS8509, CS8524, and CS8846 from a switch expression that C# 15 judges exhaustive.

## Consumer checks

A consumer test project references `TypeModeling.Testing` and runs the setup checks from declarations.

### Closure check of closed hierarchies

```csharp
using TypeModeling.Testing.Closure;

var excluded = new Dictionary<string, string>
{
    ["tools/CodeGen/CodeGen.csproj"] = "A generation tool that runs only at build time and stays outside the shipped closure",
};
var closure = await SolutionClosureLoader.LoadAsync(
    new SolutionClosureScan(repoRoot, "App.slnx", "Debug", excluded),
    CancellationToken.None);
await Assert.That(SolutionClosureConformance.InventoryViolations(
    closure, SolutionClosureConformance.RepoProjectPaths(repoRoot), excluded)).IsEmpty();
await Assert.That(SolutionClosureConformance.ClosedHierarchyViolations(closure)).IsEmpty();
```

The closed hierarchy check reports a direct subtype of a closed class declared outside the assembly of the closed class, and a subtype of a `[JsonPolymorphic]` type that is missing from its `[JsonDerivedType]` registrations.

### Analyzer attach check

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

`ClosedTypeUsageDetector` counts every project that uses a closed type or a union type.

## Dependencies

The runtime `src/TypeModeling/` depends on no other library.
`src/TypeModeling.Testing/` depends on Microsoft.CodeAnalysis.CSharp 5.9.0 to evaluate MSBuild projects and compile them with C# 14 syntax and generated sources.

## Architecture standard

The architecture standard is at `/home/nixos/environment/architecture-standard`.
Conformance is always judged against the current text of the standard.
Application follows the four rules of application and the usage procedure in the standard's README.
The current contracts and reader-facing documents start at this README.

## License

MIT
