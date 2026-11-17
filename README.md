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

Check out the repository into the same workspace.
Pin the version with a `vX.Y.Z` tag on `main`.

Import the analysis props once, from the `Directory.Build.props` that covers every project of the repository:

```xml
<Project>
  <Import Project="../typemodeling-dotnet/TypeModeling.Analysis.props" />
</Project>
```

The import attaches the analyzer to every C# project under that `Directory.Build.props`, so no project declares an analyzer reference of its own.
Exhaustiveness, generation, and every diagnostic run in the analyzer.
A project that uses the library references the runtime library as usual:

```xml
<ItemGroup>
  <ProjectReference Include="../typemodeling-dotnet/src/TypeModeling/TypeModeling.csproj" />
</ItemGroup>
```

The build fails with an error when the analysis is missing or weakened:

| ID | Cause |
|---|---|
| TYPMOD201 | The analyzer project is not in the checkout |
| TYPMOD202 | `RunAnalyzers` is `false`, or `RunAnalyzersDuringBuild` is `false` without `RunAnalyzers` set to `true` |
| TYPMOD203 | The analyzer is not among the project's analyzers, for example because a `ProjectReference` or an `Analyzer` item was removed |
| TYPMOD022 | A setting weakens a TypeModeling diagnostic (see below) |
| TYPMOD204 | A C# project of the solution, or a C# project that a project of the solution references, does not import the props (see below) |

The build judges the configuration it builds. A defect that exists only in `Release` is found when `Release` is built, and a `Release` build of any project passes these checks only if the analyzer is attached and not stopped or weakened in `Release`. Build each configuration that you ship.

A project that opts out of `Directory.Build.props` (`ImportDirectoryBuildProps` set to `false` before the SDK props or as a global property), or whose nearest `Directory.Build.props` does not import its parent, is not reached by the import, and the project itself cannot detect that.
To make the solution build detect it, add a `Directory.Solution.targets` next to the solution that imports the solution check:

```xml
<Project>
  <Import Project="../typemodeling-dotnet/TypeModeling.Solution.targets" />
</Project>
```

Before it builds anything, `dotnet build` of the solution fails with TYPMOD204 and names each C# project whose `TypeModelingAnalysisState` target is missing, because only the props define that target.
The check starts at the C# projects that the solution lists and follows their `ProjectReference` items, in each target framework that a project builds, through every project that the props reach.
A project that the solution does not list but that a listed project references, directly or through other projects, is therefore checked too, and the error names it with the project that references it.
A reference that only attaches an analyzer (`OutputItemType="Analyzer"`) is not followed.
A build of a single `.csproj` instead of the solution is not checked, and neither is a project that no listed project references.
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
The internal tag fixes 0 as uninitialized. `HasValue` is the only safe initialization-status query: it returns `false` for a `default` result, not a business failure or an absent payload. Every payload or case projection and semantic observation (`Value`, both `TryGetValue` overloads, `IsFailed`, `Match`, equality, hashing, and string conversion) throws `InvalidOperationException` on an uninitialized result. Equality checks both operands, even when their cases differ; an uninitialized result also rejects comparison with `null` or an unrelated object. A constructed success or failure with a permitted `null` payload remains initialized and keeps its distinct case.

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

The generator adds a get-only `Value`, a private constructor, and a `Create` method that returns the result of `Parse`.
`Create` keeps the value that `Parse` returns, so `Parse` can normalize its input.
Code outside the declaration can therefore obtain an `OrderId` only from `Create`, and `id with { }` copies a validated value unchanged.
TYPMOD023 keeps it so: the record must be a `sealed partial record` without a base type, and the generator rejects every constructor you declare (a primary, copy, or private one included), a member named `Value`, and a field or property that code can write (`set`, `init`, or a field that is not `readonly`).
TYPMOD024 rejects every `new` of a value object in source, including code from another generator and code that is marked as generated, whether written as `new OrderId(...)`, as a target-typed `new(...)`, or inside a lambda or a nested type of the record, so `public static OrderId Empty => new(Guid.Empty)` does not compile.
Only the `new` inside the generated `Create` is accepted, and the analyzer finds it by its signature, `public static Result<OrderId, TFailure> Create(Guid raw)`, which no other declaration of the record can repeat.
Reflection, serializers, and unsafe code can still create an uninitialized instance.

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
| TYPMOD021 | Error, off by default | A switch over a closed type or a union `Value` names its cases and has no arm that accepts a case added later | None |
| TYPMOD022 | Error | No setting lowers the severity of a TypeModeling diagnostic or suppresses it | None |
| TYPMOD023 | Error | A value object is a sealed partial record without a base type that declares no constructor, no `Value`, and no writable field or property | None |
| TYPMOD024 | Error | A value object is not built with `new` in source, only by `Create` and by a `with` copy | None |

The suppressions TYPMOD101, TYPMOD102, and TYPMOD103 remove CS8509, CS8524, and CS8846 from a switch expression that C# 15 judges exhaustive.

### Weakened diagnostics

TYPMOD022 reports a setting that lowers a TypeModeling diagnostic below its default severity or hides it, so a build cannot pass by switching a diagnostic off.
Settings that cannot take effect, such as a wildcard like `dotnet_diagnostic.TYPMOD*.severity`, are not reported, and the diagnostics they name stay errors.
The diagnostic is `NotConfigurable`, so `NoWarn`, `.editorconfig`, a ruleset, and `#pragma` cannot suppress it.
It reports these settings:

- `dotnet_diagnostic.<id>.severity` in an `.editorconfig` or a global config that lowers the diagnostic, for example to `none`, `silent`, `suggestion`, or `warning` for an error.
- `dotnet_analyzer_diagnostic.severity` or `dotnet_analyzer_diagnostic.category-TypeModeling.severity` set to `none`, `silent`, or `suggestion`.
- `NoWarn` or another compiler setting that suppresses or lowers a diagnostic.
- A `#pragma warning disable` of a TypeModeling diagnostic, or a bare `#pragma warning disable`, without a `restore` in the same file.
- An assembly-wide `SuppressMessage` of a TypeModeling diagnostic.

TYPMOD021 is off by default, so it is not guarded.
A `SuppressMessage` at assembly scope that names TYPMOD022 itself suppresses even this diagnostic, because the compiler applies it to every diagnostic.

### Explicit cases

C# lets a switch over a closed type end with a catch-all arm, and TypeModeling keeps that behavior by default.
A catch-all arm also accepts a case added later, so the compiler no longer reports the switch as missing a case.
TYPMOD021 rejects such arms, is disabled by default, and is enabled through the standard diagnostic configuration, for example in a `.globalconfig` that the project includes:

```ini
is_global = true
dotnet_diagnostic.TYPMOD021.severity = error
```

TYPMOD021 checks switch expressions and switch statements.
The input of the switch, or an element of a tuple input, is protected when its type is a closed class, a closed enum, a type parameter constrained to a closed class, or the `Value` of a union such as `Result`.
A protected input is checked even when no arm names a case, so `state switch { _ => 1 }` and a `default:` label are rejected.
A tuple switch's `default:` label reports each distinct protected family it hides, rather than choosing one tuple element.
A position inside a case, such as the payload of `Succeeded<T>(var value)` or a property of a case, is checked only where some arm names a case of it, so a payload that the switch does not branch on may be bound or discarded.
An arm is also rejected at its own pattern when the pattern leaves a protected position beneath it unwritten, because it accepts a case added later there.
`_` or `var pair` over a tuple input that has a protected element, and `Done` or `Done { }` where another arm branches on the payload of `Done`, are rejected, once for each distinct protected type they hide.
A `default:` label is judged like `_`, so over an input that is not protected it is rejected when it hides a protected position that another case branched on, and it reports each distinct protected family once.
When a position names cases from nested closed hierarchies, `default:` reports each activated hierarchy, not just the outer family.
An arm that writes the position, such as `Done(var state)`, is judged at that position, and an arm that cannot match the case, such as `Skipped`, is accepted.
An arm that only tests a type, such as `Base` or `IDisposable`, is judged by whether a value of its type can also be a value of the case that another arm branched on, using the C# identity, reference, boxing, and unboxing conversions in both directions.
Variance counts, so `case IOut<object>` after `case Covariant { State: ... }` is rejected when `Covariant` implements `IOut<string>` and `IOut<T>` is covariant.
A class that is not sealed and an interface can share a value, and so can two interfaces, while a sealed class, a struct, or an array and an interface it cannot be converted to cannot, so that arm is accepted.
Numeric and user-defined conversions never count, and a type parameter is treated as able to share a value with any type.
Patterns name the same position when they read the same value, however they are written.
An element of a tuple is one position whether it is written by position, by element name, or as `Item1`.
A property that a derived type inherits or overrides is one position whether it is written on the derived type or on the base type, and `{ A.B: p }` and `{ A: { B: p } }` name the same position.
A record's positional parameter and the property of the same name are one position when the compiler generated the record's `Deconstruct` in the same compilation.
A property written on two unrelated derived types is one position for each type, so naming the cases through one type does not protect the other.
An interface property and the public property that a sealed class or a struct uses to implement it are one position, in either order of writing.
A class that is not sealed can be derived and re-implement the interface, and a public property can differ from an explicit implementation, so those properties stay separate positions, and an arm that writes one and omits an active other is rejected.
A property that a derived type hides with `new`, a hand-written `Deconstruct`, and an extension `Deconstruct` are separate positions, so an arm that writes one and omits an active other is rejected.
The `Deconstruct` of a record from a referenced assembly is treated the same way, even when its parameter names and a `CompilerGenerated` attribute match a property, because metadata does not show whether the compiler generated it or what it returns.
A switch over such a record therefore has to write each position in one form, either `Lamp(Signal.Red, _)` or `{ Light: Signal.Red }`, and an arm that mixes the two forms is rejected.
An arm is rejected when its pattern can accept a case added later, that is, `_`, `var x`, the type itself, a base type of it, `object`, a relational pattern on an enum, `not` of a case, and a property or positional pattern that names no case, alone or combined by `or` and `and`.
Patterns that name cases are accepted, such as `A or B`, `A and { ... }`, `Settled(var at)`, and `null`.
A closed class that contains closed classes counts at each level, so `Circle => ..., Rect.Square => ..., Rect => ...` rejects the last arm.
A switch expression arm with a `when` clause that is not the constant `true` is accepted, because the compiler does not count it toward exhaustiveness and still reports a missing case.
A switch statement has no such compiler check, so a guarded catch-all label is rejected too.
A protected position inside `not` or inside a list pattern, and a list pattern on a protected position, cannot be verified, so they are rejected whenever the switch branches on that position.
Write such cases positively, outside `not` and list patterns.
The diagnostic does not examine `is` expressions and `if` chains, and it recognizes a union only when the switch input is its `Value` expression itself.

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

The loader reads the `ProjectReference` items that each project file declares and replaces each `$(Name)` in an `Include` with the value that MSBuild evaluates for that property in the project.
A reference through a property that a `Directory.Build.props` sets, such as the `$(TypeModelingProjectPath)` in a project that contract-generator emits, is therefore part of the closure.
The loader rejects a property with an empty value and an expression that is not a plain property, because the closure would miss a project.

## Dependencies

The runtime `src/TypeModeling/` and `src/TypeModeling.Testing/` depend on no other library.

## Architecture standard

The architecture standard is at `/home/nixos/environment/architecture-standard`.
Conformance is always judged against the current text of the standard.
Application follows the four rules of application and the usage procedure in the standard's README.
The current contracts and reader-facing documents start at this README.

## License

MIT
