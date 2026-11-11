namespace Ahr.Foundation.Tests;

public sealed class TapStepTests
{
    private static readonly Error _failure = Error.FromMessage("failed");

    private static Result Fail(int _) => Result.Failure(_failure);
    private static Result Pass(int _) => Result.Success();

    // -- Result<T>.Tap --

    [Fact]
    public void Tap_PayloadFreeStepSucceeds_KeepsValue() => Assert.Equal(1, Result<int>.Success(1).Tap(Pass).Value);

    [Fact]
    public void Tap_PayloadFreeStepFails_ReturnsStepFailure() => Assert.Equal(_failure, Result<int>.Success(1).Tap(Fail).Error);

    [Fact]
    public void Tap_ExpressionLambdaReturningResult_StopsPipelineOnFailure()
    {
        var reached = false;
        Result<int> result = Result<int>.Success(1)
            .Tap(value => Fail(value + 1))
            .Tap(_ => reached = true);

        Assert.True(result.IsFailure);
        Assert.False(reached);
    }

    [Fact]
    public void Tap_ValueStepSucceeds_KeepsOriginalValue() =>
        Assert.Equal(1, Result<int>.Success(1).Tap(value => Result<string>.Success("other")).Value);

    [Fact]
    public void Tap_ValueStepFails_ReturnsStepFailure() =>
        Assert.Equal(_failure, Result<int>.Success(1).Tap(_ => Result<string>.Failure(_failure)).Error);

    [Fact]
    public void Tap_AlreadyFailed_SkipsStep()
    {
        var invoked = false;
        var other = Error.FromMessage("other");

        Result<int> result = Result<int>.Failure(other).Tap(_ => { invoked = true; return Result.Success(); });

        Assert.False(invoked);
        Assert.Equal(other, result.Error);
    }

    [Fact]
    public void Tap_BlockBodyWithoutReturn_IgnoresStepResult() =>
        Assert.True(Result<int>.Success(1).Tap(value => { _ = Fail(value); }).IsSuccess);

    [Fact]
    public void Tap_NonResultReturningLambda_RemainsAction()
    {
        List<int> values = [];
        Result<int> result = Result<int>.Success(1).Tap(value => values.Add(value + 1));

        Assert.True(result.IsSuccess);
        Assert.Equal([2], values);
    }

    [Fact]
    public void Tap_NullStep_ThrowsWithActionParameter()
    {
        ArgumentNullException payloadFree = Assert.Throws<ArgumentNullException>(() => Result<int>.Success(1).Tap(null!));
        ArgumentNullException value = Assert.Throws<ArgumentNullException>(() => Result<int>.Success(1).Tap((Func<int, Result<string>>)null!));
        Assert.Equal("action", payloadFree.ParamName);
        Assert.Equal("action", value.ParamName);
    }

    [Fact]
    public void Tap_Uninitialized_Throws() =>
        _ = Assert.Throws<InvalidOperationException>(() => default(Result<int>).Tap(Pass));

    // -- Result<T>.TapAsync --

    [Fact]
    public async Task TapAsync_PayloadFreeStepSucceeds_KeepsValue()
    {
        Result<int> result = await Result<int>.Success(1).TapAsync(value => Task.FromResult(Pass(value)));
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public async Task TapAsync_AsyncLambdaFails_ReturnsStepFailure()
    {
        Result<int> result = await Result<int>.Success(1).TapAsync(async value =>
        {
            await Task.Yield();
            return Fail(value);
        });

        Assert.Equal(_failure, result.Error);
    }

    [Fact]
    public async Task TapAsync_ValueStep_KeepsOriginalValue()
    {
        Result<int> kept = await Result<int>.Success(1).TapAsync(_ => Task.FromResult(Result<string>.Success("other")));
        Result<int> failed = await Result<int>.Success(1).TapAsync(_ => Task.FromResult(Result<string>.Failure(_failure)));

        Assert.Equal(1, kept.Value);
        Assert.Equal(_failure, failed.Error);
    }

    [Fact]
    public async Task TapAsync_PlainTask_RemainsSideEffect()
    {
        var invoked = false;
        Result<int> result = await Result<int>.Success(1).TapAsync(async _ =>
        {
            await Task.Yield();
            invoked = true;
        });

        Assert.True(invoked);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task TapAsync_StepReturnsNullTask_Throws()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result<int>.Success(1).TapAsync(_ => null!));
        Assert.Equal("action", exception.ParamName);
    }

    // -- Task<Result<T>>.TapAsync --

    [Fact]
    public async Task TaskTapAsync_SyncAndAsyncSteps_StopOnFirstFailure()
    {
        var reached = false;
        Result<int> result = await Task.FromResult(Result<int>.Success(1))
            .TapAsync(value => Pass(value + 1))
            .TapAsync(value => Task.FromResult(Pass(value)))
            .TapAsync(_ => Result<string>.Success("other"))
            .TapAsync(_ => Task.FromResult(Result<string>.Failure(_failure)))
            .TapAsync(_ => reached = true);

        Assert.Equal(_failure, result.Error);
        Assert.False(reached);
    }

    [Fact]
    public async Task TaskTapAsync_NullTask_ThrowsWithResultTaskParameter()
    {
        Task<Result<int>> task = null!;
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => task.TapAsync(value => Pass(value)));
        Assert.Equal("resultTask", exception.ParamName);
    }

    // -- Result<T, TError> --

    [Fact]
    public void CustomErrorTap_StepSucceeds_KeepsValue() =>
        Assert.Equal(1, Result<int, int>.Success(1).Tap(_ => Result<string, int>.Success("other")).Value);

    [Fact]
    public void CustomErrorTap_StepFails_ReturnsStepError() =>
        Assert.Equal(404, Result<int, int>.Success(1).Tap(_ => Result<string, int>.Failure(404)).Error);

    [Fact]
    public void CustomErrorTap_AlreadyFailed_SkipsStep()
    {
        var invoked = false;
        Result<int, int> result = Result<int, int>.Failure(500).Tap(_ => { invoked = true; return Result<string, int>.Success("other"); });

        Assert.False(invoked);
        Assert.Equal(500, result.Error);
    }

    [Fact]
    public async Task CustomErrorTapAsync_SyncAndAsyncSteps_StopOnFirstFailure()
    {
        var reached = false;
        Result<int, int> result = await Task.FromResult(Result<int, int>.Success(1))
            .TapAsync(_ => Result<string, int>.Success("other"))
            .TapAsync(_ => Task.FromResult(Result<string, int>.Failure(404)))
            .TapAsync(_ => reached = true);

        Assert.Equal(404, result.Error);
        Assert.False(reached);
    }

    [Fact]
    public async Task CustomErrorTapAsync_InstanceStep_KeepsValue()
    {
        Result<int, int> result = await Result<int, int>.Success(1).TapAsync(_ => Task.FromResult(Result<string, int>.Success("other")));
        Assert.Equal(1, result.Value);
    }

    // -- Result --

    [Fact]
    public void ResultTap_StepFails_ReturnsStepFailure()
    {
        var reached = false;
        Result result = Result.Success()
            .Tap(() => Fail(1))
            .Tap(() => reached = true);

        Assert.Equal(_failure, result.Error);
        Assert.False(reached);
    }

    [Fact]
    public void ResultTap_StepSucceeds_RemainsSuccess() => Assert.True(Result.Success().Tap(() => Pass(1)).IsSuccess);

    [Fact]
    public void ResultTap_ValueStep_FailureStopsAndSuccessDropsValue()
    {
        Assert.True(Result.Success().Tap(() => Result<string>.Success("other")).IsSuccess);
        Assert.Equal(_failure, Result.Success().Tap(() => Result<string>.Failure(_failure)).Error);
    }

    [Fact]
    public void ResultTap_AlreadyFailed_SkipsStep()
    {
        var invoked = false;
        Result result = Result.Failure(_failure).Tap(() => { invoked = true; return Result.Success(); });

        Assert.False(invoked);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void ResultTap_BlockBodyWithoutReturn_IgnoresStepResult() =>
        Assert.True(Result.Success().Tap(() => { _ = Fail(1); }).IsSuccess);

    [Fact]
    public async Task ResultTapAsync_Steps_FailureStops()
    {
        Result payloadFree = await Result.Success().TapAsync(() => Task.FromResult(Fail(1)));
        Result value = await Result.Success().TapAsync(() => Task.FromResult(Result<string>.Failure(_failure)));
        Result passed = await Result.Success().TapAsync(() => Task.FromResult(Result<string>.Success("other")));

        Assert.Equal(_failure, payloadFree.Error);
        Assert.Equal(_failure, value.Error);
        Assert.True(passed.IsSuccess);
    }

    [Fact]
    public async Task ResultTapAsync_StepReturnsNullTask_Throws()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Result.Success().TapAsync(() => (Task<Result<string>>)null!));
        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public async Task TaskResultTapAsync_SyncAndAsyncSteps_StopOnFirstFailure()
    {
        var reached = false;
        Result result = await Task.FromResult(Result.Success())
            .TapAsync(() => Pass(1))
            .TapAsync(() => Task.FromResult(Pass(1)))
            .TapAsync(() => Result<string>.Success("other"))
            .TapAsync(() => Task.FromResult(Result<string>.Failure(_failure)))
            .TapAsync(() => reached = true);

        Assert.Equal(_failure, result.Error);
        Assert.False(reached);
    }

    // -- Option<T> --

    [Fact]
    public void OptionTap_StepReturnsSome_KeepsValue() =>
        Assert.Equal(1, Option<int>.Some(1).Tap(_ => Option<string>.Some("other")).Value);

    [Fact]
    public void OptionTap_StepReturnsNone_StopsPipeline()
    {
        var reached = false;
        Option<int> option = Option<int>.Some(1)
            .Tap(_ => Option<string>.None)
            .Tap(_ => reached = true);

        Assert.True(option.IsNone);
        Assert.False(reached);
    }

    [Fact]
    public void OptionTap_None_SkipsStep()
    {
        var invoked = false;
        Option<int> option = Option<int>.None.Tap(_ => { invoked = true; return Option<string>.Some("other"); });

        Assert.False(invoked);
        Assert.True(option.IsNone);
    }

    [Fact]
    public async Task OptionTapAsync_Steps_NoneStops()
    {
        Option<int> kept = await Option<int>.Some(1).TapAsync(_ => Task.FromResult(Option<string>.Some("other")));
        Option<int> stopped = await Option<int>.Some(1).TapAsync(_ => Task.FromResult(Option<string>.None));

        Assert.Equal(1, kept.Value);
        Assert.True(stopped.IsNone);
    }

    [Fact]
    public async Task TaskOptionTapAsync_SyncAndAsyncSteps_StopOnNone()
    {
        var reached = false;
        Option<int> option = await Task.FromResult(Option<int>.Some(1))
            .TapAsync(_ => Option<string>.Some("other"))
            .TapAsync(_ => Task.FromResult(Option<string>.None))
            .TapAsync(_ => reached = true);

        Assert.True(option.IsNone);
        Assert.False(reached);
    }
}
