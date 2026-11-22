namespace Ahr.Foundation.Tests;

public sealed class ResultTryFilterTests
{
    private static readonly BusinessContext _filteredOperations = BusinessContext
        .Define("filtered-operations", "Filtered operations", "Convert expected operation failures while allowing unrelated exceptions to propagate.")
        .Capability("filter-operation-failures", "Filter expected exceptions from synchronous and asynchronous operations")
        .Outcome("expected-failure-represented", "A matching exception becomes a result failure")
        .Rule("unmatched-failure-propagates", "Exceptions outside the filter are not hidden")
        .Build();

    private static bool IsIo(Exception exception) => exception is IOException;

    private static void ThrowIo() => throw new IOException("disk");

    private static void ThrowInvalid() => throw new InvalidOperationException("bug");

    private static int ReturnIo() => throw new IOException("disk");

    private static int ReturnInvalid() => throw new InvalidOperationException("bug");

    [Fact]
    public Task Try_MatchingException_ReturnsFailure()
    {
        Result? payloadFree = null;
        Result<int>? value = null;
        Result<int, string>? custom = null;

        return Scenario.Define("A matching I/O exception becomes a result failure", _filteredOperations)
            .When("Synchronous operations are run with an I/O exception filter", () =>
            {
                payloadFree = Result.Try(ThrowIo, IsIo);
                value = Result.Try(ReturnIo, IsIo);
                custom = Result.Try(ReturnIo, IsIo, exception => exception.Message);
            })
            .Then("The payload-free failure keeps the exception message", () =>
                Assert.Equal("disk", payloadFree?.Error.Message))
            .And("The value failure keeps the originating exception", () =>
                _ = Assert.IsType<IOException>(value?.Error.Exception))
            .And("The custom-error failure uses the exception message", () =>
                Assert.Equal("disk", custom?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task Try_OtherExceptionFromPayloadFreeOperation_Propagates() =>
        Scenario.Define("A non-matching exception propagates from a payload-free operation", _filteredOperations)
            .WhenThrows<InvalidOperationException>("The operation fails outside the I/O filter", () =>
            _ = Result.Try(ThrowInvalid, IsIo))
            .VerifyAsync();

    [Fact]
    public Task Try_OtherExceptionFromValueOperation_Propagates() =>
        Scenario.Define("A non-matching exception propagates from a value operation", _filteredOperations)
            .WhenThrows<InvalidOperationException>("The value operation fails outside the I/O filter", () =>
            _ = Result.Try(ReturnInvalid, IsIo))
            .VerifyAsync();

    [Fact]
    public Task Try_OtherExceptionFromCustomErrorOperation_Propagates() =>
        Scenario.Define("A non-matching exception propagates from a custom-error operation", _filteredOperations)
            .WhenThrows<InvalidOperationException>("The custom-error operation fails outside the I/O filter", () =>
            _ = Result.Try(ReturnInvalid, IsIo, exception => exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Try_Success_ReturnsValueWithoutCallingFilter()
    {
        var filterCalled = false;
        Result<int>? result = null;

        return Scenario.Define("A successful operation bypasses its exception filter", _filteredOperations)
            .When("The operation returns a value", () =>
            {
                result = Result.Try(() => 5, _ =>
                {
                    filterCalled = true;
                    return true;
                });
            })
            .Then("The value is returned successfully", () => Assert.Equal(5, result?.Value))
            .And("The exception filter is not called", () => Assert.False(filterCalled))
            .VerifyAsync();
    }

    [Fact]
    public Task Try_Cancellation_PropagatesWithoutCallingFilter()
    {
        var filterCalled = false;

        return Scenario.Define("Caller cancellation propagates through a filtered operation", _filteredOperations)
            .WhenThrows<OperationCanceledException>("The operation is canceled", () =>
            _ = Result.Try(() => throw new OperationCanceledException(), _ =>
                {
                    filterCalled = true;
                    return true;
                }))
            .Then("Cancellation is not passed to the exception filter", () => Assert.False(filterCalled))
            .VerifyAsync();
    }

    [Fact]
    public Task Try_FilterWithBoolFunction_BindsToFilterOverload()
    {
        Result<int>? result = null;

        return Scenario.Define("A boolean predicate filters an expected operation exception", _filteredOperations)
            .When("The I/O failure is run with a boolean filter", () =>
            {
                result = Result.Try(ReturnIo, exception => exception is IOException);
            })
            .Then("The matching failure is represented as a result failure", () =>
                Assert.True(result?.IsFailure))
            .VerifyAsync();
    }

    [Fact]
    public Task Try_NullFilter_ThrowsWithExactParameter() =>
        Scenario.Define("Filtered operations require a catch predicate", _filteredOperations)
            .WhenThrows<ArgumentNullException>("A synchronous operation is given no filter", () =>
            _ = Result.Try(ThrowIo, null!))
            .Then("The refusal identifies the filter parameter", (ArgumentNullException refusal) =>
                Assert.Equal("catchWhen", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryAsync_MatchingException_ReturnsFailure()
    {
        Result? payloadFree = null;
        Result<int>? value = null;
        Result<int, string>? custom = null;

        return Scenario.Define("A matching asynchronous I/O exception becomes a result failure", _filteredOperations)
            .When("Asynchronous operations are run with an I/O exception filter", async () =>
            {
                payloadFree = await Result.TryAsync(async () => { await Task.Yield(); ThrowIo(); }, IsIo);
                value = await Result.TryAsync(async () => { await Task.Yield(); return ReturnIo(); }, IsIo);
                custom = await Result.TryAsync(async () => { await Task.Yield(); return ReturnIo(); }, IsIo, exception => exception.Message);
            })
            .Then("The payload-free failure keeps the exception message", () =>
                Assert.Equal("disk", payloadFree?.Error.Message))
            .And("The value failure keeps the exception message", () =>
                Assert.Equal("disk", value?.Error.Message))
            .And("The custom-error failure uses the exception message", () =>
                Assert.Equal("disk", custom?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsync_ExceptionBeforeFirstAwait_IsFiltered()
    {
        Result? result = null;

        return Scenario.Define("An exception before the first await is filtered", _filteredOperations)
            .When("The asynchronous operation throws before returning its task", async () =>
            {
                result = await Result.TryAsync(() =>
                {
                    ThrowIo();
                    return Task.CompletedTask;
                }, IsIo);
            })
            .Then("The matching exception becomes a failure", () => Assert.True(result?.IsFailure))
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsync_OtherExceptionBeforeFirstAwait_Propagates() =>
        Scenario.Define("An unmatched exception before the first await propagates", _filteredOperations)
            .WhenThrows<InvalidOperationException>("The asynchronous operation throws outside the filter", async () =>
                await Result.TryAsync(() =>
                {
                    ThrowInvalid();
                    return Task.CompletedTask;
                }, IsIo))
            .VerifyAsync();

    [Fact]
    public Task TryAsync_OtherExceptionFromPayloadFreeOperation_Propagates() =>
        Scenario.Define("An unmatched asynchronous exception propagates from a payload-free operation", _filteredOperations)
            .WhenThrows<InvalidOperationException>("The operation fails outside the I/O filter", async () =>
                await Result.TryAsync(async () => { await Task.Yield(); ThrowInvalid(); }, IsIo))
            .VerifyAsync();

    [Fact]
    public Task TryAsync_OtherExceptionFromValueOperation_Propagates() =>
        Scenario.Define("An unmatched asynchronous exception propagates from a value operation", _filteredOperations)
            .WhenThrows<InvalidOperationException>("The value operation fails outside the I/O filter", async () =>
                await Result.TryAsync(async () => { await Task.Yield(); return ReturnInvalid(); }, IsIo))
            .VerifyAsync();

    [Fact]
    public Task TryAsync_OtherExceptionFromCustomErrorOperation_Propagates() =>
        Scenario.Define("An unmatched asynchronous exception propagates from a custom-error operation", _filteredOperations)
            .WhenThrows<InvalidOperationException>("The custom-error operation fails outside the I/O filter", async () =>
                await Result.TryAsync(async () => { await Task.Yield(); return ReturnInvalid(); }, IsIo, exception => exception.Message))
            .VerifyAsync();

    [Fact]
    public async Task TryAsync_Token_MatchingException_ReturnsFailure()
    {
        using CancellationTokenSource cancellation = new();
        Result? payloadFree = null;
        Result<int>? value = null;
        Result<int, string>? custom = null;

        _ = await Scenario.Define("A matching token-aware I/O exception becomes a result failure", _filteredOperations)
            .When("Token-aware asynchronous operations are run with an I/O filter", async () =>
            {
                payloadFree = await Result.TryAsync(async _ => { await Task.Yield(); ThrowIo(); }, IsIo, cancellation.Token);
                value = await Result.TryAsync(async _ => { await Task.Yield(); return ReturnIo(); }, IsIo, cancellation.Token);
                custom = await Result.TryAsync(
                    async _ => { await Task.Yield(); return ReturnIo(); },
                    IsIo,
                    exception => exception.Message,
                    cancellation.Token);
            })
            .Then("Each matching exception becomes a failure", () =>
            {
                Assert.True(payloadFree?.IsFailure);
                Assert.True(value?.IsFailure);
                Assert.Equal("disk", custom?.Error);
            })
            .VerifyAsync();
    }

    [Fact]
    public async Task TryAsync_Token_OtherException_Propagates()
    {
        using CancellationTokenSource cancellation = new();

        _ = await Scenario.Define("An unmatched token-aware exception propagates", _filteredOperations)
            .WhenThrows<InvalidOperationException>("The operation fails outside the I/O filter", async () =>
                await Result.TryAsync(async _ => { await Task.Yield(); return ReturnInvalid(); }, IsIo, cancellation.Token))
            .VerifyAsync();
    }

    [Fact]
    public async Task TryAsync_Token_Timeout_BecomesFailureWithoutCallingFilter()
    {
        using CancellationTokenSource cancellation = new();
        var filterCalled = false;
        Result<int>? value = null;
        Result<int, string>? custom = null;
        Result? payloadFree = null;

        _ = await Scenario.Define("A timeout becomes a cancellation failure without matching the filter", _filteredOperations)
            .When("Token-aware operations return timed-out tasks", async () =>
            {
                static Task<int> TimesOut(CancellationToken _) =>
                    Task.FromException<int>(new TaskCanceledException("timeout"));

                value = await Result.TryAsync(TimesOut, _ =>
                {
                    filterCalled = true;
                    return true;
                }, cancellation.Token);
                custom = await Result.TryAsync(TimesOut, IsIo, exception => exception.Message, cancellation.Token);
                payloadFree = await Result.TryAsync(_ => Task.FromException(new TaskCanceledException("timeout")), IsIo, cancellation.Token);
            })
            .Then("The timeout is represented as cancellation", () =>
            {
                Assert.True(value?.Error.IsCancellation);
                Assert.True(payloadFree?.Error.IsCancellation);
                Assert.Equal("timeout", custom?.Error);
            })
            .And("The exception filter is not called for a timeout", () => Assert.False(filterCalled))
            .VerifyAsync();
    }

    [Fact]
    public async Task TryAsync_Token_CallerCanceled_PropagatesWithoutCallingFilter()
    {
        using CancellationTokenSource cancellation = new();
        var filterCalled = false;

        _ = await Scenario.Define("Caller cancellation propagates from a token-aware operation", _filteredOperations)
            .WhenThrowsAny<OperationCanceledException>("The caller cancels the token-aware operation", async () =>
                _ = await Result.TryAsync(async token =>
                {
                    await cancellation.CancelAsync();
                    token.ThrowIfCancellationRequested();
                }, _ =>
                {
                    filterCalled = true;
                    return true;
                }, cancellation.Token))
            .Then("The cancellation filter is not called", () => Assert.False(filterCalled))
            .VerifyAsync();
    }

    [Fact]
    public Task Try_NullSuccess_ThrowsWithMessageNamingTheCause()
    {
        string? missing = null;

        return Scenario.Define("A synchronous operation cannot return a null result value", _filteredOperations)
            .WhenThrows<ArgumentNullException>("The operation returns no value", () => _ = Result.Try(() => missing))
            .Then("The refusal names the operation and its null result", (ArgumentNullException refusal) =>
            {
                Assert.StartsWith("The operation passed to Try returned null", refusal.Message, StringComparison.Ordinal);
                Assert.Equal("operation", refusal.ParamName);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Try_NullSuccessWithCustomError_ThrowsWithMessageNamingTheCause()
    {
        string? missing = null;

        return Scenario.Define("A custom-error operation cannot return a null result value", _filteredOperations)
            .WhenThrows<ArgumentNullException>("The custom-error operation returns no value", () =>
                _ = Result.Try(() => missing, exception => exception.Message))
            .Then("The refusal names the operation and its null result", (ArgumentNullException refusal) =>
            {
                Assert.StartsWith("The operation passed to Try returned null", refusal.Message, StringComparison.Ordinal);
                Assert.Equal("operation", refusal.ParamName);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsync_NullSuccess_ThrowsWithMessageNamingTheCause()
    {
        string? missing = null;

        return Scenario.Define("An asynchronous operation cannot return a null result value", _filteredOperations)
            .WhenThrows<ArgumentNullException>("The asynchronous operation returns no value", async () =>
                await Result.TryAsync(() => Task.FromResult(missing)))
            .Then("The refusal names the operation and its null result", (ArgumentNullException refusal) =>
            {
                Assert.StartsWith("The operation passed to TryAsync returned null", refusal.Message, StringComparison.Ordinal);
                Assert.Equal("operation", refusal.ParamName);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsync_NullSuccessWithCustomError_ThrowsWithMessageNamingTheCause()
    {
        string? missing = null;

        return Scenario.Define("An asynchronous custom-error operation cannot return a null result value", _filteredOperations)
            .WhenThrows<ArgumentNullException>("The asynchronous custom-error operation returns no value", async () =>
                await Result.TryAsync(() => Task.FromResult(missing), exception => exception.Message))
            .Then("The refusal names the operation and its null result", (ArgumentNullException refusal) =>
            {
                Assert.StartsWith("The operation passed to TryAsync returned null", refusal.Message, StringComparison.Ordinal);
                Assert.Equal("operation", refusal.ParamName);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsync_Token_NullSuccess_ThrowsWithMessageNamingTheCause()
    {
        string? missing = null;

        return Scenario.Define("A token-aware operation cannot return a null result value", _filteredOperations)
            .WhenThrows<ArgumentNullException>("The token-aware operation returns no value", async () =>
                await Result.TryAsync(_ => Task.FromResult(missing), CancellationToken.None))
            .Then("The refusal names the operation and its null result", (ArgumentNullException refusal) =>
            {
                Assert.StartsWith("The operation passed to TryAsync returned null", refusal.Message, StringComparison.Ordinal);
                Assert.Equal("operation", refusal.ParamName);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsync_Token_NullSuccessWithCustomError_ThrowsWithMessageNamingTheCause()
    {
        string? missing = null;

        return Scenario.Define("A token-aware custom-error operation cannot return a null result value", _filteredOperations)
            .WhenThrows<ArgumentNullException>("The token-aware custom-error operation returns no value", async () =>
                await Result.TryAsync(_ => Task.FromResult(missing), exception => exception.Message, CancellationToken.None))
            .Then("The refusal names the operation and its null result", (ArgumentNullException refusal) =>
            {
                Assert.StartsWith("The operation passed to TryAsync returned null", refusal.Message, StringComparison.Ordinal);
                Assert.Equal("operation", refusal.ParamName);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Try_NullableValueTypeWithValue_Succeeds()
    {
        Result<int?>? result = null;

        return Scenario.Define("A nullable value with a value becomes a successful result", _filteredOperations)
            .When("A present nullable value is processed", () =>
            {
                int? present = 3;
                result = Result.Try(() => present);
            })
            .Then("The value is available in the result", () => Assert.Equal(3, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task IsCancellation_ReflectsTheException()
    {
        IEnumerable<bool> expected = [true, true, false, false, false];
        bool[]? cancellationStates = null;

        return Scenario.Define("An error reports cancellation only for cancellation exceptions", _filteredOperations)
            .When("Errors are created from cancellation, I/O, and message-only failures", () =>
            {
                cancellationStates =
                [
                    Error.FromException(new OperationCanceledException()).IsCancellation,
                    Error.FromException(new TaskCanceledException()).IsCancellation,
                    Error.FromException(new IOException()).IsCancellation,
                    Error.FromMessage("failed").IsCancellation,
                    default(Error).IsCancellation
                ];
            })
            .Then("Only cancellation exceptions mark the error as cancellation", () =>
                Assert.Equal(expected, cancellationStates))
            .VerifyAsync();
    }
}
