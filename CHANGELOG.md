# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

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

- `Result<TValue, TFailure>` is a C# 15 union struct whose case types are `Succeeded<TValue>` and `Failed<TFailure>`, replacing the nested records `Result<TValue, TFailure>.Succeeded` and `Result<TValue, TFailure>.Failed`. Construct it with `Result.Succeeded` and `Result.Failed`, return the failure of another result with `IsFailed`, and match it with `result.Value switch` before C# 15 or `result switch` in C# 15. Its internal tag fixes 0 as uninitialized, and every observation of an uninitialized result throws.
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

- The mutation-testing gate in `devenv shell verify` now runs [mutation-dotnet](../mutation-dotnet) instead of Stryker.NET.

## [0.1.0] - 2026-07-24

First version after becoming independent from tcs-ocr-beta.

### Added

- The TypeModeling core: `[ClosedUnion]`, `[ValueObject<TUnderlying>]`, `Result<TValue, TFailure>`, `Unit`, and `Never`.
- The compile-time engine: TYPMOD001, TYPMOD002, and the exhaustiveness suppressor.
- The setup-obligation executor as the Testing package, with the shipped checks applied to this repository itself.
