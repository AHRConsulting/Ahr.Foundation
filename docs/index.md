# Overview & Guide

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
| `Ensure` | — (takes a predicate and an error) | `.Ensure(o => o.Total > 0, error)` |
| `Where` | — (takes a predicate and keeps or removes an option) | `.Where(o => o.Total > 0)` |
| `Tap` | — (takes an action, value passes through) | `.Tap(o => Log(o.Id))` |
| `Match` | the final value, handling both branches | `.Match(ok => …, err => …)` |

Use `Map` when your function returns a plain value and `Bind` when it returns another `Result` or
`Option` of the corresponding family. Use `MapError` to transform only a failure. Convert between
the two models with `ToOption` and `ToResult`.

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

Use `Option<T>` when absence is expected, and `Result<T>` when absence is a failure that should
carry an error. `Option<T>` filters with `Where`; `Result<T>` uses `Ensure`, which also takes the
error to return on rejection.

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

- `OperationCanceledException` (including `TaskCanceledException`) always propagates unchanged; it
  is never converted into a `Failure`.
- The built-in-`Error` overloads (`Try`, `Try<T>`, `TryAsync`, `TryAsync<T>`) use
  `Error.FromException` automatically. The custom-error overloads (`Try<T, TError>`,
  `TryAsync<T, TError>`) require an explicit `Func<Exception, TError>` error factory.
- Invariant violations — a `null` operation or error factory, or a `null` value/error/task returned
  by the operation — are not swallowed. They propagate as real exceptions instead of silently
  becoming a domain `Failure`.

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

## AI Agent Skill

The [AHRConsulting/agent-skills](https://github.com/AHRConsulting/agent-skills) repository
publishes an installable `ahr-foundation` skill that teaches Copilot, Claude, and compatible
agents the conventions on this page: explicit `Success`/`Failure` construction, preferring
`Bind`/`Map`/`Ensure` chains over imperative branching, and using `Result.Try`/`Result.TryAsync`
at I/O boundaries.
