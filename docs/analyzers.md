# Roslyn Analyzers

The `Ahr.Foundation` NuGet package includes `Ahr.Foundation.Analyzers`. The .NET SDK loads the
analyzer assembly automatically from the package's `analyzers/dotnet/cs` directory, so consumers
do not need to install or configure a second package. The analyzer is a build-time development
asset and does not add a runtime dependency.

## Rules

| Rule | Severity | Description |
| --- | --- | --- |
| `AHRF001` | Error | Result values must be created explicitly rather than with `default` or parameterless construction. |
| `AHRF002` | Info | Prefer `Result.Try` or `Result.TryAsync` when a manual try/catch only converts success and failure into a `Result`. |
| `AHRF003` | Warning | Use `Result.TryAsync` when the operation returns `Task`, `Task<T>`, `ValueTask`, or `ValueTask<T>`. |
| `AHRF004` | Warning | Do not pass an async delegate typed or cast as `Action` to `Result.Try`; post-await exceptions cannot be captured. |

All four rules are enabled by default.

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

`Option<T>` is intentionally different: its default value safely represents `None`, so
`AHRF001` does not report default option construction.

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

// AHRF003: the asynchronous operation must be awaited by TryAsync.
Result.Try(() => repository.LoadAsync());
Result<Order> order = await Result.TryAsync(() => repository.LoadAsync());
```

`AHRF004` covers the narrower async-void form, such as
`Result.Try((Action)(async () => await repository.SaveAsync()))`. Use `Result.TryAsync` instead so
the operation is awaited and exceptions become a `Failure`. The analyzer detects inline async
`Action` expressions, including explicit casts, but does not perform data-flow analysis to trace a
delegate variable back to the async lambda assigned to it.
