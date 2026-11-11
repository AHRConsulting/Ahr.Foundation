# Ahr.Foundation

Allocation-conscious `Result` and `Option` types for .NET, with railway-oriented composition, async
pipelines, and bundled Roslyn analyzers that catch misuse at compile time.

```bash
dotnet add package Ahr.Foundation --version 0.2.0
```

Targets .NET Standard 2.0 and .NET 10, with no runtime dependencies. The .NET 10 build is trimming
and NativeAOT compatible.

## Results

```csharp
using Ahr.Foundation;

Result<Order> order = ValidateOrder(request)
    .Bind(valid => PriceOrder(valid))
    .Ensure(priced => priced.Total > 0, "The order is empty.")
    .Tap(priced => repository.Save(priced)); // a failure stops the pipeline; the order is kept

string message = order.Match(
    priced => $"Total: {priced.Total}",
    error => error.Message);
```

The first failure short-circuits every later step. Create results with `Success` or `Failure`; a
default-initialized result is uninitialized and reported by analyzer `AHRF001`.

## Async pipelines

Extensions on an unawaited `Task` accept synchronous and asynchronous steps:

```csharp
string message = await LoadOrderAsync(id)
    .MapAsync(order => order.Total)
    .BindAsync(total => ChargeAsync(total))
    .MatchAsync(receipt => $"Charged {receipt.Id}", error => error.Message);
```

## Options

Use `Option<T>` when absence is expected rather than an error:

```csharp
Option<User> admin = users.FirstOrNone(user => user.IsAdmin);
Option<decimal> price = prices.GetValueOrNone("apple");

Result<User> user = await repository.FindAsync(id)
    .ToResultAsync(new Error($"User '{id}' was not found."));
```

`FirstOrNone`, `SingleOrNone`, `LastOrNone`, `GetValueOrNone`, and `Values` bridge sequences and
dictionaries to options.

A lookup that can fail or find nothing returns `Result<Option<T>>`, which keeps the three outcomes
apart without nesting:

```csharp
string text = await repository.FindAsync(id).MatchAsync(
    onSome: user => user.Name,
    onNone: () => "Not found",
    onFailure: error => error.Message);

Result<Settings> settings = await store.FindAsync(userId).SomeOrAsync(Settings.Default);
```

`TapSome`, `MapSome`, and `BindSome` continue from a found value, and a failing `TapSome` step stops the pipeline like `Tap`. `EnsureSome` treats a missing value as
a failure, `SomeOr` uses a fallback value, and `BindNone` tries another lookup.

## Lists of results

`Traverse` runs a step that can fail on each item and stops at the first failure. `Sequence` does the
same for results that already exist. `Partition` runs every item and keeps every error:

```csharp
Result<IReadOnlyList<Report>> reports = await inputs.TraverseAsync(input => LoadAsync(input));

var (written, errors) = await drafts.PartitionAsync(draft => WriteAsync(draft));
```

Each has an async form that runs one item at a time, in order.

## Exceptions at the boundary

`Result.TryAsync` turns exceptions from I/O and third-party code into failures. Pass the caller's
token so only that cancellation propagates and other cancellations, such as timeouts, become a
failure:

```csharp
Result<string> body = await Result.TryAsync(
    token => httpClient.GetStringAsync(url, token),
    cancellationToken);
```

Pass `catchWhen: ex => ex is HttpRequestException` to turn only expected exceptions into failures,
and check `error.IsCancellation` to tell a timeout from another failure. Chain
`.CatchCancellationAsync(Errors.Cancelled, cancellationToken)` when the caller's cancellation should
return a failure instead of throwing.

## Types

| Type | Use it for |
| --- | --- |
| `Result` | Success or failure without a value |
| `Result<T>` | A value or a built-in `Error` |
| `Result<T, TError>` | A value or your own error type |
| `Option<T>` | A value that may be absent |
| `Error` | A failure message with an optional exception |

## Analyzers

| Rule | Severity | Reports |
| --- | --- | --- |
| `AHRF001` | Error | `default` or parameterless construction of a result |
| `AHRF002` | Info | A manual try/catch that `Result.Try` or `Result.TryAsync` can replace |
| `AHRF003` | Warning | An awaitable operation passed to synchronous `Result.Try` |
| `AHRF004` | Warning | An async `Action` delegate passed to `Result.Try` |
| `AHRF005` | Info | A result array whose elements start uninitialized |
| `AHRF006` | Warning | `Map`, `MapAsync`, `MapSome`, `MapSomeAsync`, `Select`, `Try`, or `TryAsync` producing a nullable value |
| `AHRF007` | Info | A `TryAsync` operation that uses a cancellation token without passing it |
| `AHRF008` | Warning | A `Tap` function that discards a result or option |
| `AHRF009` | Info | `GetValueOrDefault` or `ToOption` on a result of an option discards the failure |
| `AHRF010` | Warning | A statement that discards a result, so a failure is lost |
| `AHRF011` | Info | A failure passed on by hand that `Bind` or `BindAsync` can replace |

## Learn more

- [Documentation and guide](https://ahrconsulting.github.io/Ahr.Foundation/)
- [Analyzer rules](https://ahrconsulting.github.io/Ahr.Foundation/docs/analyzers.html)
- [API reference](https://ahrconsulting.github.io/Ahr.Foundation/api/Ahr.Foundation.html)
- [Changelog](https://github.com/AHRConsulting/Ahr.Foundation/blob/main/CHANGELOG.md)
- [Source and issues](https://github.com/AHRConsulting/Ahr.Foundation)
- [Agent skill for AI coding assistants](https://github.com/AHRConsulting/agent-skills)

Licensed under the Apache License, Version 2.0.
