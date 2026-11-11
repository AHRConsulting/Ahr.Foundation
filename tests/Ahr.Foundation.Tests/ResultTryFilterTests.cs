// Explicit result types check which overload each call binds to.
#pragma warning disable IDE0007

namespace Ahr.Foundation.Tests;

public sealed class ResultTryFilterTests
{
    private static bool IsIo(Exception exception) => exception is IOException;

    private static void ThrowIo() => throw new IOException("disk");

    private static void ThrowInvalid() => throw new InvalidOperationException("bug");

    private static int ReturnIo() => throw new IOException("disk");

    private static int ReturnInvalid() => throw new InvalidOperationException("bug");

    // -- Try --

    [Fact]
    public void Try_MatchingException_ReturnsFailure()
    {
        Result payloadFree = Result.Try(ThrowIo, IsIo);
        Result<int> value = Result.Try(ReturnIo, IsIo);
        Result<int, string> custom = Result.Try(ReturnIo, IsIo, ex => ex.Message);

        Assert.Equal("disk", payloadFree.Error.Message);
        _ = Assert.IsType<IOException>(value.Error.Exception);
        Assert.Equal("disk", custom.Error);
    }

    [Fact]
    public void Try_OtherException_Propagates()
    {
        _ = Assert.Throws<InvalidOperationException>(() => Result.Try(ThrowInvalid, IsIo));
        _ = Assert.Throws<InvalidOperationException>(() => Result.Try(ReturnInvalid, IsIo));
        _ = Assert.Throws<InvalidOperationException>(() => Result.Try(ReturnInvalid, IsIo, ex => ex.Message));
    }

    [Fact]
    public void Try_Success_ReturnsValueWithoutCallingFilter()
    {
        var called = false;
        Result<int> result = Result.Try(() => 5, _ => called = true);

        Assert.Equal(5, result.Value);
        Assert.False(called);
    }

    [Fact]
    public void Try_Cancellation_PropagatesWithoutCallingFilter()
    {
        var called = false;
        _ = Assert.Throws<OperationCanceledException>(() => Result.Try(() => throw new OperationCanceledException(), _ => called = true));
        Assert.False(called);
    }

    [Fact]
    public void Try_FilterWithBoolFunction_BindsToFilterOverload()
    {
        // A lambda returning bool is a filter, not an error factory producing Result<int, bool>.
        Result<int> result = Result.Try(ReturnIo, ex => ex is IOException);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Try_NullFilter_ThrowsWithExactParameter() =>
        Assert.Equal("catchWhen", Assert.Throws<ArgumentNullException>(() => Result.Try(ThrowIo, null!)).ParamName);

    // -- TryAsync --

    [Fact]
    public async Task TryAsync_MatchingException_ReturnsFailure()
    {
        Result payloadFree = await Result.TryAsync(async () => { await Task.Yield(); ThrowIo(); }, IsIo);
        Result<int> value = await Result.TryAsync(async () => { await Task.Yield(); return ReturnIo(); }, IsIo);
        Result<int, string> custom = await Result.TryAsync(async () => { await Task.Yield(); return ReturnIo(); }, IsIo, ex => ex.Message);

        Assert.Equal("disk", payloadFree.Error.Message);
        Assert.Equal("disk", value.Error.Message);
        Assert.Equal("disk", custom.Error);
    }

    [Fact]
    public async Task TryAsync_ExceptionBeforeFirstAwait_IsFiltered()
    {
        Result matched = await Result.TryAsync(() => { ThrowIo(); return Task.CompletedTask; }, IsIo);

        Assert.True(matched.IsFailure);
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => Result.TryAsync(() => { ThrowInvalid(); return Task.CompletedTask; }, IsIo));
    }

    [Fact]
    public async Task TryAsync_OtherException_Propagates()
    {
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => Result.TryAsync(async () => { await Task.Yield(); ThrowInvalid(); }, IsIo));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => Result.TryAsync(async () => { await Task.Yield(); return ReturnInvalid(); }, IsIo));
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => Result.TryAsync(async () => { await Task.Yield(); return ReturnInvalid(); }, IsIo, ex => ex.Message));
    }

    // -- TryAsync with a cancellation token --

    [Fact]
    public async Task TryAsync_Token_MatchingException_ReturnsFailure()
    {
        using var cancellation = new CancellationTokenSource();

        Result payloadFree = await Result.TryAsync(async _ => { await Task.Yield(); ThrowIo(); }, IsIo, cancellation.Token);
        Result<int> value = await Result.TryAsync(async _ => { await Task.Yield(); return ReturnIo(); }, IsIo, cancellation.Token);
        Result<int, string> custom = await Result.TryAsync(async _ => { await Task.Yield(); return ReturnIo(); }, IsIo, ex => ex.Message, cancellation.Token);

        Assert.True(payloadFree.IsFailure);
        Assert.True(value.IsFailure);
        Assert.Equal("disk", custom.Error);
    }

    [Fact]
    public async Task TryAsync_Token_OtherException_Propagates()
    {
        using var cancellation = new CancellationTokenSource();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => Result.TryAsync(async _ => { await Task.Yield(); return ReturnInvalid(); }, IsIo, cancellation.Token));
    }

    [Fact]
    public async Task TryAsync_Token_Timeout_BecomesFailureWithoutCallingFilter()
    {
        using var cancellation = new CancellationTokenSource();
        var called = false;
        static Task<int> TimesOut(CancellationToken _) => Task.FromException<int>(new TaskCanceledException("timeout"));

        Result<int> value = await Result.TryAsync(TimesOut, _ => called = true, cancellation.Token);
        Result<int, string> custom = await Result.TryAsync(TimesOut, IsIo, ex => ex.Message, cancellation.Token);
        Result payloadFree = await Result.TryAsync(_ => Task.FromException(new TaskCanceledException("timeout")), IsIo, cancellation.Token);

        Assert.True(value.Error.IsCancellation);
        Assert.Equal("timeout", custom.Error);
        Assert.True(payloadFree.Error.IsCancellation);
        Assert.False(called);
    }

    [Fact]
    public async Task TryAsync_Token_CallerCanceled_PropagatesWithoutCallingFilter()
    {
        using var cancellation = new CancellationTokenSource();
        var called = false;

        _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Result.TryAsync(
            async token => { await cancellation.CancelAsync(); token.ThrowIfCancellationRequested(); },
            _ => called = true,
            cancellation.Token));
        Assert.False(called);
    }

    // -- A null success --

    [Fact]
    public async Task Try_NullSuccess_ThrowsWithMessageNamingTheCause()
    {
        string? missing = null;
        ArgumentNullException sync = Assert.Throws<ArgumentNullException>(() => Result.Try(() => missing));
        ArgumentNullException custom = Assert.Throws<ArgumentNullException>(() => Result.Try(() => missing, ex => ex.Message));
        ArgumentNullException awaited = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.TryAsync(() => Task.FromResult(missing)));
        ArgumentNullException customAsync = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.TryAsync(() => Task.FromResult(missing), ex => ex.Message));
        ArgumentNullException token = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.TryAsync(_ => Task.FromResult(missing), CancellationToken.None));
        ArgumentNullException customToken = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.TryAsync(_ => Task.FromResult(missing), ex => ex.Message, CancellationToken.None));

        Assert.All([sync, custom], ex => Assert.StartsWith("The operation passed to Try returned null", ex.Message, StringComparison.Ordinal));
        Assert.All([awaited, customAsync, token, customToken], ex => Assert.StartsWith("The operation passed to TryAsync returned null", ex.Message, StringComparison.Ordinal));
        Assert.All([sync, custom, awaited, customAsync, token, customToken], ex => Assert.Equal("operation", ex.ParamName));
    }

    [Fact]
    public void Try_NullableValueTypeWithValue_Succeeds()
    {
        int? present = 3;
        Assert.Equal(3, Result.Try(() => present).Value);
    }

    // -- Error.IsCancellation --

    [Fact]
    public void IsCancellation_ReflectsTheException()
    {
        Assert.True(Error.FromException(new OperationCanceledException()).IsCancellation);
        Assert.True(Error.FromException(new TaskCanceledException()).IsCancellation);
        Assert.False(Error.FromException(new IOException()).IsCancellation);
        Assert.False(Error.FromMessage("failed").IsCancellation);
        Assert.False(default(Error).IsCancellation);
    }
}
