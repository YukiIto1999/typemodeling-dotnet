# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- TYPMOD003 rejects a closed class that is not abstract. C# 15 makes a `closed` class implicitly abstract, and C# 14 expresses it by declaring `abstract`.
- TYPMOD004 rejects a direct subtype of a closed class declared in another assembly, as C# 15 does with CS9382.
- TYPMOD005 rejects a type parameter of a direct subtype that the closed base type does not reference, as C# 15 does with CS9383.
- TYPMOD006 rejects a switch expression arm that every earlier arm already covers through a closed type, as C# 15 does with CS8510.

### Changed

- `[ClosedUnion]` is replaced by `[Closed]`, which follows the C# 15 `closed` modifier. Direct subtypes may be declared anywhere in the same assembly, nested or not and sealed or not, and closed classes declared with `[Closed]` in other assemblies are recognized. A class compiled with the C# 15 `closed` modifier is not recognized, because a Roslyn that understands `closed` hides its `IsClosedTypeAttribute` from analyzers. A closed class from another assembly counts as exhausted only when every direct subtype is visible: a generator records the number of direct subtypes of each public closed class as `ClosedSubtypeCountAttribute`, as C# 15 records them in `IsClosedTypeAttribute.DerivedTypes`, so an internal subtype hidden from metadata keeps a switch non-exhaustive.
- The exhaustiveness suppressor lets the compiler itself decide exhaustiveness after covering the values a closed type cannot have, so positional, property, tuple, nested and guarded patterns, generic instantiations, and type parameters constrained to a closed class behave as in C# 15. A switch is not treated as exhaustive when an arm added for the check does not compile, because the compiler may then skip the exhaustiveness check. Its suppressions are renamed to TYPMOD101 for CS8509, TYPMOD102 for CS8524, and TYPMOD103 for CS8846.
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
