# Overview & Guide

## Installation

Install the current stable package explicitly:

```bash
dotnet add package Ahr.Foundation --version 0.2.0
```

## Railway-Oriented Programming (ROP)

Operations compose as two tracks: **Success** and **Failure**. `Bind` continues on the success
track and switches to the failure track at the first error, skipping every later step.

```csharp
using Ahr.Foundation;

public Result<User> Register(string email, int age)
{
    return ValidateEmail(email)
        .Bind(validEmail => ValidateAge(age).Map(validAge => new User(validEmail, validAge)));
}
```

## Core Types

Start with the type that matches the meaning of the operation:

- `Result` represents success or failure when there is no success payload.
- `Result<T>` represents a successful value or a built-in `Error`.
- `Result<T, TError>` represents a successful value or an application-specific error type.
- `Option<T>` represents a value that may be absent when absence is expected rather than erroneous.
- `Error` is the built-in failure type used by `Result` and `Result<T>`.

Use `Option<T>` for an optional lookup, such as finding the first matching item. Use `Result<T>`
when the operation must explain why it could not produce a value.

```csharp
Option<string> adminName = users.FirstOrNone(user => user.IsAdmin)
    .Map(user => user.Name);

Result<User> user = repository.Find(userId)
    .ToResult(new Error($"User '{userId}' was not found."));
```

## Choosing a Combinator

| Use | When the function you pass returns | Example |
| --- | --- | --- |
| `Map` | a plain value | `.Map(user => user.Name)` |
| `Bind` | another `Result` or `Option` of the corresponding family | `.Bind(user => LoadOrders(user))` |
| `MapError` | a transformed error | `.MapError(error => error with { Message = "Unavailable" })` |
| `Ensure` | — (takes a predicate and an error or message) | `.Ensure(o => o.Total > 0, "Empty order")` |
| `Where` | — (takes a predicate and keeps or removes an option) | `.Where(o => o.Total > 0)` |
| `Tap` | nothing, or a `Result` or `Option` that can stop the pipeline; the value passes through | `.Tap(o => Save(o))` |
| `Match` | the final value, handling both branches | `.Match(ok => …, err => …)` |

`Tap` keeps the value and `Bind` replaces it. On every result and option type, a `Tap` function that
returns the same family stops the pipeline when it fails or returns `None`:

```csharp
Result<Order> saved = ValidateOrder(request)
    .Tap(order => repository.Save(order)) // a failure stops the pipeline
    .Tap(order => Log(order.Id));         // returns nothing, so it just runs
```

A block body without a `return`, such as `order => { repository.Save(order); }`, discards the returned
result, as does `async order => { await repository.SaveAsync(order); }`. So does a function returning
the other family or a different error type, such as a `Result` passed to `Option<T>.Tap`, because the
failure cannot be carried. Analyzer `AHRF008` reports these. Convert the value, use `Bind`, or write
`_ = repository.Save(order);` to ignore it deliberately.

Use `Map` when your function returns a plain value and `Bind` when it returns another `Result` or
`Option` of the corresponding family. Use `MapError` to transform only a failure; a function that
returns a custom error type turns `Result<T>` into `Result<T, TError>`. Payload-free `Result` has
`MapError(Func<Error, Error>)` too, for rewording or wrapping a failure. Convert between the two models
with `ToOption` and `ToResult`, and use `Result<T>.ToResult()` to drop a value and keep only success or
failure:

```csharp
Result<User, ApiError> user = await LoadUserAsync(id).MapErrorAsync(error => ApiError.From(error));
Result restored = await store.LoadAsync(key).TapSomeAsync(page.Restore).ToResultAsync();
```

## LINQ Query Syntax

`Select` and `SelectMany` are available on `Result<T>`, `Result<T, TError>`, and `Option<T>`, so
query syntax composes the same pipeline with the same short-circuiting:

```csharp
Result<decimal> total =
    from user in FindUser(userId)
    from cart in LoadCart(user)
    from priced in PriceCart(cart)
    select priced.Total;
```

If `FindUser` fails, neither `LoadCart` nor `PriceCart` runs and the original error is returned.

Query syntax suits pipelines where later steps need values bound earlier. For short chains prefer
`Map` and `Bind`. Query syntax is **synchronous only**; use `BindAsync` and `MapAsync` for
`Task<Result<T>>` pipelines. The same async combinators are available for `Task<Option<T>>` and
`Task<Result<T, TError>>` through `TaskCompositionExtensions`.

## Options

`Option<T>` provides explicit handling of optional presence:

```csharp
Option<string> item = users.FirstOrNone(u => u.IsAdmin).Map(u => u.Name);
```

Sequence and dictionary helpers return options instead of defaults or exceptions:

```csharp
Option<User> admin = users.FirstOrNone(u => u.IsAdmin);
Option<User> owner = members.SingleOrNone(m => m.IsOwner); // throws if more than one matches
Option<Order> latest = orders.LastOrNone();
Option<decimal> price = prices.GetValueOrNone("apple");     // IReadOnlyDictionary
IEnumerable<User> found = ids.Select(cache.Find).Values();  // keeps Some values, skips None
```

A null element or stored value is treated as absent. `SingleOrNone` throws
`InvalidOperationException` when more than one element matches, like `SingleOrDefault`.

Use `Option<T>` when absence is expected, and `Result<T>` when absence is a failure that should
carry an error. `Option<T>` filters with `Where`; `Result<T>` uses `Ensure`, which also takes the
error to return on rejection.

### Lookups That Can Fail

A database or remote lookup can fail or find nothing, and those are different outcomes.
`Result<Option<T>>` and `Result<Option<T>, TError>` keep them apart. `Match` and `MatchAsync` handle
all three cases without nesting:

```csharp
return await repository.FindAsync(id).MatchAsync(
    onSome: user => Ok(user),
    onNone: () => NotFound(),
    onFailure: error => Problem(error.Message));
```

To run code instead of producing a value, pass actions. All three handlers are required, and the original
result is returned:

```csharp
picked.Match(
    onSome: order => Select(order),
    onNone: () => { },
    onFailure: error => ShowError(error));
```

`EnsureSome` and `EnsureSomeAsync` treat a missing value as a failure and continue the pipeline. An
existing failure is kept:

```csharp
Result<string> name = await repository.FindAsync(id)
    .EnsureSomeAsync(new Error("User not found."))
    .MapAsync(user => user.Name);
```

`SomeOr` and `SomeOrAsync` use a fallback value instead, and `BindNone` and `BindNoneAsync` try another
lookup when nothing was found. A failure is kept by both:

```csharp
Result<Settings> settings = await store.FindAsync(userId).SomeOrAsync(Settings.Default);

Result<Option<User>> user = await cache.FindAsync(id).BindNoneAsync(() => database.FindAsync(id));
```

`TapSome`, `MapSome`, and `BindSome`, with their async forms, continue the chain from a found value. A missing
value or a failure passes through. `TapSome` keeps the value and, like `Tap`, stops the pipeline when its
step returns a failure. `BindSome` continues with another lookup of the same shape:

```csharp
Result<Option<PageState>> state = await store.LoadAsync(key)
    .TapSomeAsync(saved => Result.Try(() => page.RestoreState(saved)));
```

```csharp
string text = await customers.FindAsync(id)
    .BindSomeAsync(customer => orders.FindFirstAsync(customer.Id))
    .MapSomeAsync(order => order.Number)
    .MatchAsync(number => number, () => "No order", error => error.Message);
```

| Goal | Use |
| --- | --- |
| End the chain and handle all three outcomes, returning a value or running actions | `Match`, `MatchAsync` |
| Run a step for a found value only, stopping if it fails | `TapSome`, `TapSomeAsync` |
| Change a found value | `MapSome`, `MapSomeAsync` |
| Continue with another lookup | `BindSome`, `BindSomeAsync` |
| Treat a missing value as a failure | `EnsureSome`, `EnsureSomeAsync` |
| Use a default for a missing value | `SomeOr`, `SomeOrAsync` |
| Try another lookup for a missing value | `BindNone`, `BindNoneAsync` |

### Lists of Results

`Traverse` runs a step that can fail on each item, in order. It returns all the values, or the first
failure, and does not run the items after it. `Sequence` does the same for results that already exist:

```csharp
Result<IReadOnlyList<Order>> orders = ids.Traverse(id => repository.Load(id));

Result<IReadOnlyList<int>> numbers = inputs.Select(Parse).Sequence();
```

`Partition` runs every item and returns the values and the errors separately, for batch work where
one failure should not stop the rest. On `Result`, which has no value, it returns the errors:

```csharp
var (written, errors) = await drafts.PartitionAsync(draft => WriteAsync(draft));

return errors.Count == 0 ? Done(written) : PartialFailure(written, errors);
```

All three work on `Result`, `Result<T>`, and `Result<T, TError>`. `TraverseAsync` and `PartitionAsync`
run one item at a time. `SequenceAsync` and `PartitionAsync()` on a list of tasks await each task in
order; `SequenceAsync` stops awaiting at the first failure, but a task that has already started keeps
running.

| Goal | Use |
| --- | --- |
| Run a step on each item and stop at the first failure | `Traverse`, `TraverseAsync` |
| Turn results that already exist into one result | `Sequence`, `SequenceAsync` |
| Run every item and keep every error | `Partition`, `PartitionAsync` |

Use `Bind(inner => inner)` to flatten a nested result such as `Result<Result>` or
`Result<Result<T, TError>, TError>`.

`GetValueOrDefault` returns `None` for a failure, and `ToOption` returns a nested option without the
error. Analyzer `AHRF009` points out these calls.

## Async Composition

The main combinators have asynchronous equivalents. `TaskCompositionExtensions` lets a
`Task<Result<T>>`, `Task<Result<T, TError>>`, or `Task<Option<T>>` continue as a fluent pipeline
without awaiting each intermediate operation manually.

```csharp
Result<OrderConfirmation> confirmation = await ValidateOrder(request)
    .BindAsync(order => inventory.ReserveAsync(order))
    .BindAsync(reservation => payments.CaptureAsync(reservation))
    .MapAsync(payment => new OrderConfirmation(payment.TransactionId))
    .TapAsync(value => notifications.SendAsync(value));
```

Use `MapAsync` and `BindAsync` for asynchronous transformations, `EnsureAsync` and `WhereAsync`
for asynchronous predicates, and `TapAsync`/`TapErrorAsync` for asynchronous side effects. The
pipeline still short-circuits on the first failure or `None`.

Each of these task extensions also accepts a synchronous delegate, so synchronous steps fit into an
asynchronous pipeline without an intermediate `await`. `MatchAsync` ends the pipeline with either
synchronous or asynchronous branches:

```csharp
string message = await ValidateOrder(request)
    .BindAsync(order => inventory.ReserveAsync(order))
    .MapAsync(reservation => reservation.Total)          // synchronous step
    .EnsureAsync(total => total > 0, new Error("Empty order"))
    .MatchAsync(total => $"Reserved {total:C}", error => error.Message);
```

Use `ToResultAsync` to continue from an optional lookup as a result pipeline:

```csharp
Result<Receipt> receipt = await repository.FindAsync(id)
    .ToResultAsync(new Error("Order not found."))
    .BindAsync(order => ChargeAsync(order));
```

## Boundary Exception Handling

At the edges of a system — I/O, database calls, third-party SDKs — exceptions are often the only
signal of failure. `Result.Try`/`Result.TryAsync` convert that signal into a `Result` without
repeating the same try/catch shape at every call site:

```csharp
// Built-in Error, synchronous
Result<Order> order = Result.Try(() => repository.GetOrder(orderId));

// Built-in Error, asynchronous
Result<Customer> customer = await Result.TryAsync(() => repository.GetCustomerAsync());

// Custom domain error, asynchronous
Result<Customer, DomainError> customer = await Result.TryAsync(
    () => repository.GetCustomerAsync(),
    ex => DomainError.FromException(ex));
```

Rules that apply to every `Try`/`TryAsync` overload:

- Overloads without a token propagate every `OperationCanceledException` (including
  `TaskCanceledException`) unchanged. The cancellation-aware overloads described below propagate only
  the caller's cancellation.
- The built-in-`Error` overloads (`Try`, `Try<T>`, `TryAsync`, `TryAsync<T>`) use
  `Error.FromException` automatically. The custom-error overloads (`Try<T, TError>`,
  `TryAsync<T, TError>`) require an explicit `Func<Exception, TError>` error factory.
- Invariant violations — a `null` operation or error factory, or a `null` value/error/task returned
  by the operation — are not swallowed. They propagate as real exceptions instead of silently
  becoming a domain `Failure`.

### Catching Only Some Exceptions

Pass a `catchWhen` filter to turn only the expected exceptions into a failure. Any other exception
propagates unchanged, as with a C# `when` filter, so a programming error is not hidden as a failure:

```csharp
Result<string> body = await Result.TryAsync(
    token => client.GetStringAsync(url, token),
    catchWhen: ex => ex is HttpRequestException or IOException,
    cancellationToken);
```

Every `Try` and `TryAsync` overload has a `catchWhen` form. Cancellation is handled before the filter
runs: the caller's cancellation propagates, and with a token, a timeout always becomes a failure.

### Cleanup

Keep a `finally` block around the call, or put a `using` declaration inside the operation:

```csharp
try
{
    return await Result.TryAsync(token => client.SendAsync(request, token), cancellationToken);
}
finally
{
    gate.Release();
}
```

### Cancellation and Timeouts

Overloads without a token propagate every `OperationCanceledException`, including an `HttpClient`
timeout, which surfaces as `TaskCanceledException`. Pass the caller's token to the
cancellation-aware overload so only that cancellation propagates and every other cancellation
becomes a `Failure`:

```csharp
Result<string> body = await Result.TryAsync(
    token => client.GetStringAsync(url, token),
    cancellationToken);
```

The operation receives the token, and a token that is already canceled throws before the operation
runs. Analyzer `AHRF007` suggests this overload when an operation uses a caller's token.

When the caller's cancellation should be a failure too, chain `CatchCancellationAsync` with the error
to return. Only cancellation by the given token is caught; a timeout is already a failure, and any other
exception still throws:

```csharp
Result<Page> page = await Result.TryAsync(token => LoadAsync(token), cancellationToken)
    .CatchCancellationAsync(Errors.Cancelled, cancellationToken);
```

It works on any `Task<Result>`, `Task<Result<T>>`, or `Task<Result<T, TError>>`, not only one from
`TryAsync`, and a form that takes a function builds the error from the exception.

| What happened | Outcome |
| --- | --- |
| The caller canceled | Throws, or returns your error with `CatchCancellationAsync` |
| A timeout or other cancellation | A failure whose `Error.IsCancellation` is `true` |
| An exception that `catchWhen` accepts | A failure whose `Error.IsCancellation` is `false` |
| Any other exception | Throws |

`Error.IsCancellation` reports whether a failure came from a cancellation, such as that timeout:

```csharp
Result<string> classified = await Result.TryAsync(token => client.GetStringAsync(url, token), cancellationToken)
    .TapErrorAsync(error => logger.Log(error.IsCancellation ? "Timed out" : error.Message));
```

The package's Roslyn analyzers identify manual exception-adapter patterns and unsafe calls that
would leave asynchronous exceptions unobserved. See the [analyzer guide](analyzers.md) for every
rule, severity, and remediation.

## Extracting Values Without Branching

`GetValueOrDefault()` and `GetValueOrDefault(T fallback)` on `Result<T>` and `Result<T, TError>`
mirror `Option<T>`'s existing helpers, letting you read the success payload (or a default/fallback)
without an explicit `IsSuccess` check or `Match` call:

```csharp
int total = Result<int>.Success(42).GetValueOrDefault(); // 42
int missing = Result<int>.Failure(new Error("bad")).GetValueOrDefault(); // 0
string label = Result<string, string>.Failure("bad").GetValueOrDefault("unknown"); // "unknown"
```

## Falling Back to an Alternative

`OrElse` and `OrElseAsync` on `Result`, `Result<T>`, `Result<T, TError>`, and `Option<T>` return the
original value unchanged when it is a success/`Some`, or a fallback of the same type when it is a
failure/`None`. Unlike `GetValueOrDefault`, which extracts a plain payload value, `OrElse` returns
another `Result`/`Option`, so the fallback itself can be chained further:

```csharp
Result<Customer> customer = repository.FindLocal(id)
    .OrElse(() => repository.FindRemote(id));

Option<Settings> settings = userSettings
    .OrElse(defaultSettings)
    .OrElseAsync(() => settingsService.LoadFromDiskAsync());
```

The eager overload (`OrElse(fallback)`) accepts an already-constructed fallback. The lazy overload
(`OrElse(Func<fallback> factory)`) only invokes the factory when the original is a failure/`None`,
avoiding the cost of constructing a fallback that would be discarded on the success path.
`OrElseAsync(Func<Task<fallback>> factory)` is the asynchronous equivalent. The factory takes no
parameters — it does not receive the error or `None` state, keeping the shape identical across all
four types.

`TaskCompositionExtensions` also provides `OrElseAsync(Func<Task<fallback>> factory)` for
`Task<Result>`, `Task<Result<T>>`, `Task<Result<T, TError>>`, and `Task<Option<T>>`, so you can
chain `.OrElseAsync(...)` directly off an unawaited task without an intermediate `await`:

```csharp
Result<Customer> customer = await repository.FindLocalAsync(id)
    .OrElseAsync(() => repository.FindRemoteAsync(id));
```

## Upgrading from 0.1.1

0.2.0 includes these changes that can affect existing code:

| Change | What to do |
| --- | --- |
| `Tap` and `TapAsync` stop the pipeline when the function returns a failure of the same family, or `None` on `Option<T>`. There is no compile error. | Review `Tap` calls whose function returns a `Result` or `Option`. Write `_ = operation();` in a block body where a failure should still be ignored. |
| `FirstOrNone` returns `None` for a null first element instead of throwing. | Remove any handling for the old `ArgumentNullException`. |
| `ToOption()` on a nullable value type returns `Option<T>` rather than `Option<T?>`. | Update code that declares or passes `Option<int?>` for these calls. |
| `AHRF006` reports `Map`, `MapAsync`, `MapSome`, `MapSomeAsync`, `Select`, `Try`, and `TryAsync` producing a nullable value. | Use `Bind` with `ToOption()` or an explicit failure, or return an `Option` from the `Try` operation. |
| `AHRF008` reports a `Tap` function that discards a result or option. | Return the same family, use `Bind`, or assign the value to `_`. |
| `AHRF010` reports a statement that discards a result. | Handle or return the result, end the chain with `TapError`, or assign the value to `_`. |
| A result array created without initializers moves from `AHRF001` (Error) to `AHRF005` (Info). | Update any suppression or severity configuration for those arrays. |
| `Ensure(predicate, default)` no longer compiles, because it is ambiguous. | Remove the call; it always threw at runtime. |

`AHRF006`, `AHRF008`, and `AHRF010` are warnings, so they fail the build in projects that treat warnings as errors.
See the [analyzer guide](analyzers.md) and the
[changelog](https://github.com/AHRConsulting/Ahr.Foundation/blob/main/CHANGELOG.md) for details.

## Agent Skill

The [AHRConsulting/agent-skills](https://github.com/AHRConsulting/agent-skills) repository
publishes an installable `ahr-foundation` skill for
agents the conventions on this page: explicit `Success`/`Failure` construction, preferring
`Bind`/`Map`/`Ensure` chains over imperative branching, and using `Result.Try`/`Result.TryAsync`
at I/O boundaries.
