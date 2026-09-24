# Changelog

All notable changes follow [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project uses Semantic Versioning.

## [Unreleased]

## [0.1.0]

### Added

- Platform-neutral `Result`, `Result<T>`, `Result<T, TError>`, `Error`, and `Option<T>` primitives
  targeting .NET Standard 2.0 and .NET 10 with no runtime package dependencies.
- Explicit success/failure factories, pattern matching, mapping, binding, validation, filtering,
  tapping, error mapping, conversions, and value extraction helpers.
- `Result.Try` and `Result.TryAsync` boundary adapters for converting exceptions into failures while
  preserving cancellation.
- Eager, lazy, and asynchronous `OrElse` fallbacks for result and option types.
- Task composition extensions for asynchronous result and option pipelines.
- Synchronous LINQ query syntax through `Select` and `SelectMany`.
- Roslyn analyzer `AHRF001` and code fix for detectable default construction of result values.
- Roslyn analyzer `AHRF002` for manual try/catch patterns that can use `Result.Try` or
  `Result.TryAsync`.
- Roslyn analyzers `AHRF003` and `AHRF004` for awaitables or async-void delegates passed to
  synchronous `Result.Try`.
- XML documentation, API reference generation, runnable samples, package-consumer verification,
  Source Link, deterministic builds, and symbol packages.

[Unreleased]: https://github.com/AHRConsulting/Ahr.Foundation/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/AHRConsulting/Ahr.Foundation/releases/tag/v0.1.0
