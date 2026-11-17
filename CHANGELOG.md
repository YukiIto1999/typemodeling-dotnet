# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- TYPMOD023 rejects a `[ValueObject<TUnderlying>]` declaration that gives code outside the record another way to build or write a value, so an instance exists only after `Parse`. It is an analyzer that checks the final compilation, so it sees the members that another generator adds to the partial too, which the `[ValueObject]` generator cannot. The record must be a `sealed partial record` without a base type, and it declares no constructor except the generated private one (a primary, copy, or private one included), no second member named `Value`, no property or indexer with `set` or `init`, and no instance field that is not `readonly`. The generated constructor is recognized by its signature. A `[ValueObject]` on a class that is not a record or on a record that is not `partial` is reported too, where it used to be ignored. Like every other TypeModeling diagnostic, it is guarded by TYPMOD022.
- TYPMOD024 rejects every `new` of a `[ValueObject<TUnderlying>]` type in source, including code from another generator and code that is marked as generated: `new OrderId(...)`, a target-typed `new(...)`, and a `new` inside a lambda, a field initializer, or a nested type of the record. Only the `new` inside the generated `Create` is accepted, found by its signature `public static Result<OrderId, TFailure> Create(Guid raw)`, which no other declaration of the record can repeat. A `with` copy of a validated value is not a `new` and stays valid. Like every other TypeModeling diagnostic, it is guarded by TYPMOD022.

### Fixed

- A value object could be built without `Parse`: a hand-written constructor, including a primary or copy constructor, was accepted next to the generated `Create`, so a `public OrderId() { }` could build an `OrderId` that held `Guid.Empty`. A copy constructor that another generator adds to the partial was reached by `with` without any `new` expression. A hand-written factory such as `public static OrderId Empty => new(Guid.Empty)`, or one that another generator adds to the partial, could reach the generated private constructor the same way. An indexer with `set` or `init` could write to a `readonly` array inside the instance. A value object that was not sealed or had a base type was accepted too. TYPMOD023 now reports each of these declarations on the final compilation, including the code of other generators, and TYPMOD024 reports the factories. `Create`, normalization in `Parse`, and `with` copies of a validated value are unchanged. Reflection, serializers, and unsafe code are outside the guarantee.

### Migration

1. Declare each value object as a `sealed partial record` without a base type.
2. Remove declared constructors, a primary parameter list, members named `Value`, and writable instance state. Validate and normalize the input in `Parse`, and let the generated `Create` build the instance.
3. Replace each `new OrderId(...)` with a call of `Create`, and each copy with a `with` expression.
4. Build once, and fix each TYPMOD023 and TYPMOD024 error by removing the cause it names.

## [0.4.0] - 2026-11-16

### Added

- `TypeModeling.Analysis.props` attaches the analyzer to every C# project with one import from the consumer's `Directory.Build.props`, so a project no longer declares an analyzer reference of its own. The build fails with TYPMOD201 when the analyzer project is missing from the checkout, with TYPMOD202 when `RunAnalyzers` or `RunAnalyzersDuringBuild` stops the analyzers, and with TYPMOD203 when the analyzer is not among the project's analyzers.
- TYPMOD022 reports a setting that lowers or hides a TypeModeling diagnostic: a lower severity in `.editorconfig` or a global config, `NoWarn`, an unrestored `#pragma warning disable`, and an assembly-wide `SuppressMessage`. It is `NotConfigurable`, so `NoWarn`, `.editorconfig`, a ruleset, and `#pragma` cannot suppress it.
- `TypeModeling.Solution.targets` fails a solution build with TYPMOD204 before it builds anything, and names every C# project that the props do not reach: a project of the solution that sets `ImportDirectoryBuildProps` to `false`, a project under a nested `Directory.Build.props` that does not import its parent, and every project when `ImportDirectoryBuildProps=false` is a global property. The check starts at the C# projects that the solution lists and follows their `ProjectReference` items, in each target framework that a project builds, through every project that the props reach, so a project that the solution does not list but that a listed project references, directly or through other projects, is checked too, and the error names it with the project that references it. A reference that only attaches an analyzer (`OutputItemType="Analyzer"`) is not followed. The solution imports it from a `Directory.Solution.targets`. A build of a single `.csproj` and a project that no listed project references are not checked.
- `TypeModeling.Analysis.Tests` builds repositories that import the props with each weakening in turn, and checks that the build fails with the diagnostic or error above. It runs in `verify-push` and `verify-full`.

### Changed

- The `verify` and `verify-push` time budgets are declared in `tiergate.json` at the repository root. The declaration file was renamed and its keys are unchanged.

### Removed

- The runtime analyzer attachment check is removed from `TypeModeling.Testing`: `AnalyzerAttachmentConformance`, `AnalyzerAttachmentCheck`, `AnalyzerAttachmentRequirement`, `IGuardedFeatureDetector`, `ClosedTypeUsageDetector`, and `GuardedTypeUsageWalk` in `TypeModeling.Testing.Attach`, with their tests. A missing, stopped, or weakened analyzer is now a build error from `TypeModeling.Analysis.props` and TYPMOD022, so no test has to build projects to find it. The closure check (`SolutionClosureConformance`) is unchanged.
- `TypeModeling.Testing` no longer depends on Microsoft.CodeAnalysis.CSharp.

### Fixed

- `SolutionClosureLoader` resolves a `$(Name)` in the `Include` of a `ProjectReference` with the value that MSBuild evaluates for that property in the project, so `SolutionClosureConformance` can read a project that contract-generator emits, whose `ProjectReference Include="$(TypeModelingProjectPath)"` it used to reject as statically unresolvable. A property with an empty value, such as an undefined one, and an expression that is not a plain property are still rejected before the build.

### Migration

1. Import `TypeModeling.Analysis.props` once from the `Directory.Build.props` that covers every project of the repository.
2. Remove the analyzer `ProjectReference` (`OutputItemType="Analyzer"`) from every project. Keep the `ProjectReference` to the runtime library.
3. Delete the test that calls `AnalyzerAttachmentConformance.ViolationsAsync`, with its `AnalyzerAttachmentRequirement`, its detector, and its list of expected projects. The props attach the analyzer to every C# project, so no list of projects that use closed types is needed.
4. Build once. Fix a TYPMOD201, TYPMOD202, TYPMOD203, or TYPMOD022 error by removing the cause it names. The check judged `RunAnalyzers`, `NoWarn`, `.editorconfig`, and `#pragma` in every configuration at once; the build judges the configuration it builds, so build each configuration that you ship.
5. A project that opts out of `Directory.Build.props` or has a nested `Directory.Build.props` that does not import its parent is not reached by the import. Add a `Directory.Solution.targets` that imports `TypeModeling.Solution.targets` so that the solution build fails with TYPMOD204 for such a project.

## [0.3.0] - 2026-11-14

### Added

- `[Closed]` on an enum follows the `closed` enum planned for C# 16. TYPMOD018 requires a member for 0, TYPMOD019 rejects a conversion to the enum from anything but a constant of a declared member, and TYPMOD020 rejects an operator that returns the enum unless its constant operands produce a declared member.
- TYPMOD021 requires a switch expression or statement over a closed class, a closed enum, or the `Value` of a union such as `Result` to name its cases, by rejecting a catch-all arm that accepts a case added later. It is disabled by default, so the C# behavior is unchanged, and a project enables it with `dotnet_diagnostic.TYPMOD021.severity` in its diagnostic configuration. It protects the switch input and each element of a tuple input even when no arm names a case, checks a position inside a case only where an arm names a case of it, and rejects a protected position that it cannot verify inside `not` or a list pattern. A `default:` label identifies each distinct protected family it hides, including a position that another case branched on when the switch input itself is not protected. An arm whose pattern leaves a protected position beneath it unwritten, such as `_` or `var pair` over a tuple input with a protected element, or `Done` where another arm branches on the payload of `Done`, is rejected at that pattern and identifies each distinct protected family it hides. An arm that only tests a type is judged by whether a value of its type can also be a value of the branched case, using the C# identity, reference, boxing, and unboxing conversions in both directions, so variance counts and a sealed class, a struct, or an array does not share a value with an interface it cannot be converted to; numeric and user-defined conversions never count and a type parameter may share a value with any type. Patterns name the same position when they read the same value: a tuple element by position, element name, or `Item1`; a property that a derived type inherits or overrides, written on the derived or the base type; the nested and extended forms of a property pattern; an interface property and the public property that a sealed class or a struct implements it with; and a record's positional parameter and the property of the same name when the compiler generated the record's `Deconstruct` in the same compilation. A property written on two unrelated derived types is one position for each type. A property that a derived type hides with `new`, a property of a class that is not sealed beside the interface property it implements, a public property beside an explicit implementation, and a hand-written or extension `Deconstruct` are never merged with another position. The `Deconstruct` of a record from a referenced assembly is not merged with a property either, because metadata does not show whether the compiler generated it or what it returns.
  Nested closed hierarchies activated at the same position are reported independently of the outer family.
- `Result` composes with query expressions, `Match`, `Sequence`, which stops at the first failure, and `Accumulate` and `Zip`, which collect every failure in a `NonEmptyList<TFailure>`.
- `NonEmptyList<T>` is an immutable list that has at least one item by construction.
- `[MustUse]` marks types and methods whose values must not be discarded, as Rust does with `#[must_use]`, and `Result` carries it. TYPMOD017 warns when a call discards such a value without assigning it to a discard.
- TYPMOD015 rejects an uninitialized `Result` built by `default` or a parameterless construction, and TYPMOD016 rejects casting the `Value` of a `Result` to a case type.
- `[Union]` with partial constructors declares a union in the shape of the C# 15 union declaration. The generator adds the constructor bodies, the `Value` property, `IUnion`, and before C# 15 the implicit conversions from each case type, and it adds embedded `UnionAttribute` and `IUnion` types before .NET 11. C# 15 recognizes the generated type as a language union.
- The exhaustiveness suppressor treats a switch on the `Value` of a union, alone or inside a tuple, as exhaustive when it handles every case type, as C# 15 does for a union. A hand-written union may take its creation members and `Value` from a union member provider, as in C# 15.
- TYPMOD007 to TYPMOD014 check unions with the rules of C# 15: the union declaration shape rejects what CS9373, CS9374, CS9375, and CS9387 reject, counting the backing fields of auto-implemented and `field`-backed properties as instance fields and reading the implementation part of partial members, a hand-written union needs creation members and a `Value` property as CS9385 and CS9386 require, and the declaration shape must be a partial struct inside partial types.
- TYPMOD003 rejects a closed class that is not abstract. C# 15 makes a `closed` class implicitly abstract, and C# 14 expresses it by declaring `abstract`.
- TYPMOD004 rejects a direct subtype of a closed class declared in another assembly, as C# 15 does with CS9382.
- TYPMOD005 rejects a type parameter of a direct subtype that the closed base type does not reference, as C# 15 does with CS9383.
- TYPMOD006 rejects a switch expression arm that every earlier arm already covers through a closed type, as C# 15 does with CS8510.

### Changed

- `verify-push` now runs acceptance checks and repository self-audits without mutation analysis. Changed-line and full mutation analysis remain separate commands with their original reports and exit statuses; survivors, scores, and missing analysis are not approval gates.
- `Result<TValue, TFailure>` is a C# 15 union struct whose case types are `Succeeded<TValue>` and `Failed<TFailure>`, replacing the nested records `Result<TValue, TFailure>.Succeeded` and `Result<TValue, TFailure>.Failed`. Construct it with `Result.Succeeded` and `Result.Failed`, return the failure of another result with `IsFailed`, and match it with `result.Value switch` before C# 15 or `result switch` in C# 15. Its internal tag fixes 0 as uninitialized. Only `HasValue` may query that state and returns `false`; payload and case projections, equality, hashing, and string conversion throw `InvalidOperationException` rather than exposing an empty third case. Equality rejects an uninitialized operand on either side, including comparison of an uninitialized receiver with `null` or an unrelated object. Constructed cases with permitted `null` payloads remain initialized.
- `[ClosedUnion]` is replaced by `[Closed]`, which follows the C# 15 `closed` modifier. Direct subtypes may be declared anywhere in the same assembly, nested or not and sealed or not, and closed classes declared with `[Closed]` in other assemblies are recognized. A class compiled with the C# 15 `closed` modifier is not recognized, because a Roslyn that understands `closed` hides its `IsClosedTypeAttribute` from analyzers. A closed class from another assembly counts as exhausted only when every direct subtype is visible: a generator records the number of direct subtypes of each public closed class as `ClosedSubtypeCountAttribute`, as C# 15 records them in `IsClosedTypeAttribute.DerivedTypes`, so an internal subtype hidden from metadata keeps a switch non-exhaustive.
- The exhaustiveness suppressor lets the compiler itself decide exhaustiveness after covering the values a closed type cannot have, so positional, property, tuple, nested and guarded patterns, generic instantiations, and type parameters constrained to a closed class behave as in C# 15. A switch is not treated as exhaustive when an arm added for the check does not compile, because the compiler may then skip the exhaustiveness check. Its suppressions are renamed to TYPMOD101 for CS8509, TYPMOD102 for CS8524, and TYPMOD103 for CS8846.
- TypeModeling.Testing compiles consumer projects with Microsoft.CodeAnalysis.CSharp 5.9.0, so C# 14 syntax such as partial constructors is accepted, and its attach check counts uses of union types.
- The closed hierarchy check of TypeModeling.Testing reports a direct subtype of a closed class declared outside its assembly, instead of a subtype that is not a nested sealed record.

### Removed

- TYPMOD001, which rejected switch statements over closed types, is removed. C# 15 does not restrict switch statements over closed hierarchies, so the analyzer rejected code that the language accepts.
- The exhaustiveness suppressor no longer treats a `[JsonPolymorphic]` hierarchy as a closed set. C# 15 does not close a hierarchy by its JSON registrations, so a switch that relied on it stopped being exhaustive after migration.

## [0.2.0] - 2026-10-12

### Added

- `AnalyzerAttachmentCheck` bundles several detector checks so that `AnalyzerAttachmentConformance.ViolationsAsync` judges them in one scan of the repository projects, instead of evaluating and compiling every project once per check.
- `cacheDirectory` makes `ViolationsAsync` reuse a detector's verdict while a project's sources, references and build definitions are unchanged, and recompile only the projects that changed. It is optional and off by default.

### Changed

- Verification is split by time budget into `devenv shell verify` (build and in-process tests, within 2 minutes), `verify-push` (adds the self-audit tests and mutation testing of changed lines, within 15 minutes), and `verify-full` (adds all TypeModeling.Testing tests and mutation testing of all source projects, not a gate).
- The mutation gate no longer requires a whole-runtime score of 60; one surviving or uncovered mutant on a line of the runtime or the analyzers changed since the push base fails `verify-push`. TypeModeling.Analyzers and TypeModeling.Testing are now mutation targets as well; TypeModeling.Testing is mutated only by `verify-full`.
- mutation-dotnet is built from the commit of its v0.4.1 release tag pinned in `devenv.yaml` instead of a sibling checkout. `verify-push` generates only the mutants on changed lines with its `--changed-lines` option, and its `changed-lines` command selects the undetected ones.
- Cognitive complexity above 15 (SonarAnalyzer S3776) now fails the build.
- Project inventory and analyzer-attachment self-audits now follow Git's ignore rules, so ignored build and development-environment projects are not treated as repository projects.

## [0.1.1] - 2026-08-25

### Changed

- The mutation-testing gate in `devenv shell verify` now runs [mutation-dotnet](https://github.com/YukiIto1999/mutation-dotnet) instead of Stryker.NET.

## [0.1.0] - 2026-07-24

First version.

### Added

- The TypeModeling core: `[ClosedUnion]`, `[ValueObject<TUnderlying>]`, `Result<TValue, TFailure>`, `Unit`, and `Never`.
- The compile-time engine: TYPMOD001, TYPMOD002, and the exhaustiveness suppressor.
- The setup-obligation executor as the Testing package, with the shipped checks applied to this repository itself.
