# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Changed

- Verification is split by time budget into `devenv shell verify` (build and in-process tests, within 2 minutes), `verify-push` (adds the self-audit tests and mutation testing of changed lines, within 15 minutes), and `verify-full` (adds all TypeModeling.Testing tests and mutation testing of all source projects, not a gate).
- The mutation gate no longer requires a whole-runtime score of 60; one surviving or uncovered mutant on a line of the runtime or the analyzers changed since the push base fails `verify-push`. TypeModeling.Analyzers and TypeModeling.Testing are now mutation targets as well; TypeModeling.Testing is mutated only by `verify-full`.
- mutation-dotnet is built from the commit of its v0.2.0 release tag pinned in `devenv.yaml` instead of a sibling checkout.
- Cognitive complexity above 15 (SonarAnalyzer S3776) now fails the build.

## [0.1.1] - 2026-08-25

### Changed

- The mutation-testing gate in `devenv shell verify` now runs [mutation-dotnet](../mutation-dotnet) instead of Stryker.NET.

## [0.1.0] - 2026-07-24

First version after becoming independent from tcs-ocr-beta.

### Added

- The TypeModeling core: `[ClosedUnion]`, `[ValueObject<TUnderlying>]`, `Result<TValue, TFailure>`, `Unit`, and `Never`.
- The compile-time engine: TYPMOD001, TYPMOD002, and the exhaustiveness suppressor.
- The setup-obligation executor as the Testing package, with the shipped checks applied to this repository itself.
