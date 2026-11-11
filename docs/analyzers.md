# Roslyn Analyzers

The `Ahr.Foundation` NuGet package includes `Ahr.Foundation.Analyzers`. The .NET SDK loads the
analyzer assembly automatically from the package's `analyzers/dotnet/cs` directory, so consumers
do not need to install or configure a second package. The analyzer is a build-time development
asset and does not add a runtime dependency.

The analyzers load in any C# compiler from Roslyn 4.0 onward: the .NET 6 SDK or newer, and
Visual Studio 2022 17.0 or newer. An older compiler still builds the project and uses the library;
it reports `CS9057` or `CS8032` and skips only the analyzers.

## Rules

| Rule | Severity | Description |
| --- | --- | --- |
| `AHRF001` | Error | Result values must be created explicitly rather than with `default` or parameterless construction. |
| `AHRF002` | Info | Prefer `Result.Try` or `Result.TryAsync` when a manual try/catch only converts success and failure into a `Result`. |
| `AHRF003` | Warning | Use `Result.TryAsync` when the operation returns `Task`, `Task<T>`, `ValueTask`, or `ValueTask<T>`. |
| `AHRF004` | Warning | Do not pass an async delegate typed or cast as `Action` to `Result.Try`; post-await exceptions cannot be captured. |
| `AHRF005` | Info | Elements of a Result array created without initializers start uninitialized and must each be assigned before use. |
| `AHRF006` | Warning | `Map`, `MapAsync`, `MapSome`, `MapSomeAsync`, `Select`, `Try`, and `TryAsync` must not produce a nullable value; a null result throws. Use `Bind`, or return an `Option` from the operation. |
| `AHRF007` | Info | A `Result.TryAsync` operation uses a caller's cancellation token; pass it to the overload that takes a `CancellationToken`. |
| `AHRF008` | Warning | A `Tap`, `TapAsync`, `TapSome`, or `TapSomeAsync` function discards a result or option, so a failure is ignored. |
| `AHRF009` | Info | `GetValueOrDefault` or `ToOption` on a result of an option discards the failure; use the three-way `Match`, `SomeOr`, or `EnsureSome`. |
| `AHRF010` | Warning | A statement discards a `Result`, or a `Task` or `ValueTask` of one, so a failure is lost. |
| `AHRF011` | Info | A failure is passed on unchanged by hand; use `Bind` or `BindAsync`. |

All rules are enabled by default.

## Explicit Result Construction

`Result`, `Result<T>`, and `Result<T, TError>` have an uninitialized default state. `AHRF001`
reports detectable default construction before that value can reach runtime:

```csharp
Result invalid = default;       // AHRF001
Result valid = Result.Success();
```

The supplied code fix can replace a default-constructed payload-free `Result` with
`Result.Success()`. Generic results require choosing an intentional success value or failure:

```csharp
Result<User> user = Result<User>.Failure(new Error("User was not loaded."));
```

Arrays are reported separately. A Result array created without element initializers is a common
allocate-then-fill pattern, so `AHRF005` reports it at Info severity rather than failing the build:

```csharp
var results = new Result<Order>[ids.Length]; // AHRF005: assign every element before use
for (var i = 0; i < ids.Length; i++)
{
    results[i] = LoadOrder(ids[i]);
}
```

`Option<T>` is intentionally different: its default value safely represents `None`, so
neither `AHRF001` nor `AHRF005` reports default option construction.

## Mapping to Nullable Values

`Some` and `Success` never hold `null`, so `Map`, `MapAsync`, `MapSome`, `MapSomeAsync`, and `Select`
throw `ArgumentNullException` when the mapping returns `null`. `AHRF006` reports a mapping whose result
type is nullable, such as `string?` or `int?`, so the mistake appears at compile time instead:

```csharp
Option<string> middle = customer.Map(c => c.MiddleName);            // AHRF006
Option<string> middle = customer.Bind(c => c.MiddleName.ToOption()); // null becomes None

Result<string> city = order.Map(o => o.Address.City);               // AHRF006
Result<string> city = order.Bind(o => o.Address.City is { } value
    ? Result<string>.Success(value)
    : Result<string>.Failure(new Error("The order has no delivery city.")));
```

The rule relies on nullable annotations, so it reports only in a nullable-enabled context. LINQ
query syntax (`select c.MiddleName`) is not reported because the compiler does not track
nullability through query expressions. A mapping declared non-nullable that still returns `null`
at runtime continues to throw.

`Result.Try` and `Result.TryAsync` follow the same rule: an operation declared to return a nullable
type is reported, because a `null` success throws. Return an `Option` from the operation instead:

```csharp
Result<object> shown = await Result.TryAsync(presenter.ShowAsync, ct);                    // AHRF006 when ShowAsync returns Task<object?>
Result<Option<object>> shown = await Result.TryAsync(
    async token => (await presenter.ShowAsync(token)).ToOption(), ct);                    // null becomes None
```

## Safe Exception Adapters

The remaining rules protect the boundary between exception-based APIs and result-based code:

```csharp
// AHRF002: this shape can be replaced with Result.Try.
Result<Order> LoadOrder()
{
    try
    {
        return Result<Order>.Success(repository.Load());
    }
    catch (Exception exception)
    {
        return Result<Order>.Failure(Error.FromException(exception));
    }
}

// Not reported: Result.Try rethrows cancellation, so this catch cannot be replaced.
Result Save(CancellationToken cancellationToken)
{
    try
    {
        repository.Save(cancellationToken);
        return Result.Success();
    }
    catch (OperationCanceledException)
    {
        return Result.Failure(new Error("Cancelled."));
    }
}

// AHRF002 also reports a try/catch/finally. Keep the cleanup around the call:
try
{
    return Result.Try(() => repository.Load());
}
finally
{
    gate.Release();
}

// AHRF003: the asynchronous operation must be awaited by TryAsync.
_ = Result.Try(() => repository.LoadAsync());
Result<Order> order = await Result.TryAsync(() => repository.LoadAsync());
```

`AHRF004` covers the narrower async-void form, such as
`Result.Try((Action)(async () => await repository.SaveAsync()))`. Use `Result.TryAsync` instead so
the operation is awaited and exceptions become a `Failure`. The analyzer detects inline async
`Action` expressions, including explicit casts, but does not perform data-flow analysis to trace a
delegate variable back to the async lambda assigned to it.

## Cancellation-Aware TryAsync

`Result.TryAsync` overloads without a token propagate every `OperationCanceledException`, including
cancellations the caller never requested. `HttpClient`, for example, reports a timeout as a
`TaskCanceledException`, so the timeout escapes instead of becoming a `Failure`. The overloads that
take a `CancellationToken` pass the token to the operation, propagate only that token's
cancellation, and convert any other cancellation into a `Failure`. A token that is already canceled
throws before the operation runs.

`AHRF007` reports a `TryAsync` operation that uses a cancellation token from the surrounding code: a
parameter, a local, or a member such as `HttpContext.RequestAborted`. `CancellationToken.None`,
`default`, and tokens created inside the operation are not reported.

```csharp
// AHRF007: the operation uses 'cancellationToken'
Result<string> body = await Result.TryAsync(() => client.GetStringAsync(url, cancellationToken));

// After the code fix
Result<string> body = await Result.TryAsync(token => client.GetStringAsync(url, token), cancellationToken);
```

The rule reports at Info severity because the existing call is not wrong: it changes only how
cancellations other than the caller's surface. Review code that catches `TaskCanceledException`
around the call before applying the fix. The code fix runs only when invoked, supports Fix All, and
is not offered when the operation uses more than one distinct token.

## Discarded Results in Tap

A `Tap` function that returns the same family stops the pipeline on failure or `None`. When the
function discards the value instead, the failure goes unnoticed, so `AHRF008` reports it:

```csharp
Result<int> result = LoadTotal();

_ = result.Tap(total => { Save(total); });      // AHRF008: block body has no return
_ = option.Tap(value => Save(value));           // AHRF008: an option cannot carry the failure
_ = customError.Tap(value => Save(value));      // AHRF008: a different error type

_ = result.Tap(total => Save(total));           // a failure stops the pipeline
_ = result.Tap(total => { _ = Save(total); });  // ignored deliberately
```

Return a value of the same family, convert it, or use `Bind`. Assign the value to `_` when ignoring it
is intentional. `TapSome` and `TapSomeAsync` on `Result<Option<T>>` are checked the same way.
`TapError` and nested lambdas are not analyzed.

## Discarded Results

A `Result` is often the only signal that an operation failed. `AHRF010` reports a statement that drops
one, including an awaited call, a call that is not awaited, and a statement in a lambda with no return
value:

```csharp
Save(order);                         // AHRF010
await SaveAsync(order, ct);          // AHRF010: awaiting does not handle the failure
SaveAsync(order, ct);                // AHRF010: not awaited
LoadOrder(id).Tap(Use);              // AHRF010: Tap runs only on success
Action save = () => Save(order);     // AHRF010

Result saved = Save(order);          // assigned
Save(order).TapError(Log);           // the chain ends by handling the failure
await SaveAsync(order, ct).TapErrorAsync(Log);
_ = Save(order);                     // ignored deliberately
```

A `Match` that takes actions handles the failure, so it is not reported, even though it returns the
result. A `Match` whose functions return a result is reported. `Option<T>` is not analyzed, because `None` is not a
failure. A drop inside a `Tap` function is reported by `AHRF008` instead. The rule treats `_ =` as a
decision, so review these assignments as you would any ignored error. A `Task<Result>` converted to a
plain `Task`, such as `Func<Task> save = () => SaveAsync(order, ct);`, is not detected.

To allow discards in a project such as a test project, lower the severity in `.editorconfig`:

```ini
[tests/**.cs]
dotnet_diagnostic.AHRF010.severity = none
```

## Failures Passed On by Hand

`AHRF011` reports a check that returns the failure unchanged, which is what `Bind` and `BindAsync` do:

```csharp
return loaded.IsFailure ? Result<Page>.Failure(loaded.Error) : Build(loaded.Value);  // AHRF011
return loaded.Bind(value => Build(value));                                           // after the code fix

if (loaded.IsFailure)                                                                // AHRF011
{
    return Result<Page>.Failure(loaded.Error);
}
```

A failure wrapped in `Task.FromResult` or `ValueTask.FromResult`, in a method that returns a task
without being `async`, is reported too; `BindAsync` replaces it.

The code fix rewrites the conditional expression to `Bind`, except when the failure is wrapped in a
task. An early return has no automatic fix,
because the rest of the method would move into the `Bind` function. A failure that is changed, logged,
or combined with another result is not reported, nor is a check inside a loop or a `try`, `using`, or
`lock` block. The failure must keep the same error type, so `Bind` is a direct replacement.
