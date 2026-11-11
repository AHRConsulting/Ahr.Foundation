// Explicit result types check which overload each call binds to.
#pragma warning disable IDE0007

namespace Ahr.Foundation.Tests;

public sealed class CatchCancellationTests
{
    private static readonly Error _cancelled = Error.FromMessage("cancelled");

    private static async Task<Result<int>> CancelledBy(CancellationTokenSource source)
    {
        await source.CancelAsync();
        source.Token.ThrowIfCancellationRequested();
        return Result<int>.Success(1);
    }

    [Fact]
    public async Task CallerCancellation_BecomesTheGivenFailure()
    {
        using var source = new CancellationTokenSource();

        Result<int> result = await CancelledBy(source).CatchCancellationAsync(_cancelled, source.Token);

        Assert.Equal("cancelled", result.Error.Message);
    }

    [Fact]
    public async Task CallerCancellation_AllForms_BecomeFailures()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        Task<Result> payloadFree = Task.FromCanceled<Result>(source.Token);
        Task<Result<int, string>> custom = Task.FromCanceled<Result<int, string>>(source.Token);

        Result payloadFreeResult = await payloadFree.CatchCancellationAsync(_cancelled, source.Token);
        Result factoryResult = await Task.FromCanceled<Result>(source.Token).CatchCancellationAsync(Error.FromException, source.Token);
        Result<int> valueResult = await Task.FromCanceled<Result<int>>(source.Token).CatchCancellationAsync(Error.FromException, source.Token);
        Result<int, string> customResult = await custom.CatchCancellationAsync("cancelled", source.Token);
        Result<int, string> customFactoryResult = await Task.FromCanceled<Result<int, string>>(source.Token).CatchCancellationAsync(_ => "built", source.Token);

        Assert.Equal("cancelled", payloadFreeResult.Error.Message);
        Assert.True(factoryResult.Error.IsCancellation);
        Assert.True(valueResult.Error.IsCancellation);
        Assert.Equal("cancelled", customResult.Error);
        Assert.Equal("built", customFactoryResult.Error);
    }

    [Fact]
    public async Task OtherCancellation_Propagates()
    {
        using var caller = new CancellationTokenSource();
        Task<Result<int>> timedOut = Task.FromException<Result<int>>(new TaskCanceledException("timeout"));

        _ = await Assert.ThrowsAsync<TaskCanceledException>(() => timedOut.CatchCancellationAsync(_cancelled, caller.Token));
    }

    [Fact]
    public async Task OtherException_Propagates()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        Task<Result<int>> broken = Task.FromException<Result<int>>(new InvalidOperationException("bug"));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => broken.CatchCancellationAsync(_cancelled, source.Token));
    }

    [Fact]
    public async Task SuccessAndFailure_PassThrough()
    {
        using var source = new CancellationTokenSource();

        Result<int> success = await Task.FromResult(Result<int>.Success(5)).CatchCancellationAsync(_cancelled, source.Token);
        Result<int> failure = await Task.FromResult(Result<int>.Failure(Error.FromMessage("failed"))).CatchCancellationAsync(_cancelled, source.Token);

        Assert.Equal(5, success.Value);
        Assert.Equal("failed", failure.Error.Message);
    }

    [Fact]
    public async Task WithTryAsync_TimeoutFailsAndCallerCancellationBecomesGivenError()
    {
        using var source = new CancellationTokenSource();

        Result<int> timedOut = await Result.TryAsync(_ => Task.FromException<int>(new TaskCanceledException("timeout")), source.Token)
            .CatchCancellationAsync(_cancelled, source.Token);
        Result<int> cancelled = await Result.TryAsync(async token => { await source.CancelAsync(); token.ThrowIfCancellationRequested(); return 1; }, source.Token)
            .CatchCancellationAsync(_cancelled, source.Token);

        Assert.True(timedOut.Error.IsCancellation);
        Assert.Equal("cancelled", cancelled.Error.Message);
    }

    [Fact]
    public async Task InvalidArguments_Throw()
    {
        Task<Result<int>> nullTask = null!;

        Assert.Equal("resultTask", (await Assert.ThrowsAsync<ArgumentNullException>(() => nullTask.CatchCancellationAsync(_cancelled, CancellationToken.None))).ParamName);
        Assert.Equal("error", (await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(Result<int>.Success(1)).CatchCancellationAsync(default(Error), CancellationToken.None))).ParamName);
        Assert.Equal("errorFactory", (await Assert.ThrowsAsync<ArgumentNullException>(() => Task.FromResult(Result<int>.Success(1)).CatchCancellationAsync(null!, CancellationToken.None))).ParamName);
    }
}
