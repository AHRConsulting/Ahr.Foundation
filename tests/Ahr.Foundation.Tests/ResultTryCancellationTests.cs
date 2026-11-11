namespace Ahr.Foundation.Tests;

public sealed class ResultTryCancellationTests
{
    // -- Result.TryAsync(Func<CancellationToken, Task>, CancellationToken) --

    [Fact]
    public async Task TryAsync_Token_Success_PassesTokenAndReturnsSuccess()
    {
        using var cancellation = new CancellationTokenSource();
        CancellationToken observed = default;

        Result result = await Result.TryAsync(token => { observed = token; return Task.CompletedTask; }, cancellation.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(cancellation.Token, observed);
    }

    [Fact]
    public async Task TryAsync_Token_CallerCanceled_Propagates()
    {
        using var cancellation = new CancellationTokenSource();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Result.TryAsync(
            async token => { await cancellation.CancelAsync(); token.ThrowIfCancellationRequested(); },
            cancellation.Token));
    }

    [Fact]
    public async Task TryAsync_Token_UnrelatedCancellation_ReturnsFailure()
    {
        var timeout = new TaskCanceledException("The request timed out.");

        Result result = await Result.TryAsync(_ => Task.FromException(timeout), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Same(timeout, result.Error.Exception);
    }

    [Fact]
    public async Task TryAsync_Token_SynchronousUnrelatedCancellation_ReturnsFailure()
    {
        Result result = await Result.TryAsync(Task (_) => throw new OperationCanceledException(), CancellationToken.None);

        Assert.True(result.IsFailure);
        _ = Assert.IsType<OperationCanceledException>(result.Error.Exception);
    }

    [Fact]
    public async Task TryAsync_Token_PreCanceled_ThrowsWithoutInvokingOperation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var invoked = false;

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Result.TryAsync(
            _ => { invoked = true; return Task.CompletedTask; },
            cancellation.Token));
        Assert.False(invoked);
    }

    [Fact]
    public async Task TryAsync_Token_NullOperation_Throws()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result.TryAsync(null!, CancellationToken.None));
        Assert.Equal("operation", exception.ParamName);
    }

    [Fact]
    public async Task TryAsync_Token_NullTask_Throws()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result.TryAsync(_ => null!, CancellationToken.None));
        Assert.Equal("operation", exception.ParamName);
    }

    // -- Result.TryAsync<T>(Func<CancellationToken, Task<T>>, CancellationToken) --

    [Fact]
    public async Task TryAsyncOfT_Token_Success_ReturnsValue()
    {
        Result<int> result = await Result.TryAsync(_ => Task.FromResult(42), CancellationToken.None);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task TryAsyncOfT_Token_UnrelatedCancellation_ReturnsFailure()
    {
        var timeout = new TaskCanceledException("The request timed out.");

        Result<int> result = await Result.TryAsync(_ => Task.FromException<int>(timeout), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Same(timeout, result.Error.Exception);
    }

    [Fact]
    public async Task TryAsyncOfT_Token_CallerCanceled_Propagates()
    {
        using var cancellation = new CancellationTokenSource();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Result.TryAsync(
            async token => { await cancellation.CancelAsync(); token.ThrowIfCancellationRequested(); return 1; },
            cancellation.Token));
    }

    [Fact]
    public async Task TryAsyncOfT_Token_OtherException_ReturnsFailure()
    {
        var thrown = new InvalidOperationException("boom");

        Result<int> result = await Result.TryAsync(_ => Task.FromException<int>(thrown), CancellationToken.None);

        Assert.Same(thrown, result.Error.Exception);
    }

    // -- Result.TryAsync<T, TError>(Func<CancellationToken, Task<T>>, Func<Exception, TError>, CancellationToken) --

    [Fact]
    public async Task TryAsyncOfTAndTError_Token_UnrelatedCancellation_UsesErrorFactory()
    {
        Result<int, string> result = await Result.TryAsync(
            _ => Task.FromException<int>(new TaskCanceledException("timeout")),
            exception => exception.Message,
            CancellationToken.None);

        Assert.Equal("timeout", result.Error);
    }

    [Fact]
    public async Task TryAsyncOfTAndTError_Token_CallerCanceled_Propagates()
    {
        using var cancellation = new CancellationTokenSource();

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Result.TryAsync(
            async token => { await cancellation.CancelAsync(); token.ThrowIfCancellationRequested(); return 1; },
            exception => exception.Message,
            cancellation.Token));
    }

    [Fact]
    public async Task TryAsyncOfTAndTError_Token_NullErrorFactory_Throws()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result.TryAsync(_ => Task.FromResult(1), (Func<Exception, string>)null!, CancellationToken.None));
        Assert.Equal("errorFactory", exception.ParamName);
    }
}
