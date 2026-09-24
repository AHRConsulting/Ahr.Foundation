# Benchmarks

The BenchmarkDotNet project measures the public `Result` and `Option` APIs using primitive and
complex payloads. Construction, synchronous composition, and asynchronous composition are kept in
separate benchmark classes so allocation and task overhead remain visible.

Run all benchmarks from the repository root:

```bash
dotnet run -c Release --project benchmarks/Ahr.Foundation.Benchmarks
```

Run one family:

```bash
dotnet run -c Release --project benchmarks/Ahr.Foundation.Benchmarks -- --filter "*Result*"
dotnet run -c Release --project benchmarks/Ahr.Foundation.Benchmarks -- --filter "*Option*"
```

List benchmarks without executing them:

```bash
dotnet run -c Release --project benchmarks/Ahr.Foundation.Benchmarks -- --list flat
```

Reports are generated beneath `artifacts/benchmarks` and are intentionally excluded from source
control. Performance depends on the runtime, architecture, operating system, power state, and
hardware. Compare implementations using the same machine, runtime, command, and benchmark commit.

Sub-nanosecond results and `0 ns` results should be interpreted as indistinguishable from the
measurement overhead, not as literal zero-cost operations.
