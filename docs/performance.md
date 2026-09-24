# Performance

Ahr.Foundation uses flat `readonly record struct` values and is designed to avoid managed
allocations in common synchronous workflows.

The included BenchmarkDotNet suite currently covers:

- `Result`, `Result<T>`, and `Result<T, TError>` construction.
- `Option<T>` `Some` and `None` construction.
- Success/Some and Failure/None composition through operations such as `Match`, `Map`, `Bind`,
  `Ensure`, and `Where`.
- Primitive and nested reference-type payloads.
- Task-based asynchronous composition, reported separately from synchronous operations.
- `Result.Try`/`Result.TryAsync` boundary-adapter helpers, success and exception paths, for the
  payload-free, standard-error, and custom-error result shapes.
- Managed allocations through BenchmarkDotNet's memory diagnoser.

Current measurements support describing construction and most synchronous composition as
allocation-free or allocation-conscious. Async operations can allocate for `Task` and async state
machinery, so the library does not claim zero allocations for every operation.

The suite is a regression baseline, not a claim that Ahr.Foundation is faster than other
libraries. A comparative ranking requires equivalent scenarios, versions, runtime settings, and
hardware for every library being compared.

## Run the benchmarks

From the repository root:

```bash
dotnet run -c Release --project benchmarks/Ahr.Foundation.Benchmarks
```

To run one primitive family:

```bash
dotnet run -c Release --project benchmarks/Ahr.Foundation.Benchmarks -- --filter "*Result*"
dotnet run -c Release --project benchmarks/Ahr.Foundation.Benchmarks -- --filter "*Option*"
```

BenchmarkDotNet writes generated reports beneath `artifacts/benchmarks`. These reports are
machine-specific and are not committed. For meaningful before-and-after comparisons, use the same
machine, runtime, power settings, command, and benchmark source.

Very small operations can be indistinguishable from the benchmark harness overhead. Treat a
reported `0 ns` as below the reliable measurement resolution, not as a literal zero-time
operation.

## NativeAOT and trimming

`Ahr.Foundation` targets `net10.0` with `IsAotCompatible` enabled: the library uses no reflection,
dynamic code generation, or unannotated generics, so it is compatible with `PublishAot` and with
trimming. The AOT/trim analyzer runs on every build of the library itself, so a change that
introduces an incompatible pattern is caught as a build warning before release. `scripts/verify-aot.sh`
publishes the packaged sample consumer with `PublishAot=true` and runs the resulting native binary
as a release-time regression check.
