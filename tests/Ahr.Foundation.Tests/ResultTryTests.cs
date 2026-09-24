namespace Ahr.Foundation.Tests;

public sealed class ResultTryTests
{
    // -- Result.Try(Action) --

    [Fact]
    public void Try_Action_Success_ReturnsSuccess()
    {
        var invocations = 0;
        var result = Result.Try(() => { invocations++; });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, invocations);
    }

    [Fact]
    public void Try_Action_ThrownException_ReturnsFailureWithFromException()
    {
        var thrown = new InvalidOperationException("boom");
        var result = Result.Try(() => throw thrown);

        Assert.True(result.IsFailure);
        Assert.Same(thrown, result.Error.Exception);
        Assert.Equal("boom", result.Error.Message);
    }

    [Fact]
    public void Try_Action_OperationCanceledException_Propagates() => _ = Assert.Throws<OperationCanceledException>(() => Result.Try(() => throw new OperationCanceledException()));

    [Fact]
    public void Try_Action_TaskCanceledException_Propagates() => _ = Assert.Throws<TaskCanceledException>(() => Result.Try(() => throw new TaskCanceledException()));

    [Fact]
    public void Try_Action_NullOperation_ThrowsWithOperationParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result.Try(null!));
        Assert.Equal("operation", exception.ParamName);
    }

    // -- Result.Try<T>(Func<T>) --

    [Fact]
    public void TryOfT_Success_ReturnsSuccessWithValue()
    {
        var result = Result.Try(() => 42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void TryOfT_ThrownException_ReturnsFailureWithFromException()
    {
        var thrown = new InvalidOperationException("boom");
        var result = Result.Try<int>(() => throw thrown);

        Assert.True(result.IsFailure);
        Assert.Same(thrown, result.Error.Exception);
    }

    [Fact]
    public void TryOfT_OperationCanceledException_Propagates() => _ = Assert.Throws<OperationCanceledException>(() => Result.Try<int>(() => throw new OperationCanceledException()));

    [Fact]
    public void TryOfT_NullOperation_ThrowsWithOperationParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result.Try<int>(null!));
        Assert.Equal("operation", exception.ParamName);
    }

    [Fact]
    public void TryOfT_NullReturnedValue_ThrowsArgumentNullException_NotConvertedToFailure()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result.Try<string>(() => null!));
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void TryOfT_Success_ExecutesOperationExactlyOnce()
    {
        var invocations = 0;
        var result = Result.Try(() =>
        {
            invocations++;
            return invocations;
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, invocations);
    }

    // -- Result.Try<T, TError>(Func<T>, Func<Exception, TError>) --

    [Fact]
    public void TryOfTAndTError_Success_ReturnsSuccessWithValue()
    {
        var result = Result.Try(() => 42, ex => ex.Message);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void TryOfTAndTError_ThrownException_InvokesErrorFactoryWithExactException()
    {
        var thrown = new InvalidOperationException("boom");
        Exception? observed = null;
        var result = Result.Try<int, string>(
            () => throw thrown,
            ex =>
            {
                observed = ex;
                return ex.Message;
            });

        Assert.True(result.IsFailure);
        Assert.Same(thrown, observed);
        Assert.Equal("boom", result.Error);
    }

    [Fact]
    public void TryOfTAndTError_OperationCanceledException_Propagates()
    {
        _ = Assert.Throws<OperationCanceledException>(
            () => Result.Try<int, string>(() => throw new OperationCanceledException(), ex => ex.Message));
    }

    [Fact]
    public void TryOfTAndTError_NullOperation_ThrowsWithOperationParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => Result.Try<int, string>(null!, ex => ex.Message));
        Assert.Equal("operation", exception.ParamName);
    }

    [Fact]
    public void TryOfTAndTError_NullErrorFactory_ThrowsWithoutInvokingOperation()
    {
        var invoked = false;
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result.Try<int, string>(
            () =>
            {
                invoked = true;
                return 1;
            },
            null!));

        Assert.Equal("errorFactory", exception.ParamName);
        Assert.False(invoked);
    }

    [Fact]
    public void TryOfTAndTError_ErrorFactoryThrows_PropagatesException()
    {
        var factoryException = new InvalidOperationException("factory failed");
        InvalidOperationException observed = Assert.Throws<InvalidOperationException>(() => Result.Try<int, string>(
            () => throw new InvalidOperationException("boom"),
            _ => throw factoryException));

        Assert.Same(factoryException, observed);
    }

    [Fact]
    public void TryOfTAndTError_ErrorFactoryReturnsNull_ThrowsArgumentNullException_NotWrappedAsFailure()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result.Try<int, string>(
            () => throw new InvalidOperationException("boom"),
            _ => null!));

        Assert.Equal("error", exception.ParamName);
    }

    [Fact]
    public void TryOfTAndTError_NullReturnedValue_ThrowsArgumentNullException_NotConvertedToFailure()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => Result.Try<string, string>(() => null!, ex => ex.Message));
        Assert.Equal("value", exception.ParamName);
    }

    // -- Result.TryAsync(Func<Task>) --

    [Fact]
    public async Task TryAsync_Success_ReturnsSuccess()
    {
        var invocations = 0;
        Result result = await Result.TryAsync(() =>
        {
            invocations++;
            return Task.CompletedTask;
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, invocations);
    }

    [Fact]
    public async Task TryAsync_SynchronousThrow_ReturnsFailureWithFromException()
    {
        var thrown = new InvalidOperationException("boom");
        Result result = await Result.TryAsync(Task () => throw thrown);

        Assert.True(result.IsFailure);
        Assert.Same(thrown, result.Error.Exception);
    }

    [Fact]
    public async Task TryAsync_FaultedTask_ReturnsFailureWithFromException()
    {
        var thrown = new InvalidOperationException("boom");
        Result result = await Result.TryAsync(() => Task.FromException(thrown));

        Assert.True(result.IsFailure);
        Assert.Same(thrown, result.Error.Exception);
    }

    [Fact]
    public async Task TryAsync_SynchronousOperationCanceledException_Propagates()
    {
        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            () => Result.TryAsync(Task () => throw new OperationCanceledException()));
    }

    [Fact]
    public async Task TryAsync_CanceledTask_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Result.TryAsync(() => Task.FromCanceled(cancellation.Token)));
    }

    [Fact]
    public async Task TryAsync_NullOperation_ThrowsWithOperationParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.TryAsync(null!));
        Assert.Equal("operation", exception.ParamName);
    }

    [Fact]
    public async Task TryAsync_NullReturnedTask_ThrowsArgumentNullException_NotConvertedToFailure()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result.TryAsync(() => null!));
        Assert.Equal("operation", exception.ParamName);
    }

    // -- Result.TryAsync<T>(Func<Task<T>>) --

    [Fact]
    public async Task TryAsyncOfT_Success_ReturnsSuccessWithValue()
    {
        Result<int> result = await Result.TryAsync(() => Task.FromResult(42));

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task TryAsyncOfT_SynchronousThrow_ReturnsFailureWithFromException()
    {
        var thrown = new InvalidOperationException("boom");
        Result<int> result = await Result.TryAsync(Task<int> () => throw thrown);

        Assert.True(result.IsFailure);
        Assert.Same(thrown, result.Error.Exception);
    }

    [Fact]
    public async Task TryAsyncOfT_FaultedTask_ReturnsFailureWithFromException()
    {
        var thrown = new InvalidOperationException("boom");
        Result<int> result = await Result.TryAsync(() => Task.FromException<int>(thrown));

        Assert.True(result.IsFailure);
        Assert.Same(thrown, result.Error.Exception);
    }

    [Fact]
    public async Task TryAsyncOfT_CanceledTask_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Result.TryAsync(() => Task.FromCanceled<int>(cancellation.Token)));
    }

    [Fact]
    public async Task TryAsyncOfT_NullOperation_ThrowsWithOperationParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.TryAsync<int>(null!));
        Assert.Equal("operation", exception.ParamName);
    }

    [Fact]
    public async Task TryAsyncOfT_NullReturnedTask_ThrowsArgumentNullException_NotConvertedToFailure()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result.TryAsync<string>(() => null!));
        Assert.Equal("operation", exception.ParamName);
    }

    [Fact]
    public async Task TryAsyncOfT_NullReturnedValue_ThrowsArgumentNullException_NotConvertedToFailure()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result.TryAsync(() => Task.FromResult<string>(null!)));
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public async Task TryAsyncOfT_Success_ExecutesOperationExactlyOnce()
    {
        var invocations = 0;
        Result<int> result = await Result.TryAsync(() =>
        {
            invocations++;
            return Task.FromResult(invocations);
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(1, invocations);
    }

    // -- Result.TryAsync<T, TError>(Func<Task<T>>, Func<Exception, TError>) --

    [Fact]
    public async Task TryAsyncOfTAndTError_Success_ReturnsSuccessWithValue()
    {
        Result<int, string> result = await Result.TryAsync(() => Task.FromResult(42), ex => ex.Message);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task TryAsyncOfTAndTError_ThrownException_InvokesErrorFactoryWithExactException()
    {
        var thrown = new InvalidOperationException("boom");
        Exception? observed = null;
        Result<int, string> result = await Result.TryAsync(
            () => Task.FromException<int>(thrown),
            ex =>
            {
                observed = ex;
                return ex.Message;
            });

        Assert.True(result.IsFailure);
        Assert.Same(thrown, observed);
        Assert.Equal("boom", result.Error);
    }

    [Fact]
    public async Task TryAsyncOfTAndTError_SynchronousOperationCanceledException_Propagates()
    {
        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            () => Result.TryAsync(Task<int> () => throw new OperationCanceledException(), ex => ex.Message));
    }

    [Fact]
    public async Task TryAsyncOfTAndTError_CanceledTask_Propagates()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Result.TryAsync(() => Task.FromCanceled<int>(cancellation.Token), ex => ex.Message));
    }

    [Fact]
    public async Task TryAsyncOfTAndTError_NullOperation_ThrowsWithOperationParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result.TryAsync<int, string>(null!, ex => ex.Message));
        Assert.Equal("operation", exception.ParamName);
    }

    [Fact]
    public async Task TryAsyncOfTAndTError_NullErrorFactory_ThrowsWithoutInvokingOperation()
    {
        var invoked = false;
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.TryAsync<int, string>(
            () =>
            {
                invoked = true;
                return Task.FromResult(1);
            },
            null!));

        Assert.Equal("errorFactory", exception.ParamName);
        Assert.False(invoked);
    }

    [Fact]
    public async Task TryAsyncOfTAndTError_ErrorFactoryThrows_PropagatesException()
    {
        var factoryException = new InvalidOperationException("factory failed");
        InvalidOperationException observed = await Assert.ThrowsAsync<InvalidOperationException>(() => Result.TryAsync<int, string>(
            () => Task.FromException<int>(new InvalidOperationException("boom")),
            _ => throw factoryException));

        Assert.Same(factoryException, observed);
    }

    [Fact]
    public async Task TryAsyncOfTAndTError_ErrorFactoryReturnsNull_ThrowsArgumentNullException_NotWrappedAsFailure()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.TryAsync<int, string>(
            () => Task.FromException<int>(new InvalidOperationException("boom")),
            _ => null!));

        Assert.Equal("error", exception.ParamName);
    }

    [Fact]
    public async Task TryAsyncOfTAndTError_NullReturnedTask_ThrowsArgumentNullException_NotConvertedToFailure()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result.TryAsync<string, string>(() => null!, ex => ex.Message));
        Assert.Equal("operation", exception.ParamName);
    }

    [Fact]
    public async Task TryAsyncOfTAndTError_NullReturnedValue_ThrowsArgumentNullException_NotConvertedToFailure()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result.TryAsync(() => Task.FromResult<string>(null!), ex => ex.Message));
        Assert.Equal("value", exception.ParamName);
    }
}
