namespace Ahr.Foundation.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_Inspection_ReportsInitializedSuccess()
    {
        var result = Result.Success();
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void Failure_Inspection_ReportsError()
    {
        Error error = new("failure");
        var result = Result.Failure(error);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Failure_DefaultError_ThrowsWithErrorParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result.Failure(default));
        Assert.Equal("error", exception.ParamName);
    }

    [Fact]
    public void Default_IsSuccess_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = default(Result).IsSuccess);
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void Default_IsFailure_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = default(Result).IsFailure);
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void Default_Error_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = default(Result).Error);
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void Default_Match_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => default(Result).Match(() => 1, _ => 0));
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void Success_Error_ThrowsWrongBranchException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = Result.Success().Error);
        Assert.Equal("Cannot access the error of a successful result.", exception.Message);
    }

    [Fact]
    public void Match_Success_InvokesOnlySuccessBranch()
    {
        var failures = 0;
        var value = Result.Success().Match(() => 42, _ => { failures++; return 0; });
        Assert.Equal(42, value);
        Assert.Equal(0, failures);
    }

    [Fact]
    public void Match_NullSuccessDelegate_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result.Success().Match(null!, _ => 0));
        Assert.Equal("onSuccess", exception.ParamName);
    }

    [Fact]
    public async Task MatchAsync_Success_InvokesOnlySuccessBranchAndReturnsItsValue()
    {
        var failures = 0;
        var value = await Result.Success().MatchAsync(() => Task.FromResult(42), _ => { failures++; return Task.FromResult(0); });
        Assert.Equal(42, value);
        Assert.Equal(0, failures);
    }

    [Fact]
    public async Task MatchAsync_Failure_InvokesOnlyFailureBranchWithErrorAndReturnsItsValue()
    {
        var successes = 0;
        Error error = new("bad");
        var value = await Result.Failure(error).MatchAsync(() => { successes++; return Task.FromResult(0); }, err =>
        {
            Assert.Equal(error, err);
            return Task.FromResult(99);
        });
        Assert.Equal(99, value);
        Assert.Equal(0, successes);
    }

    [Fact]
    public async Task MatchAsync_NullSuccessDelegate_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.Success().MatchAsync(null!, _ => Task.FromResult(0)));
        Assert.Equal("onSuccess", exception.ParamName);
    }

    [Fact]
    public async Task MatchAsync_NullFailureDelegate_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.Failure(new Error("bad")).MatchAsync(() => Task.FromResult(0), null!));
        Assert.Equal("onFailure", exception.ParamName);
    }

    [Fact]
    public async Task MatchAsync_SuccessDelegateReturnsNullTask_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.Success().MatchAsync(() => null!, _ => Task.FromResult(0)));
        Assert.Equal("onSuccess", exception.ParamName);
    }

    [Fact]
    public async Task MatchAsync_FailureDelegateReturnsNullTask_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.Failure(new Error("bad")).MatchAsync(() => Task.FromResult(0), _ => null!));
        Assert.Equal("onFailure", exception.ParamName);
    }

    [Fact]
    public async Task MatchAsync_Uninitialized_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => default(Result).MatchAsync(() => Task.FromResult(1), _ => Task.FromResult(0)));
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void Bind_Failure_DoesNotInvokeDelegate()
    {
        var calls = 0;
        Result result = Result.Failure(new Error("bad")).Bind(() => { calls++; return Result.Success(); });
        Assert.Equal(0, calls);
        Assert.Equal(new Error("bad"), result.Error);
    }

    [Fact]
    public void TapError_Failure_InvokesActionAndPreservesResult()
    {
        Error observed = default;
        var original = Result.Failure(new Error("bad"));
        Result returned = original.TapError(error => observed = error);
        Assert.Equal(new Error("bad"), observed);
        Assert.Equal(original, returned);
    }

    [Fact]
    public void Equality_SameStateAndError_AreEqualWithSameHashCode()
    {
        var left = Result.Failure(new Error("bad"));
        var right = Result.Failure(new Error("bad"));
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Match_Action_Success_InvokesOnlySuccessAction()
    {
        var successCalled = false;
        var failureCalled = false;
        var result = Result.Success();
        Result returned = result.Match(() => { successCalled = true; }, _ => failureCalled = true);

        Assert.True(successCalled);
        Assert.False(failureCalled);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Match_Action_Failure_InvokesOnlyFailureAction()
    {
        var successCalled = false;
        var failureCalled = false;
        Error error = new("err");
        var result = Result.Failure(error);
        Result returned = result.Match(() => successCalled = true, err => { failureCalled = true; Assert.Equal(error, err); });

        Assert.False(successCalled);
        Assert.True(failureCalled);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Match_Action_NullSuccessAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result.Success().Match(null!, _ => { }));
        Assert.Equal("onSuccess", exception.ParamName);
    }

    [Fact]
    public void Match_Action_NullFailureAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result.Success().Match(() => { }, null!));
        Assert.Equal("onFailure", exception.ParamName);
    }

    [Fact]
    public async Task MatchAsync_Action_Success_InvokesOnlySuccessAsyncAction()
    {
        var successCalled = false;
        var failureCalled = false;
        var result = Result.Success();
        Result returned = await result.MatchAsync(() => { successCalled = true; return Task.CompletedTask; }, _ => { failureCalled = true; return Task.CompletedTask; });

        Assert.True(successCalled);
        Assert.False(failureCalled);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task MatchAsync_Action_Failure_InvokesOnlyFailureAsyncAction()
    {
        var successCalled = false;
        var failureCalled = false;
        Error error = new("err");
        var result = Result.Failure(error);
        Result returned = await result.MatchAsync(() => { successCalled = true; return Task.CompletedTask; }, err => { failureCalled = true; Assert.Equal(error, err); return Task.CompletedTask; });

        Assert.False(successCalled);
        Assert.True(failureCalled);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task MatchAsync_Action_NullSuccessAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.Success().MatchAsync(null!, _ => Task.CompletedTask));
        Assert.Equal("onSuccess", exception.ParamName);
    }

    [Fact]
    public async Task MatchAsync_Action_NullFailureAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.Success().MatchAsync(() => Task.CompletedTask, null!));
        Assert.Equal("onFailure", exception.ParamName);
    }

    [Fact]
    public void Tap_Success_InvokesActionAndReturnsSelf()
    {
        var tapped = false;
        var result = Result.Success();
        Result returned = result.Tap(() => tapped = true);

        Assert.True(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Tap_Failure_DoesNotInvokeAction()
    {
        var tapped = false;
        var result = Result.Failure(new Error("err"));
        Result returned = result.Tap(() => tapped = true);

        Assert.False(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Tap_NullAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result.Success().Tap(null!));
        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public async Task TapAsync_Success_InvokesAsyncActionAndReturnsSelf()
    {
        var tapped = false;
        var result = Result.Success();
        Result returned = await result.TapAsync(() => { tapped = true; return Task.CompletedTask; });

        Assert.True(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task TapAsync_Failure_DoesNotInvokeAsyncAction()
    {
        var tapped = false;
        var result = Result.Failure(new Error("err"));
        Result returned = await result.TapAsync(() => { tapped = true; return Task.CompletedTask; });

        Assert.False(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task TapAsync_NullAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.Success().TapAsync(null!));
        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public void TapError_Success_DoesNotInvokeAction()
    {
        var tapped = false;
        var result = Result.Success();
        Result returned = result.TapError(_ => tapped = true);

        Assert.False(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void TapError_NullAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result.Failure(new Error("err")).TapError(null!));
        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public async Task TapErrorAsync_Failure_InvokesAsyncAction()
    {
        var tapped = false;
        Error error = new("err");
        var result = Result.Failure(error);
        Result returned = await result.TapErrorAsync(err => { tapped = true; Assert.Equal(error, err); return Task.CompletedTask; });

        Assert.True(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task TapErrorAsync_Success_DoesNotInvokeAsyncAction()
    {
        var tapped = false;
        var result = Result.Success();
        Result returned = await result.TapErrorAsync(_ => { tapped = true; return Task.CompletedTask; });

        Assert.False(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task TapErrorAsync_NullAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.Failure(new Error("err")).TapErrorAsync(null!));
        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public void Bind_ToResultOfT_Success_ReturnsBoundResult()
    {
        var result = Result.Success();
        Result<int> bound = result.Bind(() => Result<int>.Success(123));

        Assert.True(bound.IsSuccess);
        Assert.Equal(123, bound.Value);
    }

    [Fact]
    public void Bind_ToResultOfT_Failure_PropagatesErrorWithoutInvokingBind()
    {
        var invoked = false;
        var result = Result.Failure(new Error("bad"));
        Result<int> bound = result.Bind(() => { invoked = true; return Result<int>.Success(123); });

        Assert.False(invoked);
        Assert.True(bound.IsFailure);
        Assert.Equal(new Error("bad"), bound.Error);
    }

    [Fact]
    public void Bind_ToResultOfT_NullBind_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result.Success().Bind<int>(null!));
        Assert.Equal("bind", exception.ParamName);
    }

    [Fact]
    public async Task BindAsync_ToResultOfT_Success_ReturnsBoundResult()
    {
        var result = Result.Success();
        Result<int> bound = await result.BindAsync(() => Task.FromResult(Result<int>.Success(123)));

        Assert.True(bound.IsSuccess);
        Assert.Equal(123, bound.Value);
    }

    [Fact]
    public async Task BindAsync_ToResultOfT_Failure_PropagatesErrorWithoutInvokingBind()
    {
        var invoked = false;
        var result = Result.Failure(new Error("bad"));
        Result<int> bound = await result.BindAsync(() => { invoked = true; return Task.FromResult(Result<int>.Success(123)); });

        Assert.False(invoked);
        Assert.True(bound.IsFailure);
        Assert.Equal(new Error("bad"), bound.Error);
    }

    [Fact]
    public async Task BindAsync_ToResultOfT_NullBind_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.Success().BindAsync<int>(null!));
        Assert.Equal("bind", exception.ParamName);
    }

    [Fact]
    public void OrElse_Success_ReturnsOriginalAndIgnoresFallback()
    {
        Result result = Result.Success().OrElse(Result.Failure(new Error("fallback")));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void OrElse_Failure_ReturnsEagerFallback()
    {
        var fallback = Result.Success();
        Result result = Result.Failure(new Error("bad")).OrElse(fallback);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void OrElse_Func_Success_DoesNotInvokeFallback()
    {
        var calls = 0;
        Result result = Result.Success().OrElse(() => { calls++; return Result.Failure(new Error("fallback")); });
        Assert.True(result.IsSuccess);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void OrElse_Func_Failure_InvokesFallback()
    {
        var calls = 0;
        Result result = Result.Failure(new Error("bad")).OrElse(() => { calls++; return Result.Success(); });
        Assert.True(result.IsSuccess);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void OrElse_FuncNullFallback_ThrowsWithFallbackParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result.Failure(new Error("bad")).OrElse(null!));
        Assert.Equal("fallback", exception.ParamName);
    }

    [Fact]
    public async Task OrElseAsync_Success_DoesNotInvokeFallback()
    {
        var calls = 0;
        Result result = await Result.Success().OrElseAsync(() => { calls++; return Task.FromResult(Result.Failure(new Error("fallback"))); });
        Assert.True(result.IsSuccess);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task OrElseAsync_Failure_InvokesFallback()
    {
        var calls = 0;
        Result result = await Result.Failure(new Error("bad")).OrElseAsync(() => { calls++; return Task.FromResult(Result.Success()); });
        Assert.True(result.IsSuccess);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task OrElseAsync_NullFallback_ThrowsWithFallbackParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result.Failure(new Error("bad")).OrElseAsync(null!));
        Assert.Equal("fallback", exception.ParamName);
    }
}
