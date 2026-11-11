namespace Ahr.Foundation.Tests;

public sealed class EnsureMessageTests
{
    private const string Message = "The value must be positive.";

    [Fact]
    public void Ensure_PredicatePasses_KeepsValue() => Assert.Equal(1, Result<int>.Success(1).Ensure(value => value > 0, Message).Value);

    [Fact]
    public void Ensure_PredicateFails_UsesMessage() =>
        Assert.Equal(Message, Result<int>.Success(-1).Ensure(value => value > 0, Message).Error.Message);

    [Fact]
    public void Ensure_AlreadyFailed_KeepsOriginalError()
    {
        var original = Error.FromMessage("original");
        Assert.Equal(original, Result<int>.Failure(original).Ensure(_ => false, Message).Error);
    }

    [Fact]
    public void Ensure_NullMessage_Throws() =>
        _ = Assert.Throws<ArgumentNullException>(() => Result<int>.Success(1).Ensure(_ => true, null!));

    [Fact]
    public async Task EnsureAsync_PredicateFails_UsesMessage()
    {
        Result<int> result = await Result<int>.Success(-1).EnsureAsync(value => Task.FromResult(value > 0), Message);
        Assert.Equal(Message, result.Error.Message);
    }

    [Fact]
    public async Task EnsureAsync_PredicatePasses_KeepsValue()
    {
        Result<int> result = await Result<int>.Success(1).EnsureAsync(value => Task.FromResult(value > 0), Message);
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public async Task TaskEnsureAsync_SyncPredicateFails_UsesMessage()
    {
        Result<int> result = await Task.FromResult(Result<int>.Success(-1)).EnsureAsync(value => value > 0, Message);
        Assert.Equal(Message, result.Error.Message);
    }

    [Fact]
    public async Task TaskEnsureAsync_AsyncPredicateFails_UsesMessage()
    {
        Result<int> result = await Task.FromResult(Result<int>.Success(-1)).EnsureAsync(value => Task.FromResult(value > 0), Message);
        Assert.Equal(Message, result.Error.Message);
    }

    [Fact]
    public async Task TaskEnsureAsync_NullTask_ThrowsWithResultTaskParameter()
    {
        Task<Result<int>> task = null!;
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => task.EnsureAsync(_ => true, Message));
        Assert.Equal("resultTask", exception.ParamName);
    }

    // -- Task<Option<T>>.ToResultAsync --

    [Fact]
    public async Task ToResultAsync_Some_ReturnsSuccess()
    {
        Result<int> result = await Task.FromResult(Option<int>.Some(1)).ToResultAsync(Error.FromMessage(Message));
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public async Task ToResultAsync_None_ReturnsFailure()
    {
        var error = Error.FromMessage(Message);
        Result<int> result = await Task.FromResult(Option<int>.None).ToResultAsync(error);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public async Task ToResultAsync_CustomError_ReturnsCustomFailure()
    {
        Result<int, int> result = await Task.FromResult(Option<int>.None).ToResultAsync(404);
        Assert.Equal(404, result.Error);
    }

    [Fact]
    public async Task ToResultAsync_ContinuesPipeline()
    {
        Result<int> result = await Task.FromResult(Option<int>.Some(1))
            .ToResultAsync(Error.FromMessage(Message))
            .BindAsync(value => Task.FromResult(Result<int>.Success(value + 1)));
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public async Task ToResultAsync_NullTask_ThrowsWithOptionTaskParameter()
    {
        Task<Option<int>> task = null!;
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => task.ToResultAsync(Error.FromMessage(Message)));
        Assert.Equal("optionTask", exception.ParamName);
    }
}
