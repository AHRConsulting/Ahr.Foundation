namespace Ahr.Foundation.Tests;

public sealed class ResultTryTests
{
    private static readonly BusinessContext _tryBoundaries = BusinessContext
        .Define(
            "reliable-operation-boundaries",
            "Reliable operation boundaries",
            "Capture operational failures as results while preserving cancellation and invalid-operation behavior.")
        .Capability("capture-operation-failures", "Translate ordinary operation exceptions into failures")
        .Capability("preserve-operation-outcomes", "Keep successful values and execute operations exactly once")
        .Outcome("cancellation-propagates", "Cancellation exceptions are never converted into ordinary failures")
        .Rule("invalid-delegates-rejected", "Missing operations and invalid returned values are rejected")
        .Build();

    [Fact]
    public Task Try_Action_Success_ReturnsSuccess()
    {
        var invocations = 0;
        Result? result = null;

        return Scenario.Define("A successful operation produces a successful result", _tryBoundaries)
            .When("The operation completes normally", () =>
            {
                result = Result.Try(() => { invocations++; });
            })
            .Then("The operation is invoked once and succeeds", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(1, invocations);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Try_Action_ThrownException_ReturnsFailureWithFromException()
    {
        var thrown = new InvalidOperationException("boom");
        Result? result = null;

        return Scenario.Define("An operation failure is represented by its exception error", _tryBoundaries)
            .When("The operation throws an ordinary exception", () =>
            {
                result = Result.Try(() => throw thrown);
            })
            .Then("The failure retains the original exception and message", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Same(thrown, result?.Error.Exception);
                Assert.Equal("boom", result?.Error.Message);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Try_Action_OperationCanceledException_Propagates()
    {
        OperationCanceledException cancellation = new();

        return Scenario.Define("Synchronous cancellation propagates through the operation boundary", _tryBoundaries)
            .WhenThrows<OperationCanceledException>(
                "The operation raises cancellation",
                () => _ = Result.Try(() => throw cancellation))
            .Then("The original cancellation exception is preserved", (OperationCanceledException exception) =>
                Assert.Same(cancellation, exception))
            .VerifyAsync();
    }

    [Fact]
    public Task Try_Action_TaskCanceledException_Propagates()
    {
        TaskCanceledException cancellation = new();

        return Scenario.Define("A task cancellation exception propagates through the operation boundary", _tryBoundaries)
            .WhenThrows<TaskCanceledException>(
                "The operation raises task cancellation",
                () => _ = Result.Try(() => throw cancellation))
            .Then("The original task cancellation exception is preserved", (TaskCanceledException exception) =>
                Assert.Same(cancellation, exception))
            .VerifyAsync();
    }

    [Fact]
    public Task Try_Action_NullOperation_ThrowsWithOperationParameter() =>
        Scenario.Define("A missing synchronous operation is rejected", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "Try receives no operation",
                () => _ = Result.Try(null!))
            .Then("The refusal identifies the missing operation", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryOfT_Success_ReturnsSuccessWithValue()
    {
        Result<int>? result = null;

        return Scenario.Define("A successful value operation preserves its value", _tryBoundaries)
            .When("The operation returns a value", () =>
            {
                result = Result.Try(() => 42);
            })
            .Then("The result contains the returned value", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(42, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryOfT_ThrownException_ReturnsFailureWithFromException()
    {
        var thrown = new InvalidOperationException("boom");
        Result<int>? result = null;

        return Scenario.Define("A failed value operation retains its original exception", _tryBoundaries)
            .When("The operation throws an ordinary exception", () =>
            {
                result = Result.Try<int>(() => throw thrown);
            })
            .Then("The failure contains the thrown exception", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Same(thrown, result?.Error.Exception);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryOfT_OperationCanceledException_Propagates()
    {
        OperationCanceledException cancellation = new();

        return Scenario.Define("Cancellation from a value operation propagates", _tryBoundaries)
            .WhenThrows<OperationCanceledException>(
                "The value operation raises cancellation",
                () => _ = Result.Try<int>(() => throw cancellation))
            .Then("The original cancellation exception is preserved", (OperationCanceledException exception) =>
                Assert.Same(cancellation, exception))
            .VerifyAsync();
    }

    [Fact]
    public Task TryOfT_NullOperation_ThrowsWithOperationParameter() =>
        Scenario.Define("A missing value operation is rejected", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "Try receives no value operation",
                () => _ = Result.Try<int>(null!))
            .Then("The refusal identifies the missing operation", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryOfT_NullReturnedValue_ThrowsArgumentNullException_NotConvertedToFailure() =>
        Scenario.Define("A null returned value is rejected rather than captured", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "The operation returns a null value",
                () => _ = Result.Try<string>(() => null!))
            .Then("The refusal identifies the operation result", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryOfT_Success_ExecutesOperationExactlyOnce()
    {
        var invocations = 0;
        Result<int>? result = null;

        return Scenario.Define("A successful value operation executes exactly once", _tryBoundaries)
            .When("The operation returns its invocation count", () =>
            {
                result = Result.Try(() =>
                {
                    invocations++;
                    return invocations;
                });
            })
            .Then("The successful result and invocation count are both one", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(1, invocations);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryOfTAndTError_Success_ReturnsSuccessWithValue()
    {
        Result<int, string>? result = null;

        return Scenario.Define("A successful operation preserves its value with a custom error type", _tryBoundaries)
            .When("The operation returns a value", () =>
            {
                result = Result.Try(() => 42, exception => exception.Message);
            })
            .Then("The result contains the returned value", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(42, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryOfTAndTError_ThrownException_InvokesErrorFactoryWithExactException()
    {
        var thrown = new InvalidOperationException("boom");
        Exception? observed = null;
        Result<int, string>? result = null;

        return Scenario.Define("A custom error factory receives the original operation exception", _tryBoundaries)
            .When("The operation fails and the error factory maps its exception", () =>
            {
                result = Result.Try<int, string>(
                    () => throw thrown,
                    exception =>
                    {
                        observed = exception;
                        return exception.Message;
                    });
            })
            .Then("The exact exception is mapped to its message", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Same(thrown, observed);
                Assert.Equal("boom", result?.Error);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryOfTAndTError_OperationCanceledException_Propagates()
    {
        OperationCanceledException cancellation = new();

        return Scenario.Define("Cancellation bypasses custom error mapping", _tryBoundaries)
            .WhenThrows<OperationCanceledException>(
                "The operation raises cancellation",
                () => _ = Result.Try<int, string>(
                    () => throw cancellation,
                    exception => exception.Message))
            .Then("The original cancellation exception is preserved instead of mapped", (OperationCanceledException exception) =>
                Assert.Same(cancellation, exception))
            .VerifyAsync();
    }

    [Fact]
    public Task TryOfTAndTError_NullOperation_ThrowsWithOperationParameter() =>
        Scenario.Define("A missing custom-error operation is rejected", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "Try receives no operation",
                () => _ = Result.Try<int, string>(null!, exception => exception.Message))
            .Then("The refusal identifies the missing operation", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryOfTAndTError_NullErrorFactory_ThrowsWithoutInvokingOperation()
    {
        var invoked = false;

        return Scenario.Define("A missing error factory is rejected before the operation runs", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "Try receives an operation without an error factory",
                () => _ = Result.Try<int, string>(
                    () =>
                    {
                        invoked = true;
                        return 1;
                    },
                    null!))
            .Then("The refusal identifies the error factory", (ArgumentNullException refusal) =>
                Assert.Equal("errorFactory", refusal.ParamName))
            .And("The operation is not run", (ArgumentNullException _) =>
                Assert.False(invoked))
            .VerifyAsync();
    }

    [Fact]
    public Task TryOfTAndTError_ErrorFactoryThrows_PropagatesException()
    {
        var factoryException = new InvalidOperationException("factory failed");

        return Scenario.Define("An error factory exception propagates unchanged", _tryBoundaries)
            .WhenThrows<InvalidOperationException>(
                "The error factory throws while handling an operation failure",
                () => _ = Result.Try<int, string>(
                    () => throw new InvalidOperationException("boom"),
                    _ => throw factoryException))
            .Then("The original error-factory exception is propagated", (InvalidOperationException exception) =>
                Assert.Same(factoryException, exception))
            .VerifyAsync();
    }

    [Fact]
    public Task TryOfTAndTError_ErrorFactoryReturnsNull_ThrowsArgumentNullException_NotWrappedAsFailure() =>
        Scenario.Define("A null custom error is rejected rather than wrapped", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "The error factory returns null for an operation failure",
                () => _ = Result.Try<int, string>(
                    () => throw new InvalidOperationException("boom"),
                    _ => null!))
            .Then("The refusal identifies the invalid error", (ArgumentNullException refusal) =>
                Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryOfTAndTError_NullReturnedValue_ThrowsArgumentNullException_NotConvertedToFailure() =>
        Scenario.Define("A null value is rejected before custom error conversion", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "The operation returns a null value",
                () => _ = Result.Try<string, string>(() => null!, exception => exception.Message))
            .Then("The refusal identifies the operation result", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryAsync_Success_ReturnsSuccess()
    {
        var invocations = 0;
        Result? result = null;

        return Scenario.Define("A successful asynchronous operation produces a successful result", _tryBoundaries)
            .When("The operation completes normally", async () =>
            {
                result = await Result.TryAsync(() =>
                {
                    invocations++;
                    return Task.CompletedTask;
                });
            })
            .Then("The operation is invoked once and succeeds", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(1, invocations);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsync_SynchronousThrow_ReturnsFailureWithFromException()
    {
        var thrown = new InvalidOperationException("boom");
        Result? result = null;

        return Scenario.Define("A synchronous throw from an async operation becomes a failure", _tryBoundaries)
            .When("The asynchronous operation throws before returning its task", async () =>
            {
                result = await Result.TryAsync(Task () => throw thrown);
            })
            .Then("The failure retains the original exception", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Same(thrown, result?.Error.Exception);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsync_FaultedTask_ReturnsFailureWithFromException()
    {
        var thrown = new InvalidOperationException("boom");
        Result? result = null;

        return Scenario.Define("A faulted asynchronous operation becomes a failure", _tryBoundaries)
            .When("The operation returns a faulted task", async () =>
            {
                result = await Result.TryAsync(() => Task.FromException(thrown));
            })
            .Then("The failure retains the original exception", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Same(thrown, result?.Error.Exception);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsync_SynchronousOperationCanceledException_Propagates()
    {
        OperationCanceledException cancellation = new();

        return Scenario.Define("A synchronous cancellation from an async operation propagates", _tryBoundaries)
            .WhenThrows<OperationCanceledException>(
                "The operation raises cancellation before returning its task",
                async () => await Result.TryAsync(Task () => throw cancellation))
            .Then("The original cancellation exception is preserved", (OperationCanceledException exception) =>
                Assert.Same(cancellation, exception))
            .VerifyAsync();
    }

    [Fact]
    public async Task TryAsync_CanceledTask_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Scenario.Define("A canceled task propagates cancellation", _tryBoundaries)
            .WhenThrowsAny<OperationCanceledException>(
                "The operation returns a task canceled with its token",
                async () => await Result.TryAsync(() => Task.FromCanceled(cancellation.Token)))
            .Then("The cancellation is associated with the caller token", (OperationCanceledException exception) => Assert.Equal(cancellation.Token, exception.CancellationToken))
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsync_NullOperation_ThrowsWithOperationParameter() =>
        Scenario.Define("A missing asynchronous operation is rejected", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "TryAsync receives no operation",
                async () => await Result.TryAsync(null!))
            .Then("The refusal identifies the missing operation", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryAsync_NullReturnedTask_ThrowsArgumentNullException_NotConvertedToFailure() =>
        Scenario.Define("A null task is rejected rather than captured as a failure", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "The asynchronous operation returns no task",
                async () => await Result.TryAsync(() => null!))
            .Then("The refusal identifies the operation", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryAsyncOfT_Success_ReturnsSuccessWithValue()
    {
        Result<int>? result = null;

        return Scenario.Define("A successful asynchronous value operation preserves its value", _tryBoundaries)
            .When("The operation returns a completed task with a value", async () =>
            {
                result = await Result.TryAsync(() => Task.FromResult(42));
            })
            .Then("The result contains the returned value", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(42, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsyncOfT_SynchronousThrow_ReturnsFailureWithFromException()
    {
        var thrown = new InvalidOperationException("boom");
        Result<int>? result = null;

        return Scenario.Define("A synchronous throw from an async value operation becomes a failure", _tryBoundaries)
            .When("The operation throws before returning its task", async () =>
            {
                result = await Result.TryAsync(Task<int> () => throw thrown);
            })
            .Then("The failure retains the original exception", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Same(thrown, result?.Error.Exception);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsyncOfT_FaultedTask_ReturnsFailureWithFromException()
    {
        var thrown = new InvalidOperationException("boom");
        Result<int>? result = null;

        return Scenario.Define("A faulted asynchronous value operation becomes a failure", _tryBoundaries)
            .When("The operation returns a faulted value task", async () =>
            {
                result = await Result.TryAsync(() => Task.FromException<int>(thrown));
            })
            .Then("The failure retains the original exception", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Same(thrown, result?.Error.Exception);
            })
            .VerifyAsync();
    }

    [Fact]
    public async Task TryAsyncOfT_CanceledTask_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Scenario.Define("A canceled value task propagates cancellation", _tryBoundaries)
            .WhenThrowsAny<OperationCanceledException>(
                "The operation returns a value task canceled with its token",
                async () => await Result.TryAsync(() => Task.FromCanceled<int>(cancellation.Token)))
            .Then("The cancellation is associated with the caller token", (OperationCanceledException exception) => Assert.Equal(cancellation.Token, exception.CancellationToken))
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsyncOfT_NullOperation_ThrowsWithOperationParameter() =>
        Scenario.Define("A missing asynchronous value operation is rejected", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "TryAsync receives no value operation",
                async () => await Result.TryAsync<int>(null!))
            .Then("The refusal identifies the missing operation", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryAsyncOfT_NullReturnedTask_ThrowsArgumentNullException_NotConvertedToFailure() =>
        Scenario.Define("A null value task is rejected rather than captured as a failure", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "The asynchronous value operation returns no task",
                async () => await Result.TryAsync<string>(() => null!))
            .Then("The refusal identifies the operation", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryAsyncOfT_NullReturnedValue_ThrowsArgumentNullException_NotConvertedToFailure() =>
        Scenario.Define("A null value from an asynchronous operation is rejected", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "The value task completes with null",
                async () => await Result.TryAsync(() => Task.FromResult<string>(null!)))
            .Then("The refusal identifies the operation result", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryAsyncOfT_Success_ExecutesOperationExactlyOnce()
    {
        var invocations = 0;
        Result<int>? result = null;

        return Scenario.Define("A successful asynchronous value operation executes exactly once", _tryBoundaries)
            .When("The operation returns its invocation count", async () =>
            {
                result = await Result.TryAsync(() =>
                {
                    invocations++;
                    return Task.FromResult(invocations);
                });
            })
            .Then("The successful result and invocation count are both one", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(1, invocations);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsyncOfTAndTError_Success_ReturnsSuccessWithValue()
    {
        Result<int, string>? result = null;

        return Scenario.Define("A successful asynchronous operation preserves its custom-error value", _tryBoundaries)
            .When("The operation returns a completed task with a value", async () =>
            {
                result = await Result.TryAsync(() => Task.FromResult(42), exception => exception.Message);
            })
            .Then("The result contains the returned value", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(42, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsyncOfTAndTError_ThrownException_InvokesErrorFactoryWithExactException()
    {
        var thrown = new InvalidOperationException("boom");
        Exception? observed = null;
        Result<int, string>? result = null;

        return Scenario.Define("The asynchronous error factory receives the original exception", _tryBoundaries)
            .When("The value task faults and the factory maps its exception", async () =>
            {
                result = await Result.TryAsync(
                    () => Task.FromException<int>(thrown),
                    exception =>
                    {
                        observed = exception;
                        return exception.Message;
                    });
            })
            .Then("The exact exception is mapped to its message", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Same(thrown, observed);
                Assert.Equal("boom", result?.Error);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsyncOfTAndTError_SynchronousOperationCanceledException_Propagates()
    {
        OperationCanceledException cancellation = new();

        return Scenario.Define("Synchronous cancellation bypasses asynchronous custom error mapping", _tryBoundaries)
            .WhenThrows<OperationCanceledException>(
                "The value operation raises cancellation before returning its task",
                async () => await Result.TryAsync(
                    Task<int> () => throw cancellation,
                    exception => exception.Message))
            .Then("The original cancellation exception is preserved instead of mapped", (OperationCanceledException exception) =>
                Assert.Same(cancellation, exception))
            .VerifyAsync();
    }

    [Fact]
    public async Task TryAsyncOfTAndTError_CanceledTask_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Scenario.Define("A canceled task bypasses custom error mapping", _tryBoundaries)
            .WhenThrowsAny<OperationCanceledException>(
                "The value task is canceled with its token",
                async () => await Result.TryAsync(
                    () => Task.FromCanceled<int>(cancellation.Token),
                    exception => exception.Message))
            .Then("The cancellation is associated with the caller token", (OperationCanceledException exception) => Assert.Equal(cancellation.Token, exception.CancellationToken))
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsyncOfTAndTError_NullOperation_ThrowsWithOperationParameter() =>
        Scenario.Define("A missing asynchronous custom-error operation is rejected", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "TryAsync receives no value operation",
                async () => await Result.TryAsync<int, string>(null!, exception => exception.Message))
            .Then("The refusal identifies the missing operation", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryAsyncOfTAndTError_NullErrorFactory_ThrowsWithoutInvokingOperation()
    {
        var invoked = false;

        return Scenario.Define("A missing asynchronous error factory is rejected before the operation runs", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "TryAsync receives an operation without an error factory",
                async () => await Result.TryAsync<int, string>(
                    () =>
                    {
                        invoked = true;
                        return Task.FromResult(1);
                    },
                    null!))
            .Then("The refusal identifies the error factory", (ArgumentNullException refusal) =>
                Assert.Equal("errorFactory", refusal.ParamName))
            .And("The operation is not run", (ArgumentNullException _) =>
                Assert.False(invoked))
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsyncOfTAndTError_ErrorFactoryThrows_PropagatesException()
    {
        var factoryException = new InvalidOperationException("factory failed");

        return Scenario.Define("An asynchronous error-factory exception propagates unchanged", _tryBoundaries)
            .WhenThrows<InvalidOperationException>(
                "The factory throws while mapping an asynchronous operation failure",
                async () => await Result.TryAsync<int, string>(
                    () => Task.FromException<int>(new InvalidOperationException("boom")),
                    _ => throw factoryException))
            .Then("The original error-factory exception is propagated", (InvalidOperationException exception) =>
                Assert.Same(factoryException, exception))
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsyncOfTAndTError_ErrorFactoryReturnsNull_ThrowsArgumentNullException_NotWrappedAsFailure() =>
        Scenario.Define("A null asynchronous custom error is rejected rather than wrapped", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "The error factory returns null for a faulted value task",
                async () => await Result.TryAsync<int, string>(
                    () => Task.FromException<int>(new InvalidOperationException("boom")),
                    _ => null!))
            .Then("The refusal identifies the invalid error", (ArgumentNullException refusal) =>
                Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryAsyncOfTAndTError_NullReturnedTask_ThrowsArgumentNullException_NotConvertedToFailure() =>
        Scenario.Define("A null task is rejected before custom error conversion", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "The asynchronous operation returns no task",
                async () => await Result.TryAsync<string, string>(() => null!, exception => exception.Message))
            .Then("The refusal identifies the operation", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryAsyncOfTAndTError_NullReturnedValue_ThrowsArgumentNullException_NotConvertedToFailure() =>
        Scenario.Define("A null asynchronous value is rejected before custom error conversion", _tryBoundaries)
            .WhenThrows<ArgumentNullException>(
                "The value task completes with null",
                async () => await Result.TryAsync(
                    () => Task.FromResult<string>(null!),
                    exception => exception.Message))
            .Then("The refusal identifies the operation result", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();
}
