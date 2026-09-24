namespace Ahr.Foundation.Tests;

public sealed class AsyncCompositionTests
{
    [Fact]
    public async Task BindAsync_Success_ComposesValue()
    {
        Result<int> result = await Result<int>.Success(20).BindAsync(value => Task.FromResult(Result<int>.Success(value + 2)));
        Assert.Equal(22, result.Value);
    }

    [Fact]
    public async Task MapAsync_FaultedCallback_PropagatesOriginalException()
    {
        var expected = new InvalidOperationException("boom");
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() => Result<int>.Success(1).MapAsync(_ => Task.FromException<int>(expected)));
        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task BindAsync_CanceledCallback_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Result<int>.Success(1).BindAsync(_ => Task.FromCanceled<Result<int>>(cancellation.Token)));
    }

    [Fact]
    public async Task MapAsync_NullReturnedTask_ThrowsWithMapParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result<int>.Success(1).MapAsync<int>(_ => null!));
        Assert.Equal("map", exception.ParamName);
    }

    [Fact]
    public async Task TaskReceiver_NullResultTask_ThrowsWithResultTaskParameter()
    {
        Task<Result<int>> task = null!;
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => task.MapAsync(value => Task.FromResult(value + 1)));
        Assert.Equal("resultTask", exception.ParamName);
    }

    [Fact]
    public async Task TaskReceiver_FaultedResultTask_PropagatesOriginalException()
    {
        var expected = new InvalidOperationException("source");
        InvalidOperationException actual = await Assert.ThrowsAsync<InvalidOperationException>(() => Task.FromException<Result<int>>(expected).MapAsync(value => Task.FromResult(value + 1)));
        Assert.Same(expected, actual);
    }

    [Fact]
    public async Task TaskReceiver_CanceledOptionTask_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Task.FromCanceled<Option<int>>(cancellation.Token).MapAsync(value => Task.FromResult(value + 1)));
    }

    [Fact]
    public async Task ValueTaskAsTask_FrameworkAdaptation_ComposesSuccessfully()
    {
        static ValueTask<Result<int>> LoadAsync(int value) => ValueTask.FromResult(Result<int>.Success(value * 2));
        Result<int> result = await Result<int>.Success(21).BindAsync(value => LoadAsync(value).AsTask());
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task EnsureAsync_FalsePredicate_ReturnsFailure()
    {
        Result<int> result = await Result<int>.Success(1).EnsureAsync(_ => Task.FromResult(false), new Error("bad"));
        Assert.Equal(new Error("bad"), result.Error);
    }

    [Fact]
    public async Task OptionWhereAsync_None_DoesNotInvokePredicate()
    {
        var calls = 0;
        Option<int> result = await Option<int>.None.WhereAsync(_ => { calls++; return Task.FromResult(true); });
        Assert.Equal(0, calls);
        Assert.True(result.IsNone);
    }

    [Fact]
    public async Task MatchAsync_NullFailureDelegate_ThrowsWithOnFailureParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result<int>.Success(1).MatchAsync(value => Task.FromResult(value), null!));
        Assert.Equal("onFailure", exception.ParamName);
    }

    [Fact]
    public async Task DefaultBindAsync_UninitializedResult_ThrowsConsistentException()
    {
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => default(Result<int>).BindAsync(value => Task.FromResult(Result<int>.Success(value))));
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public async Task TaskReceiver_Result_BindAsync_Success_Chains()
    {
        Task<Result> task = Task.FromResult(Result.Success());
        Result result = await task.BindAsync(() => Task.FromResult(Result.Success()));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TaskReceiver_Result_BindAsync_ToResultOfT_Success_Chains()
    {
        Task<Result> task = Task.FromResult(Result.Success());
        Result<int> result = await task.BindAsync(() => Task.FromResult(Result<int>.Success(42)));
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task TaskReceiver_Result_TapAsync_Success_Executes()
    {
        var tapped = false;
        Task<Result> task = Task.FromResult(Result.Success());
        Result result = await task.TapAsync(() => { tapped = true; return Task.CompletedTask; });
        Assert.True(tapped);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TaskReceiver_Result_TapErrorAsync_Failure_Executes()
    {
        var tapped = false;
        Task<Result> task = Task.FromResult(Result.Failure(new Error("err")));
        Result result = await task.TapErrorAsync(e => { tapped = true; return Task.CompletedTask; });
        Assert.True(tapped);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskReceiver_Result_OrElseAsync_Success_ShortCircuits()
    {
        var called = false;
        Task<Result> task = Task.FromResult(Result.Success());
        Result result = await task.OrElseAsync(() => { called = true; return Task.FromResult(Result.Success()); });
        Assert.False(called);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TaskReceiver_Result_OrElseAsync_Failure_InvokesFallback()
    {
        Task<Result> task = Task.FromResult(Result.Failure(new Error("err")));
        Result result = await task.OrElseAsync(() => Task.FromResult(Result.Success()));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfT_BindAsync_ToResult_Success_Chains()
    {
        Task<Result<int>> task = Task.FromResult(Result<int>.Success(42));
        Result result = await task.BindAsync(v => Task.FromResult(Result.Success()));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfT_BindAsync_ToResultOfTOut_Success_Chains()
    {
        Task<Result<int>> task = Task.FromResult(Result<int>.Success(42));
        Result<string> result = await task.BindAsync(v => Task.FromResult(Result<string>.Success($"val: {v}")));
        Assert.True(result.IsSuccess);
        Assert.Equal("val: 42", result.Value);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfT_MapErrorAsync_TransformsError()
    {
        Task<Result<int>> task = Task.FromResult(Result<int>.Failure(new Error("original")));
        Result<int> result = await task.MapErrorAsync(e => Task.FromResult(new Error($"mapped: {e.Message}")));
        Assert.True(result.IsFailure);
        Assert.Equal("mapped: original", result.Error.Message);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfT_TapAsync_Executes()
    {
        var observed = 0;
        Task<Result<int>> task = Task.FromResult(Result<int>.Success(42));
        Result<int> result = await task.TapAsync(v => { observed = v; return Task.CompletedTask; });
        Assert.Equal(42, observed);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfT_TapErrorAsync_Executes()
    {
        var observed = "";
        Task<Result<int>> task = Task.FromResult(Result<int>.Failure(new Error("bad")));
        Result<int> result = await task.TapErrorAsync(e => { observed = e.Message; return Task.CompletedTask; });
        Assert.Equal("bad", observed);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfT_EnsureAsync_PredicateFalse_ReturnsFailure()
    {
        Task<Result<int>> task = Task.FromResult(Result<int>.Success(42));
        Result<int> result = await task.EnsureAsync(v => Task.FromResult(v < 10), new Error("too large"));
        Assert.True(result.IsFailure);
        Assert.Equal("too large", result.Error.Message);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfT_OrElseAsync_Success_ShortCircuits()
    {
        var called = false;
        Task<Result<int>> task = Task.FromResult(Result<int>.Success(42));
        Result<int> result = await task.OrElseAsync(() => { called = true; return Task.FromResult(Result<int>.Success(0)); });
        Assert.False(called);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfT_OrElseAsync_Failure_InvokesFallback()
    {
        Task<Result<int>> task = Task.FromResult(Result<int>.Failure(new Error("bad")));
        Result<int> result = await task.OrElseAsync(() => Task.FromResult(Result<int>.Success(99)));
        Assert.True(result.IsSuccess);
        Assert.Equal(99, result.Value);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfTAndTError_BindAsync_Chains()
    {
        Task<Result<int, string>> task = Task.FromResult(Result<int, string>.Success(42));
        Result<string, string> result = await task.BindAsync(v => Task.FromResult(Result<string, string>.Success($"ok: {v}")));
        Assert.True(result.IsSuccess);
        Assert.Equal("ok: 42", result.Value);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfTAndTError_MapAsync_TransformsValue()
    {
        Task<Result<int, string>> task = Task.FromResult(Result<int, string>.Success(42));
        Result<string, string> result = await task.MapAsync(v => Task.FromResult($"ok: {v}"));
        Assert.True(result.IsSuccess);
        Assert.Equal("ok: 42", result.Value);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfTAndTError_MapErrorAsync_TransformsError()
    {
        Task<Result<int, string>> task = Task.FromResult(Result<int, string>.Failure("original"));
        Result<int, int> result = await task.MapErrorAsync(e => Task.FromResult(e.Length));
        Assert.True(result.IsFailure);
        Assert.Equal(8, result.Error);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfTAndTError_TapAsync_Executes()
    {
        var observed = 0;
        Task<Result<int, string>> task = Task.FromResult(Result<int, string>.Success(42));
        Result<int, string> result = await task.TapAsync(v => { observed = v; return Task.CompletedTask; });
        Assert.Equal(42, observed);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfTAndTError_TapErrorAsync_Executes()
    {
        var observed = "";
        Task<Result<int, string>> task = Task.FromResult(Result<int, string>.Failure("err"));
        Result<int, string> result = await task.TapErrorAsync(e => { observed = e; return Task.CompletedTask; });
        Assert.Equal("err", observed);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfTAndTError_EnsureAsync_PredicateFalse_ReturnsFailure()
    {
        Task<Result<int, string>> task = Task.FromResult(Result<int, string>.Success(42));
        Result<int, string> result = await task.EnsureAsync(v => Task.FromResult(v < 10), "too large");
        Assert.True(result.IsFailure);
        Assert.Equal("too large", result.Error);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfTAndTError_OrElseAsync_Success_ShortCircuits()
    {
        var called = false;
        Task<Result<int, string>> task = Task.FromResult(Result<int, string>.Success(42));
        Result<int, string> result = await task.OrElseAsync(() => { called = true; return Task.FromResult(Result<int, string>.Success(0)); });
        Assert.False(called);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task TaskReceiver_ResultOfTAndTError_OrElseAsync_Failure_InvokesFallback()
    {
        Task<Result<int, string>> task = Task.FromResult(Result<int, string>.Failure("bad"));
        Result<int, string> result = await task.OrElseAsync(() => Task.FromResult(Result<int, string>.Success(99)));
        Assert.True(result.IsSuccess);
        Assert.Equal(99, result.Value);
    }

    [Fact]
    public async Task TaskReceiver_Option_BindAsync_Some_Chains()
    {
        Task<Option<int>> task = Task.FromResult(Option<int>.Some(42));
        Option<string> result = await task.BindAsync(v => Task.FromResult(Option<string>.Some($"val: {v}")));
        Assert.True(result.IsSome);
        Assert.Equal("val: 42", result.Value);
    }

    [Fact]
    public async Task TaskReceiver_Option_WhereAsync_PredicateTrue_KeepsSome()
    {
        Task<Option<int>> task = Task.FromResult(Option<int>.Some(42));
        Option<int> result = await task.WhereAsync(v => Task.FromResult(v > 10));
        Assert.True(result.IsSome);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task TaskReceiver_Option_TapAsync_Some_Executes()
    {
        var observed = 0;
        Task<Option<int>> task = Task.FromResult(Option<int>.Some(42));
        Option<int> result = await task.TapAsync(v => { observed = v; return Task.CompletedTask; });
        Assert.Equal(42, observed);
        Assert.True(result.IsSome);
    }

    [Fact]
    public async Task TaskReceiver_Option_OrElseAsync_Some_ShortCircuits()
    {
        var called = false;
        Task<Option<int>> task = Task.FromResult(Option<int>.Some(42));
        Option<int> result = await task.OrElseAsync(() => { called = true; return Task.FromResult(Option<int>.Some(0)); });
        Assert.False(called);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public async Task TaskReceiver_Option_OrElseAsync_None_InvokesFallback()
    {
        Task<Option<int>> task = Task.FromResult(Option<int>.None);
        Option<int> result = await task.OrElseAsync(() => Task.FromResult(Option<int>.Some(99)));
        Assert.True(result.IsSome);
        Assert.Equal(99, result.Value);
    }
}
