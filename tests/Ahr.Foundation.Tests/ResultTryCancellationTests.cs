namespace Ahr.Foundation.Tests;

public sealed class ResultTryCancellationTests
{
    private static readonly BusinessContext _cancellableTry = BusinessContext
        .Define("cancellation-aware-try", "Cancellation-aware exception boundaries", "Distinguish caller cancellation from cancellation raised by an operation.")
        .Capability("propagate-caller-cancellation", "Propagate cancellation requested by the caller")
        .Capability("capture-operation-cancellation", "Capture unrelated operation cancellation as a failure")
        .Outcome("caller-cancellation-propagates", "The caller's cancellation is never converted to a result failure")
        .Outcome("operation-cancellation-is-captured", "Unrelated cancellation is represented as a result failure")
        .Rule("preserve-cancellation-ownership", "Only cancellation from the supplied token propagates")
        .Build();

    [Fact]
    public async Task TryAsync_Token_Success_PassesTokenAndReturnsSuccess()
    {
        using CancellationTokenSource cancellation = new();
        CancellationToken observed = default;
        Result? result = null;

        _ = await Scenario.Define("TryAsync passes the caller token to a successful operation", _cancellableTry)
            .When("The asynchronous operation observes the token", async () =>
            {
                result = await Result.TryAsync(
                    token =>
                    {
                        observed = token;
                        return Task.CompletedTask;
                    },
                    cancellation.Token);
            })
            .Then("The operation succeeds and receives the caller token", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(cancellation.Token, observed);
            })
            .VerifyAsync();
    }

    [Fact]
    public async Task TryAsync_Token_CallerCanceled_Propagates()
    {
        using CancellationTokenSource cancellation = new();

        _ = await Scenario.Define("TryAsync propagates cancellation requested by the caller", _cancellableTry)
            .WhenThrowsAny<OperationCanceledException>("The supplied token is canceled during the operation", async () =>
                await Result.TryAsync(
                    async token =>
                    {
                        await cancellation.CancelAsync();
                        token.ThrowIfCancellationRequested();
                    },
                    cancellation.Token))
            .Then("The cancellation is associated with the caller token", (OperationCanceledException exception) =>
                Assert.Equal(cancellation.Token, exception.CancellationToken))
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsync_Token_UnrelatedCancellation_ReturnsFailure()
    {
        TaskCanceledException timeout = new("The request timed out.");
        Result? result = null;

        return Scenario.Define("TryAsync converts an unrelated cancellation into a failure", _cancellableTry)
            .When("The operation is canceled without the caller token", async () =>
            {
                result = await Result.TryAsync(_ => Task.FromException(timeout), CancellationToken.None);
            })
            .Then("The timeout is preserved as the failure cause", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Same(timeout, result?.Error.Exception);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsync_Token_SynchronousUnrelatedCancellation_ReturnsFailure()
    {
        Result? result = null;

        OperationCanceledException unrelated = new();

        return Scenario.Define("TryAsync captures synchronous unrelated cancellation", _cancellableTry)
            .When("The operation throws before returning a task", async () =>
            {
                result = await Result.TryAsync(
                    Task (_) => throw unrelated,
                    CancellationToken.None);
            })
            .Then("The cancellation is represented as a failure", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Same(unrelated, result?.Error.Exception);
            })
            .VerifyAsync();
    }

    [Fact]
    public async Task TryAsync_Token_PreCanceled_ThrowsWithoutInvokingOperation()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        var invoked = false;

        _ = await Scenario.Define("TryAsync does not start an operation with a pre-canceled token", _cancellableTry)
            .WhenThrowsAny<OperationCanceledException>("The operation is called with an already canceled token", async () =>
                await Result.TryAsync(
                    _ =>
                    {
                        invoked = true;
                        return Task.CompletedTask;
                    },
                    cancellation.Token))
            .Then("The operation is not invoked", (OperationCanceledException _) =>
                Assert.False(invoked))
            .And("The cancellation is associated with the caller token", (OperationCanceledException exception) =>
                Assert.Equal(cancellation.Token, exception.CancellationToken))
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsync_Token_NullOperation_Throws() =>
        Scenario.Define("TryAsync rejects a null operation", _cancellableTry)
            .WhenThrows<ArgumentNullException>("A null operation is supplied", async () =>
                await Result.TryAsync(null!, CancellationToken.None))
            .Then("The missing operation is identified", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryAsync_Token_NullTask_Throws() =>
        Scenario.Define("TryAsync rejects an operation that returns a null task", _cancellableTry)
            .WhenThrows<ArgumentNullException>("The operation returns null instead of a task", async () =>
                await Result.TryAsync(_ => null!, CancellationToken.None))
            .Then("The invalid operation result is identified", (ArgumentNullException refusal) =>
                Assert.Equal("operation", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TryAsyncOfT_Token_Success_ReturnsValue()
    {
        Result<int>? result = null;

        return Scenario.Define("Generic TryAsync returns a successful value", _cancellableTry)
            .When("The asynchronous operation returns a value", async () =>
            {
                result = await Result.TryAsync(_ => Task.FromResult(42), CancellationToken.None);
            })
            .Then("The result contains the returned value", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(42, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsyncOfT_Token_UnrelatedCancellation_ReturnsFailure()
    {
        TaskCanceledException timeout = new("The request timed out.");
        Result<int>? result = null;

        return Scenario.Define("Generic TryAsync converts unrelated cancellation into a failure", _cancellableTry)
            .When("The operation is canceled without the caller token", async () =>
            {
                result = await Result.TryAsync(_ => Task.FromException<int>(timeout), CancellationToken.None);
            })
            .Then("The timeout is preserved as the failure cause", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Same(timeout, result?.Error.Exception);
            })
            .VerifyAsync();
    }

    [Fact]
    public async Task TryAsyncOfT_Token_CallerCanceled_Propagates()
    {
        using CancellationTokenSource cancellation = new();

        _ = await Scenario.Define("Generic TryAsync propagates cancellation requested by the caller", _cancellableTry)
            .WhenThrowsAny<OperationCanceledException>("The supplied token is canceled during the operation", async () =>
                await Result.TryAsync(
                    async token =>
                    {
                        await cancellation.CancelAsync();
                        token.ThrowIfCancellationRequested();
                        return 1;
                    },
                    cancellation.Token))
            .Then("The cancellation is associated with the caller token", (OperationCanceledException exception) =>
                Assert.Equal(cancellation.Token, exception.CancellationToken))
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsyncOfT_Token_OtherException_ReturnsFailure()
    {
        InvalidOperationException thrown = new("boom");
        Result<int>? result = null;

        return Scenario.Define("Generic TryAsync captures an unrelated exception", _cancellableTry)
            .When("The asynchronous operation throws", async () =>
            {
                result = await Result.TryAsync(_ => Task.FromException<int>(thrown), CancellationToken.None);
            })
            .Then("The thrown exception is retained in the failure", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Same(thrown, result?.Error.Exception);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsyncOfTAndTError_Token_UnrelatedCancellation_UsesErrorFactory()
    {
        Result<int, string>? result = null;

        return Scenario.Define("Custom-error TryAsync maps unrelated cancellation through its error factory", _cancellableTry)
            .When("The operation is canceled without the caller token", async () =>
            {
                result = await Result.TryAsync(
                    _ => Task.FromException<int>(new TaskCanceledException("timeout")),
                    exception => exception.Message,
                    CancellationToken.None);
            })
            .Then("The error factory supplies the failure", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Equal("timeout", result?.Error);
            })
            .VerifyAsync();
    }

    [Fact]
    public async Task TryAsyncOfTAndTError_Token_CallerCanceled_Propagates()
    {
        using CancellationTokenSource cancellation = new();

        _ = await Scenario.Define("Custom-error TryAsync propagates caller cancellation", _cancellableTry)
            .WhenThrowsAny<OperationCanceledException>("The supplied token is canceled during the operation", async () =>
                await Result.TryAsync(
                    async token =>
                    {
                        await cancellation.CancelAsync();
                        token.ThrowIfCancellationRequested();
                        return 1;
                    },
                    exception => exception.Message,
                    cancellation.Token))
            .Then("The cancellation is associated with the caller token", (OperationCanceledException exception) =>
                Assert.Equal(cancellation.Token, exception.CancellationToken))
            .VerifyAsync();
    }

    [Fact]
    public Task TryAsyncOfTAndTError_Token_NullErrorFactory_Throws() =>
        Scenario.Define("Custom-error TryAsync rejects a null error factory", _cancellableTry)
            .WhenThrows<ArgumentNullException>("A null error factory is supplied", async () =>
                await Result.TryAsync<int, string>(
                    _ => Task.FromResult(1),
                    null!,
                    CancellationToken.None))
            .Then("The missing error factory is identified", (ArgumentNullException refusal) =>
                Assert.Equal("errorFactory", refusal.ParamName))
            .VerifyAsync();
}
