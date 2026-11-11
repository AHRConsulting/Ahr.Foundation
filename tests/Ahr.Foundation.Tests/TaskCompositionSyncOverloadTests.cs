namespace Ahr.Foundation.Tests;

public sealed class TaskCompositionSyncOverloadTests
{
    private static readonly Error _failure = Error.FromMessage("failed");

    private static Task<Result<int>> SuccessAsync(int value) => Task.FromResult(Result<int>.Success(value));
    private static Task<Result<int>> FailureAsync() => Task.FromResult(Result<int>.Failure(_failure));

    [Fact]
    public async Task ResultOfT_MixedSyncAndAsyncPipeline_ComposesWithoutIntermediateAwait()
    {
        var text = await SuccessAsync(20)
            .MapAsync(value => value + 1)
            .BindAsync(value => Result<int>.Success(value * 2))
            .EnsureAsync(value => value > 0, _failure)
            .MapAsync(value => Task.FromResult(value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .MatchAsync(value => value, error => error.Message);

        Assert.Equal("42", text);
    }

    [Fact]
    public async Task ResultOfT_AsyncLambda_StillBindsToAsyncOverload()
    {
        // The explicit type makes this test fail to compile if the synchronous overload is chosen.
        Result<int> result = await SuccessAsync(1).MapAsync(value => Task.FromResult(value + 1));
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public async Task ResultOfT_Failure_SkipsSyncDelegates()
    {
        var invoked = false;
        Result<int> result = await FailureAsync()
            .MapAsync(value => { invoked = true; return value; })
            .BindAsync(value => { invoked = true; return Result<int>.Success(value); })
            .EnsureAsync(_ => { invoked = true; return true; }, _failure)
            .TapAsync(_ => invoked = true);

        Assert.False(invoked);
        Assert.Equal(_failure, result.Error);
    }

    [Fact]
    public async Task ResultOfT_TapErrorAndMapError_RunOnFailure()
    {
        Error? observed = null;
        Result<int> result = await FailureAsync()
            .TapErrorAsync(error => observed = error)
            .MapErrorAsync(error => Error.FromMessage($"wrapped: {error.Message}"));

        Assert.Equal(_failure, observed);
        Assert.Equal("wrapped: failed", result.Error.Message);
    }

    [Fact]
    public async Task ResultOfT_BindToPayloadFree_Composes()
    {
        Result result = await SuccessAsync(1).BindAsync(_ => Result.Success());
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ResultOfT_MatchAsync_AsyncBranches_Composes()
    {
        var text = await FailureAsync().MatchAsync(_ => Task.FromResult("ok"), error => Task.FromResult(error.Message));
        Assert.Equal("failed", text);
    }

    [Fact]
    public async Task ResultOfT_NullSyncDelegate_ThrowsWithParameterName()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => SuccessAsync(1).MapAsync((Func<int, int>)null!));
        Assert.Equal("map", exception.ParamName);
    }

    [Fact]
    public async Task ResultOfT_NullTask_ThrowsWithResultTaskParameter()
    {
        Task<Result<int>> task = null!;
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => task.MapAsync(value => value + 1));
        Assert.Equal("resultTask", exception.ParamName);
    }

    [Fact]
    public async Task Result_SyncOverloads_Compose()
    {
        var tapped = false;
        Result<int> result = await Task.FromResult(Result.Success())
            .TapAsync(() => tapped = true)
            .BindAsync(Result.Success)
            .BindAsync(() => Result<int>.Success(7));

        Assert.True(tapped);
        Assert.Equal(7, result.Value);
    }

    [Fact]
    public async Task Result_Failure_TapErrorAndMatch()
    {
        Error? observed = null;
        var text = await Task.FromResult(Result.Failure(_failure))
            .TapErrorAsync(error => observed = error)
            .MatchAsync(() => "ok", error => error.Message);

        Assert.Equal(_failure, observed);
        Assert.Equal("failed", text);
    }

    [Fact]
    public async Task Result_MatchAsync_AsyncBranches_Composes()
    {
        var text = await Task.FromResult(Result.Success()).MatchAsync(() => Task.FromResult("ok"), error => Task.FromResult(error.Message));
        Assert.Equal("ok", text);
    }

    [Fact]
    public async Task CustomErrorResult_SyncOverloads_Compose()
    {
        var tapped = 0;
        Result<string, int> result = await Task.FromResult(Result<int, int>.Success(5))
            .MapAsync(value => value * 2)
            .BindAsync(value => Result<int, int>.Success(value + 1))
            .EnsureAsync(value => value > 0, 400)
            .TapAsync(value => tapped = value)
            .MapAsync(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(11, tapped);
        Assert.Equal("11", result.Value);
    }

    [Fact]
    public async Task CustomErrorResult_Failure_MapErrorTapErrorAndMatch()
    {
        var observed = 0;
        var code = await Task.FromResult(Result<int, int>.Failure(404))
            .TapErrorAsync(error => observed = error)
            .MapErrorAsync(error => error + 1)
            .MatchAsync(_ => 0, error => error);

        Assert.Equal(404, observed);
        Assert.Equal(405, code);
    }

    [Fact]
    public async Task CustomErrorResult_MatchAsync_AsyncBranches_Composes()
    {
        var code = await Task.FromResult(Result<int, int>.Failure(500)).MatchAsync(_ => Task.FromResult(0), Task.FromResult);
        Assert.Equal(500, code);
    }

    [Fact]
    public async Task Option_SyncOverloads_Compose()
    {
        var tapped = 0;
        var text = await Task.FromResult(Option<int>.Some(4))
            .MapAsync(value => value + 1)
            .BindAsync(value => Option<int>.Some(value * 2))
            .WhereAsync(value => value > 5)
            .TapAsync(value => tapped = value)
            .MatchAsync(value => $"some {value}", () => "none");

        Assert.Equal(10, tapped);
        Assert.Equal("some 10", text);
    }

    [Fact]
    public async Task Option_None_SkipsSyncDelegatesAndMatchesNone()
    {
        var invoked = false;
        var text = await Task.FromResult(Option<int>.None)
            .MapAsync(value => { invoked = true; return value; })
            .WhereAsync(_ => { invoked = true; return true; })
            .MatchAsync(_ => Task.FromResult("some"), () => Task.FromResult("none"));

        Assert.False(invoked);
        Assert.Equal("none", text);
    }
}
