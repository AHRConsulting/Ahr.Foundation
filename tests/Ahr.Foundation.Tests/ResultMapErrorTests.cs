namespace Ahr.Foundation.Tests;

public sealed class ResultMapErrorTests
{
    private static Error Wrap(Error error) => Error.FromMessage($"save failed: {error.Message}");

    [Fact]
    public void MapError_Failure_ReplacesError()
    {
        Result result = Result.Failure(Error.FromMessage("disk")).MapError(Wrap);

        Assert.Equal("save failed: disk", result.Error.Message);
    }

    [Fact]
    public void MapError_Success_ReturnsSuccessWithoutCallingMap()
    {
        var called = false;
        Result result = Result.Success().MapError(error =>
        {
            called = true;
            return error;
        });

        Assert.True(result.IsSuccess);
        Assert.False(called);
    }

    [Fact]
    public void MapError_DefaultErrorFromMap_Throws() =>
        Assert.Equal("error", Assert.Throws<ArgumentNullException>(() => Result.Failure(Error.FromMessage("disk")).MapError(_ => default)).ParamName);

    [Fact]
    public async Task MapErrorAsync_AllForms_ReplaceError()
    {
        var failed = Result.Failure(Error.FromMessage("disk"));

        Result direct = await failed.MapErrorAsync(error => Task.FromResult(Wrap(error)));
        Result fromTask = await Task.FromResult(failed).MapErrorAsync(error => Task.FromResult(Wrap(error)));
        Result fromTaskSync = await Task.FromResult(failed).MapErrorAsync(Wrap);
        Result passed = await Task.FromResult(Result.Success()).MapErrorAsync(Wrap);

        Assert.Equal("save failed: disk", direct.Error.Message);
        Assert.Equal("save failed: disk", fromTask.Error.Message);
        Assert.Equal("save failed: disk", fromTaskSync.Error.Message);
        Assert.True(passed.IsSuccess);
    }

    [Fact]
    public async Task MapError_NullArguments_ThrowWithExactParameter()
    {
        var failed = Result.Failure(Error.FromMessage("disk"));
        Task<Result> noTask = null!;

        Assert.Equal("map", Assert.Throws<ArgumentNullException>(() => failed.MapError(null!)).ParamName);
        Assert.Equal("map", (await Assert.ThrowsAsync<ArgumentNullException>(() => failed.MapErrorAsync(null!))).ParamName);
        Assert.Equal("map", (await Assert.ThrowsAsync<ArgumentNullException>(() => failed.MapErrorAsync(_ => null!))).ParamName);
        Assert.Equal("resultTask", (await Assert.ThrowsAsync<ArgumentNullException>(() => noTask.MapErrorAsync(Wrap))).ParamName);
    }

    [Fact]
    public void MapError_Uninitialized_Throws() =>
        _ = Assert.Throws<InvalidOperationException>(() => default(Result).MapError(Wrap));
}
