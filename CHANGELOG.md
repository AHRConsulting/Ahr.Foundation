# Changelog

All notable changes follow [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this project uses Semantic Versioning.

## [Unreleased]

## [0.2.0]

### Fixed

- `ToString()` on `Result`, `Result<T>`, `Result<T, TError>`, and `Option<T>` no longer throws.
  It previously threw `InvalidOperationException` for successful results, failed `Result<T>` values,
  `Option<T>.None`, and default-initialized results, which broke string interpolation, logging, and
  test assertion messages. Values now format as `Success`, `Success(value)`, `Failure(error)`,
  `Some(value)`, `None`, or `Uninitialized`.
- `default(Error).Message` now returns an empty string instead of `null`, honoring its non-null
  annotation. Result and option factories still reject a default `Error`.
- The bundled analyzers load in older compilers again. They were built against the compiler of
  the newest .NET 10 SDK, so the .NET 6 to 9 SDKs, early .NET 10 SDKs and older Visual Studio
  versions skipped them with `CS9057`. They now target Roslyn 4.0, which is the .NET 6 SDK and
  Visual Studio 2022 17.0.

### Changed

- `AHRF002` reports a manual try/catch only when a catch-all block builds the failure. A catch of
  `OperationCanceledException` or `TaskCanceledException` that builds a failure, a catch of one
  specific exception type, and a `when` filter are no longer reported, because `Result.Try` rethrows
  cancellation and catches every other exception. When the try statement has a `finally` block, the
  message says to keep it in a `try/finally` around the call.
- `FirstOrNone` returns `None` when the first element (or first match) is `null`, instead of
  throwing `ArgumentNullException`. This matches `FirstOrDefault().ToOption()`.
- A `Result` array created without element initializers (for example `new Result<int>[n]`) is now
  reported by the new Info-level rule `AHRF005` instead of the Error-level `AHRF001`, so
  allocate-then-fill code no longer fails the build. `AHRF001` still reports `default` and
  parameterless construction.
- `Tap` and `TapAsync` stop the pipeline when the function returns a failure of the same family: a
  failed result on `Result`, `Result<T>`, and `Result<T, TError>`, or `None` on `Option<T>`. Previously
  `.Tap(order => repository.Save(order))` compiled as an `Action` and ignored the failure. A block body
  without a `return`, or a function returning the other family or a different error type, still
  ignores the returned value and is reported by `AHRF008`.
- `Result.Try` and `Result.TryAsync` still throw `ArgumentNullException` when the operation returns
  `null`, but the exception now names the `operation` parameter and says to return an `Option<T>`.
  `AHRF006` also reports a `Try` or `TryAsync` operation whose return type is nullable, such as
  `Task<object?>`.
- `ToOption()` on a nullable value type returns `Option<T>` rather than `Option<T?>`.
- `Ensure(predicate, default)` on `Result<T>` no longer compiles, because the new string overload makes
  it ambiguous. That call always threw at runtime.

### Added

- `TaskCompositionExtensions` overloads that take synchronous delegates (`MapAsync`, `BindAsync`,
  `MapErrorAsync`, `EnsureAsync`, `WhereAsync`, `TapAsync`, `TapErrorAsync`), so a `Task`-based
  pipeline can mix synchronous and asynchronous steps without an intermediate `await`. Async lambdas
  continue to bind to the existing asynchronous overloads.
- `MatchAsync` on `Task<Result>`, `Task<Result<T>>`, `Task<Result<T, TError>>`, and
  `Task<Option<T>>`, with synchronous or asynchronous branches.
- `Result.TryAsync` overloads that take a `CancellationToken`. They pass the token to the operation,
  propagate only that token's cancellation, and convert other cancellations, such as an
  `HttpClient` timeout, into a failure.
- `SingleOrNone`, `LastOrNone`, `GetValueOrNone` for `IReadOnlyDictionary<TKey, TValue>`, and
  `Values()` for sequences of options.
- `Ensure` and `EnsureAsync` overloads on `Result<T>` that take a string message.
- `ToResultAsync` on `Task<Option<T>>`, so a lookup can continue as a result pipeline without an
  intermediate `await`.
- A three-way `Match` and `MatchAsync` on `Result<Option<T>>`, `Result<Option<T>, TError>`, and their
  `Task` forms, taking `onSome`, `onNone`, and `onFailure`, so a lookup that can fail or find nothing
  no longer needs a nested match. Each form also takes actions or `Task`-returning functions and
  returns the original result, like the action `Match` on `Result<T>`.
- `EnsureSome` and `EnsureSomeAsync` on the same types, which unwrap `Some` and turn `None` into a
  failure with the given error.
- `MapSome`, `MapSomeAsync`, `BindSome`, and `BindSomeAsync` on the same types, which transform or
  continue from a found value and pass a missing value or failure through.
- `TapSome` and `TapSomeAsync` on the same types, which run a step for a found value and return the
  original result. Like `Tap`, a step that returns a result stops the pipeline when it fails.
- `SomeOr` and `SomeOrAsync` on the same types, which unwrap `Some` and turn `None` into the given
  fallback value, and `BindNone` and `BindNoneAsync`, which try another lookup when nothing was found.
  Both keep an existing failure.
- `Result<T>.ToResult()` and `Task<Result<T>>.ToResultAsync()`, which drop the success value and keep
  only success or failure.
- `MapError` and `MapErrorAsync` on `Result<T>` and `Task<Result<T>>` with a function returning a
  custom error type, producing `Result<T, TError>`. A function returning `Error` still keeps `Result<T>`.
- `Traverse`, `Sequence`, and `Partition` for lists of results, on `Result`, `Result<T>`, and
  `Result<T, TError>`. `Traverse` runs a step on each item and returns all the values or the first
  failure, and `Sequence` does the same for results that already exist. Both stop at the first
  failure. `Partition` runs every item and returns the values and the errors separately; for
  `Result` it returns the errors. `TraverseAsync`, `SequenceAsync` on a list of tasks, and
  `PartitionAsync` run or await one item at a time, in order.
- `Result.Try` and `Result.TryAsync` overloads that take a `catchWhen` filter. Only an exception the
  filter accepts becomes a failure; any other exception propagates unchanged, as with a C# `when`
  filter. Cancellation is handled before the filter: the caller's cancellation propagates, and in the
  overloads that take a token, any other cancellation, such as a timeout, always becomes a failure.
- `CatchCancellationAsync(error, cancellationToken)` on `Task<Result>`, `Task<Result<T>>`, and
  `Task<Result<T, TError>>`, with a form that builds the error from the exception. When the given
  token canceled the task, it returns that failure instead of throwing. Any other cancellation or
  exception propagates unchanged.
- `Error.IsCancellation`, which reports whether the error came from an `OperationCanceledException`,
  such as a timeout that `TryAsync` turned into a failure.
- `MapError` and `MapErrorAsync` on `Result`, plus `MapErrorAsync` on `Task<Result>` with a
  synchronous or asynchronous function, so a payload-free failure can be reworded or wrapped like a
  `Result<T>` failure.
- `[DebuggerDisplay]` on result and option types.
- A dedicated NuGet package readme with absolute links, so the package page renders correctly.
- Roslyn analyzers:
  - `AHRF005` (Info) for result arrays whose elements start uninitialized.
  - `AHRF006` (Warning) for `Map`, `MapAsync`, `MapSome`, `MapSomeAsync`, and `Select` calls that map to a nullable value,
    which throws at runtime because `Some` and `Success` never hold `null`.
  - `AHRF007` (Info), with a code fix, for `TryAsync` operations that use a caller's cancellation
    token without passing it to the cancellation-aware overload.
  - `AHRF008` (Warning) for `Tap` and `TapAsync` functions that discard a result or option, so a
    failure is ignored.
  - `AHRF009` (Info) for `GetValueOrDefault` and `ToOption` calls on a result of an option, which
    discard the failure.
  - `AHRF010` (Warning) for statements that discard a `Result`, or a `Task` or `ValueTask` of one, so
    a failure is lost.
  - `AHRF011` (Info), with a code fix for the conditional form, for a failure passed on unchanged by
    hand where `Bind` or `BindAsync` applies, including one wrapped in `Task.FromResult` or
    `ValueTask.FromResult`.

## [0.1.1]

### Added

- `net10.0` build of `Ahr.Foundation` is now marked `IsAotCompatible`, and the NuGet package
  displays as NativeAOT and trim compatible. No source or behavior changes; the library already
  used no reflection or dynamic code generation.
- `scripts/verify-aot.sh` publishes the packaged sample consumer with `PublishAot=true` and runs
  the resulting native binary as a release-time regression check.

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

[Unreleased]: https://github.com/AHRConsulting/Ahr.Foundation/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/AHRConsulting/Ahr.Foundation/compare/v0.1.1...v0.2.0
[0.1.1]: https://github.com/AHRConsulting/Ahr.Foundation/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/AHRConsulting/Ahr.Foundation/releases/tag/v0.1.0
