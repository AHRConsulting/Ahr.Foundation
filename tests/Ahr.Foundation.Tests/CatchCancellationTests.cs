namespace Ahr.Foundation.Tests;

public sealed class CatchCancellationTests
{
    private static readonly Error _cancelled = Error.FromMessage("cancelled");

    private static readonly BusinessContext _cancellationHandling = BusinessContext
        .Define("cancellation-handling", "Cancellation handling", "Distinguish caller cancellation from other interruptions in an asynchronous workflow.")
        .Capability("handle-cancellation", "Convert only the caller's cancellation to a failure")
        .Outcome("caller-cancellation-represented", "A caller cancellation can be represented as the configured failure")
        .Rule("other-interruptions-propagate", "Timeouts and unrelated exceptions keep propagating")
        .Build();

    private static async Task<Result<int>> CancelledBy(CancellationTokenSource source)
    {
        await source.CancelAsync();
        source.Token.ThrowIfCancellationRequested();
        return Result<int>.Success(1);
    }

    [Fact]
    public async Task CallerCancellation_BecomesTheGivenFailure()
    {
        using CancellationTokenSource source = new();
        Result<int>? result = null;

        _ = await Scenario.Define("A caller cancellation becomes the configured failure", _cancellationHandling)
            .When("The caller cancels the operation and cancellation is caught", async () =>
            {
                result = await CancelledBy(source).CatchCancellationAsync(_cancelled, source.Token);
            })
            .Then("The result contains the configured cancellation error", () =>
                Assert.Equal(_cancelled.Message, result?.Error.Message))
            .VerifyAsync();
    }

    [Fact]
    public async Task CallerCancellation_AllForms_BecomeFailures()
    {
        using CancellationTokenSource source = new();
        Result? payloadFreeResult = null;
        Result? factoryResult = null;
        Result<int>? valueResult = null;
        Result<int, string>? customResult = null;
        Result<int, string>? customFactoryResult = null;

        _ = await Scenario.Define("Caller cancellation is caught for each supported result shape", _cancellationHandling)
            .Given("The caller's token is canceled", source.CancelAsync)
            .When("Cancelled tasks use the built-in and custom failure factories", async () =>
            {
                Task<Result> payloadFree = Task.FromCanceled<Result>(source.Token);
                Task<Result<int, string>> custom = Task.FromCanceled<Result<int, string>>(source.Token);

                payloadFreeResult = await payloadFree.CatchCancellationAsync(_cancelled, source.Token);
                factoryResult = await Task.FromCanceled<Result>(source.Token).CatchCancellationAsync(Error.FromException, source.Token);
                valueResult = await Task.FromCanceled<Result<int>>(source.Token).CatchCancellationAsync(Error.FromException, source.Token);
                customResult = await custom.CatchCancellationAsync("cancelled", source.Token);
                customFactoryResult = await Task.FromCanceled<Result<int, string>>(source.Token)
                    .CatchCancellationAsync(_ => "built", source.Token);
            })
            .Then("Payload-free cancellation uses the supplied error", () =>
                Assert.Equal(_cancelled.Message, payloadFreeResult?.Error.Message))
            .And("Factory overloads retain cancellation details", () =>
            {
                Assert.True(factoryResult?.Error.IsCancellation);
                Assert.True(valueResult?.Error.IsCancellation);
            })
            .And("Custom errors are supplied directly or by the factory", () =>
            {
                Assert.Equal("cancelled", customResult?.Error);
                Assert.Equal("built", customFactoryResult?.Error);
            })
            .VerifyAsync();
    }

    [Fact]
    public async Task OtherCancellation_Propagates()
    {
        using CancellationTokenSource caller = new();
        Task<Result<int>> timedOut = Task.FromException<Result<int>>(new TaskCanceledException("timeout"));

        _ = await Scenario.Define("A timeout is not converted into caller cancellation", _cancellationHandling)
            .WhenThrows<TaskCanceledException>("The timed-out operation is passed through cancellation handling", async () =>
                await timedOut.CatchCancellationAsync(_cancelled, caller.Token))
            .Then("The original timeout remains visible", (TaskCanceledException exception) =>
                Assert.Equal("timeout", exception.Message))
            .VerifyAsync();
    }

    [Fact]
    public async Task OtherException_Propagates()
    {
        using CancellationTokenSource source = new();
        await source.CancelAsync();
        Task<Result<int>> broken = Task.FromException<Result<int>>(new InvalidOperationException("bug"));

        _ = await Scenario.Define("Cancellation handling does not hide unrelated failures", _cancellationHandling)
            .WhenThrows<InvalidOperationException>("The broken operation is passed through cancellation handling", async () =>
                await broken.CatchCancellationAsync(_cancelled, source.Token))
            .Then("The original failure remains visible", (InvalidOperationException exception) =>
                Assert.Equal("bug", exception.Message))
            .VerifyAsync();
    }

    [Fact]
    public async Task SuccessAndFailure_PassThrough()
    {
        Result<int>? success = null;
        Result<int>? failure = null;
        using CancellationTokenSource source = new();

        _ = await Scenario.Define("Cancellation handling preserves completed result outcomes", _cancellationHandling)
            .When("A successful and failed task result pass through", async () =>
            {
                success = await Task.FromResult(Result<int>.Success(5))
                    .CatchCancellationAsync(_cancelled, source.Token);
                failure = await Task.FromResult(Result<int>.Failure(Error.FromMessage("failed")))
                    .CatchCancellationAsync(_cancelled, source.Token);
            })
            .Then("The success value is preserved", () => Assert.Equal(5, success?.Value))
            .And("The existing failure is preserved", () =>
                Assert.Equal("failed", failure?.Error.Message))
            .VerifyAsync();
    }

    [Fact]
    public async Task WithTryAsync_TimeoutFailsAndCallerCancellationBecomesGivenError()
    {
        using CancellationTokenSource source = new();
        Result<int>? timedOut = null;
        Result<int>? cancelled = null;

        _ = await Scenario.Define("Cancellation handling distinguishes a timeout from caller cancellation", _cancellationHandling)
            .When("Timeout and caller cancellation flow through cancellation handling", async () =>
            {
                timedOut = await Result.TryAsync(_ => Task.FromException<int>(new TaskCanceledException("timeout")), source.Token)
                    .CatchCancellationAsync(_cancelled, source.Token);
                cancelled = await Result.TryAsync(async token =>
                    {
                        await source.CancelAsync();
                        token.ThrowIfCancellationRequested();
                        return 1;
                    }, source.Token)
                    .CatchCancellationAsync(_cancelled, source.Token);
            })
            .Then("The timeout remains a cancellation failure", () => Assert.True(timedOut?.Error.IsCancellation))
            .And("Caller cancellation becomes the configured error", () =>
                Assert.Equal(_cancelled.Message, cancelled?.Error.Message))
            .VerifyAsync();
    }

    [Fact]
    public Task CatchCancellationAsync_NullResultTask_ThrowsWithResultTaskParameter()
    {
        Task<Result<int>> resultTask = null!;

        return Scenario.Define("Cancellation handling requires a result task", _cancellationHandling)
            .WhenThrows<ArgumentNullException>("No result task is provided", async () =>
                await resultTask.CatchCancellationAsync(_cancelled, CancellationToken.None))
            .Then("The refusal identifies the missing task", (ArgumentNullException refusal) =>
                Assert.Equal("resultTask", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task CatchCancellationAsync_DefaultError_ThrowsWithErrorParameter()
    {
        Task<Result<int>> resultTask = Task.FromResult(Result<int>.Success(1));

        return Scenario.Define("Cancellation handling requires a valid error", _cancellationHandling)
            .WhenThrows<ArgumentNullException>("A default error is supplied", async () =>
                await resultTask.CatchCancellationAsync(default(Error), CancellationToken.None))
            .Then("The refusal identifies the error parameter", (ArgumentNullException refusal) =>
                Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task CatchCancellationAsync_NullErrorFactory_ThrowsWithErrorFactoryParameter()
    {
        Task<Result<int>> resultTask = Task.FromResult(Result<int>.Success(1));

        return Scenario.Define("Cancellation handling requires an error factory", _cancellationHandling)
            .WhenThrows<ArgumentNullException>("No cancellation error factory is supplied", async () =>
                await resultTask.CatchCancellationAsync(null!, CancellationToken.None))
            .Then("The refusal identifies the factory parameter", (ArgumentNullException refusal) =>
                Assert.Equal("errorFactory", refusal.ParamName))
            .VerifyAsync();
    }
}
