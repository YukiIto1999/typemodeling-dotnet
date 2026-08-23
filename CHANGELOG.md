# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Changed

- The mutation-testing gate in `devenv shell verify` now runs [mutation-dotnet](../mutation-dotnet) instead of Stryker.NET.

## [0.1.0] - 2026-07-24

First version after becoming independent from tcs-ocr-beta.

### Added

- The TypeModeling core: `[ClosedUnion]`, `[ValueObject<TUnderlying>]`, `Result<TValue, TFailure>`, `Unit`, and `Never`.
- The compile-time engine: TYPMOD001, TYPMOD002, and the exhaustiveness suppressor.
- The setup-obligation executor as the Testing package, with the shipped checks applied to this repository itself.
