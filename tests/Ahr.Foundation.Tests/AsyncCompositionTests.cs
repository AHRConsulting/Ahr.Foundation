namespace Ahr.Foundation.Tests;

public sealed class AsyncCompositionTests
{
    private static readonly BusinessContext _asyncComposition = BusinessContext
        .Define("async-functional-composition", "Asynchronous functional composition", "Compose Result and Option workflows over asynchronous operations while preserving their outcomes.")
        .Capability("compose-async-results", "Continue Result pipelines over asynchronous operations")
        .Capability("compose-async-options", "Continue Option pipelines over asynchronous operations")
        .Outcome("composed-values-preserved", "Successful values remain available after asynchronous composition")
        .Outcome("failures-preserved", "Failures and their errors remain available through asynchronous composition")
        .Outcome("cancellation-propagated", "Cancellation and original exceptions propagate from asynchronous operations")
        .Rule("callbacks-follow-outcomes", "Callbacks and fallbacks run only for the matching success, failure, or absence outcome")
        .Build();

    [Fact]
    public Task BindAsync_Success_ComposesValue()
    {
        Result<int>? result = null;

        return Scenario.Define("Asynchronous binding composes a successful value", _asyncComposition)
            .When("A successful value is bound to an asynchronous transformation", async () =>
            {
                result = await Result<int>.Success(20).BindAsync(value => Task.FromResult(Result<int>.Success(value + 2)));
            })
            .Then("The bound result contains the transformed value", () =>
                Assert.Equal(22, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task MapAsync_FaultedCallback_PropagatesOriginalException()
    {
        var expected = new InvalidOperationException("boom");

        return Scenario.Define("An asynchronous mapper propagates its original exception", _asyncComposition)
            .WhenThrows<InvalidOperationException>("The mapper task faults", async () =>
                await Result<int>.Success(1).MapAsync(_ => Task.FromException<int>(expected)))
            .Then("The original exception instance is propagated", (InvalidOperationException exception) =>
                Assert.Same(expected, exception))
            .VerifyAsync();
    }

    [Fact]
    public async Task BindAsync_CanceledCallback_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Scenario.Define("Asynchronous binding propagates callback cancellation", _asyncComposition)
            .WhenThrowsAny<OperationCanceledException>("The bound operation returns a canceled task", async () =>
                await Result<int>.Success(1).BindAsync(_ => Task.FromCanceled<Result<int>>(cancellation.Token)))
            .Then("The cancellation is associated with the caller token", (OperationCanceledException exception) =>
                Assert.Equal(cancellation.Token, exception.CancellationToken))
            .VerifyAsync();
    }

    [Fact]
    public Task MapAsync_NullReturnedTask_ThrowsWithMapParameter() =>
        Scenario.Define("Asynchronous mapping rejects a null returned task", _asyncComposition)
            .WhenThrows<ArgumentNullException>("The mapper returns no task", async () =>
                await Result<int>.Success(1).MapAsync<int>(_ => null!))
            .Then("The refusal identifies the mapper parameter", (ArgumentNullException refusal) =>
                Assert.Equal("map", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TaskReceiver_NullResultTask_ThrowsWithResultTaskParameter()
    {
        Task<Result<int>> resultTask = null!;

        return Scenario.Define("Task composition rejects a missing result task", _asyncComposition)
            .WhenThrows<ArgumentNullException>("A null result task is mapped", async () =>
                await resultTask.MapAsync(value => Task.FromResult(value + 1)))
            .Then("The refusal identifies the result task parameter", (ArgumentNullException refusal) =>
                Assert.Equal("resultTask", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_FaultedResultTask_PropagatesOriginalException()
    {
        var expected = new InvalidOperationException("source");

        return Scenario.Define("Task composition propagates the source task exception", _asyncComposition)
            .WhenThrows<InvalidOperationException>("The source result task faults", async () =>
                await Task.FromException<Result<int>>(expected).MapAsync(value => Task.FromResult(value + 1)))
            .Then("The original exception instance is propagated", (InvalidOperationException exception) =>
                Assert.Same(expected, exception))
            .VerifyAsync();
    }

    [Fact]
    public async Task TaskReceiver_CanceledOptionTask_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        _ = await Scenario.Define("Task composition propagates a canceled option task", _asyncComposition)
            .WhenThrowsAny<OperationCanceledException>("The source option task is canceled", async () =>
                await Task.FromCanceled<Option<int>>(cancellation.Token).MapAsync(value => Task.FromResult(value + 1)))
            .Then("The cancellation is associated with the caller token", (OperationCanceledException exception) =>
                Assert.Equal(cancellation.Token, exception.CancellationToken))
            .VerifyAsync();
    }

    [Fact]
    public Task ValueTaskAsTask_FrameworkAdaptation_ComposesSuccessfully()
    {
        static ValueTask<Result<int>> LoadAsync(int value) => ValueTask.FromResult(Result<int>.Success(value * 2));
        Result<int>? result = null;

        return Scenario.Define("A ValueTask adapts to asynchronous Result binding", _asyncComposition)
            .When("A successful result binds through the ValueTask adapter", async () =>
            {
                result = await Result<int>.Success(21).BindAsync(value => LoadAsync(value).AsTask());
            })
            .Then("The adapted operation returns the expected value", () =>
                Assert.Equal(42, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task EnsureAsync_FalsePredicate_ReturnsFailure()
    {
        Result<int>? result = null;

        return Scenario.Define("Asynchronous validation returns a failure for a false predicate", _asyncComposition)
            .When("The successful result is checked by an asynchronous false predicate", async () =>
            {
                result = await Result<int>.Success(1).EnsureAsync(_ => Task.FromResult(false), new Error("bad"));
            })
            .Then("The result contains the supplied validation error", () =>
                Assert.Equal(new Error("bad"), result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task OptionWhereAsync_None_DoesNotInvokePredicate()
    {
        var calls = 0;
        Option<int>? result = null;

        return Scenario.Define("Filtering an absent option skips its predicate", _asyncComposition)
            .When("An absent option is filtered asynchronously", async () =>
            {
                result = await Option<int>.None.WhereAsync(_ =>
                {
                    calls++;
                    return Task.FromResult(true);
                });
            })
            .Then("The predicate is not invoked and the option remains absent", () =>
            {
                Assert.Equal(0, calls);
                Assert.True(result?.IsNone);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task MatchAsync_NullFailureDelegate_ThrowsWithOnFailureParameter() =>
        Scenario.Define("Asynchronous matching rejects a missing failure delegate", _asyncComposition)
            .WhenThrows<ArgumentNullException>("A successful result is matched without a failure delegate", async () =>
                await Result<int>.Success(1).MatchAsync(value => Task.FromResult(value), null!))
            .Then("The refusal identifies the failure-handler parameter", (ArgumentNullException refusal) =>
                Assert.Equal("onFailure", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task DefaultBindAsync_UninitializedResult_ThrowsConsistentException() =>
        Scenario.Define("Asynchronous binding rejects an uninitialized result", _asyncComposition)
            .WhenThrows<InvalidOperationException>("An uninitialized result is bound asynchronously", async () =>
                await default(Result<int>).BindAsync(value => Task.FromResult(Result<int>.Success(value))))
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) =>
                Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task TaskReceiver_Result_BindAsync_Success_Chains()
    {
        Result? result = null;

        return Scenario.Define("A payload-free result task binds to another result task", _asyncComposition)
            .When("The successful task result is bound asynchronously", async () =>
            {
                result = await Task.FromResult(Result.Success()).BindAsync(() => Task.FromResult(Result.Success()));
            })
            .Then("The composed result succeeds", () =>
                Assert.True(result?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_Result_BindAsync_ToResultOfT_Success_Chains()
    {
        Result<int>? result = null;

        return Scenario.Define("A payload-free result task binds to a value result task", _asyncComposition)
            .When("The successful task result is bound to a value", async () =>
            {
                result = await Task.FromResult(Result.Success()).BindAsync(() => Task.FromResult(Result<int>.Success(42)));
            })
            .Then("The composed result succeeds with the bound value", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(42, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_Result_TapAsync_Success_Executes()
    {
        var tapped = false;
        Result? result = null;

        return Scenario.Define("Tapping a successful result task executes its callback", _asyncComposition)
            .When("The successful task result is tapped", async () =>
            {
                result = await Task.FromResult(Result.Success()).TapAsync(() =>
                {
                    tapped = true;
                    return Task.CompletedTask;
                });
            })
            .Then("The callback runs and the result remains successful", () =>
            {
                Assert.True(tapped);
                Assert.True(result?.IsSuccess);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_Result_TapErrorAsync_Failure_Executes()
    {
        var tapped = false;
        Result? result = null;

        return Scenario.Define("Tapping a failed result task executes its error callback", _asyncComposition)
            .When("The failed task result is tapped for its error", async () =>
            {
                result = await Task.FromResult(Result.Failure(new Error("err"))).TapErrorAsync(_ =>
                {
                    tapped = true;
                    return Task.CompletedTask;
                });
            })
            .Then("The callback runs and the result remains failed", () =>
            {
                Assert.True(tapped);
                Assert.True(result?.IsFailure);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_Result_OrElseAsync_Success_ShortCircuits()
    {
        var called = false;
        Result? result = null;

        return Scenario.Define("A successful result task skips its asynchronous fallback", _asyncComposition)
            .When("The successful task result is given a fallback", async () =>
            {
                result = await Task.FromResult(Result.Success()).OrElseAsync(() =>
                {
                    called = true;
                    return Task.FromResult(Result.Success());
                });
            })
            .Then("The fallback is skipped and the result succeeds", () =>
            {
                Assert.False(called);
                Assert.True(result?.IsSuccess);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_Result_OrElseAsync_Failure_InvokesFallback()
    {
        Result? result = null;

        return Scenario.Define("A failed result task invokes its asynchronous fallback", _asyncComposition)
            .When("The failed task result is given a successful fallback", async () =>
            {
                result = await Task.FromResult(Result.Failure(new Error("err"))).OrElseAsync(() => Task.FromResult(Result.Success()));
            })
            .Then("The fallback result succeeds", () =>
                Assert.True(result?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfT_BindAsync_ToResult_Success_Chains()
    {
        Result? result = null;

        return Scenario.Define("A value result task binds to a payload-free result task", _asyncComposition)
            .When("The successful task result is bound to a payload-free result", async () =>
            {
                result = await Task.FromResult(Result<int>.Success(42)).BindAsync(_ => Task.FromResult(Result.Success()));
            })
            .Then("The composed result succeeds", () =>
                Assert.True(result?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfT_BindAsync_ToResultOfTOut_Success_Chains()
    {
        Result<string>? result = null;

        return Scenario.Define("A value result task binds to a different value type", _asyncComposition)
            .When("The successful task result is bound to a string result", async () =>
            {
                result = await Task.FromResult(Result<int>.Success(42)).BindAsync(value => Task.FromResult(Result<string>.Success($"val: {value}")));
            })
            .Then("The composed string result succeeds with the mapped text", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal("val: 42", result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfT_MapErrorAsync_TransformsError()
    {
        Result<int>? result = null;

        return Scenario.Define("An asynchronous error mapper transforms a task result error", _asyncComposition)
            .When("The failed result task's error is mapped asynchronously", async () =>
            {
                result = await Task.FromResult(Result<int>.Failure(new Error("original")))
                    .MapErrorAsync(error => Task.FromResult(new Error($"mapped: {error.Message}")));
            })
            .Then("The mapped error remains a failure with its transformed message", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Equal("mapped: original", result?.Error.Message);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfT_TapAsync_Executes()
    {
        var observed = 0;
        Result<int>? result = null;

        return Scenario.Define("Tapping a successful value result task observes its value", _asyncComposition)
            .When("The successful task result is tapped", async () =>
            {
                result = await Task.FromResult(Result<int>.Success(42)).TapAsync(value =>
                {
                    observed = value;
                    return Task.CompletedTask;
                });
            })
            .Then("The callback observes the value and the result remains successful", () =>
            {
                Assert.Equal(42, observed);
                Assert.True(result?.IsSuccess);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfT_TapErrorAsync_Executes()
    {
        var observed = "";
        Result<int>? result = null;

        return Scenario.Define("Tapping a failed value result task observes its error", _asyncComposition)
            .When("The failed task result is tapped for its error", async () =>
            {
                result = await Task.FromResult(Result<int>.Failure(new Error("bad"))).TapErrorAsync(error =>
                {
                    observed = error.Message;
                    return Task.CompletedTask;
                });
            })
            .Then("The callback observes the error and the result remains failed", () =>
            {
                Assert.Equal("bad", observed);
                Assert.True(result?.IsFailure);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfT_EnsureAsync_PredicateFalse_ReturnsFailure()
    {
        Result<int>? result = null;

        return Scenario.Define("Asynchronous task validation rejects a value above the limit", _asyncComposition)
            .When("The successful task result is checked asynchronously", async () =>
            {
                result = await Task.FromResult(Result<int>.Success(42)).EnsureAsync(value => Task.FromResult(value < 10), new Error("too large"));
            })
            .Then("The failed result contains the validation message", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Equal("too large", result?.Error.Message);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfT_OrElseAsync_Success_ShortCircuits()
    {
        var called = false;
        Result<int>? result = null;

        return Scenario.Define("A successful value result task skips its fallback", _asyncComposition)
            .When("The successful task result is given an asynchronous fallback", async () =>
            {
                result = await Task.FromResult(Result<int>.Success(42)).OrElseAsync(() =>
                {
                    called = true;
                    return Task.FromResult(Result<int>.Success(0));
                });
            })
            .Then("The fallback is skipped and the original value remains", () =>
            {
                Assert.False(called);
                Assert.Equal(42, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfT_OrElseAsync_Failure_InvokesFallback()
    {
        Result<int>? result = null;

        return Scenario.Define("A failed value result task invokes its fallback", _asyncComposition)
            .When("The failed task result is given a successful fallback", async () =>
            {
                result = await Task.FromResult(Result<int>.Failure(new Error("bad"))).OrElseAsync(() => Task.FromResult(Result<int>.Success(99)));
            })
            .Then("The fallback result succeeds with its value", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(99, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfTAndTError_BindAsync_Chains()
    {
        Result<string, string>? result = null;

        return Scenario.Define("A custom-error result task binds to a new value", _asyncComposition)
            .When("The successful task result is bound to a transformed value", async () =>
            {
                result = await Task.FromResult(Result<int, string>.Success(42))
                    .BindAsync(value => Task.FromResult(Result<string, string>.Success($"ok: {value}")));
            })
            .Then("The composed result succeeds with the transformed text", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal("ok: 42", result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfTAndTError_MapAsync_TransformsValue()
    {
        Result<string, string>? result = null;

        return Scenario.Define("Asynchronous mapping transforms a custom-error result value", _asyncComposition)
            .When("The successful task result is mapped to text", async () =>
            {
                result = await Task.FromResult(Result<int, string>.Success(42)).MapAsync(value => Task.FromResult($"ok: {value}"));
            })
            .Then("The mapped custom-error result succeeds with the expected text", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal("ok: 42", result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfTAndTError_MapErrorAsync_TransformsError()
    {
        Result<int, int>? result = null;

        return Scenario.Define("Asynchronous error mapping transforms a custom error", _asyncComposition)
            .When("The failed custom-error result task's error is mapped to its length", async () =>
            {
                result = await Task.FromResult(Result<int, string>.Failure("original")).MapErrorAsync(error => Task.FromResult(error.Length));
            })
            .Then("The mapped result remains failed with the transformed error", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Equal(8, result?.Error);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfTAndTError_TapAsync_Executes()
    {
        var observed = 0;
        Result<int, string>? result = null;

        return Scenario.Define("Tapping a successful custom-error result task observes its value", _asyncComposition)
            .When("The successful task result is tapped", async () =>
            {
                result = await Task.FromResult(Result<int, string>.Success(42)).TapAsync(value =>
                {
                    observed = value;
                    return Task.CompletedTask;
                });
            })
            .Then("The callback observes the value and the result remains successful", () =>
            {
                Assert.Equal(42, observed);
                Assert.True(result?.IsSuccess);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfTAndTError_TapErrorAsync_Executes()
    {
        var observed = "";
        Result<int, string>? result = null;

        return Scenario.Define("Tapping a failed custom-error result task observes its error", _asyncComposition)
            .When("The failed task result is tapped for its error", async () =>
            {
                result = await Task.FromResult(Result<int, string>.Failure("err")).TapErrorAsync(error =>
                {
                    observed = error;
                    return Task.CompletedTask;
                });
            })
            .Then("The callback observes the error and the result remains failed", () =>
            {
                Assert.Equal("err", observed);
                Assert.True(result?.IsFailure);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfTAndTError_EnsureAsync_PredicateFalse_ReturnsFailure()
    {
        Result<int, string>? result = null;

        return Scenario.Define("Asynchronous task validation rejects a custom-error value above the limit", _asyncComposition)
            .When("The successful custom-error task result is checked asynchronously", async () =>
            {
                result = await Task.FromResult(Result<int, string>.Success(42)).EnsureAsync(value => Task.FromResult(value < 10), "too large");
            })
            .Then("The failed result contains the custom validation error", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Equal("too large", result?.Error);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfTAndTError_OrElseAsync_Success_ShortCircuits()
    {
        var called = false;
        Result<int, string>? result = null;

        return Scenario.Define("A successful custom-error result task skips its fallback", _asyncComposition)
            .When("The successful task result is given an asynchronous fallback", async () =>
            {
                result = await Task.FromResult(Result<int, string>.Success(42)).OrElseAsync(() =>
                {
                    called = true;
                    return Task.FromResult(Result<int, string>.Success(0));
                });
            })
            .Then("The fallback is skipped and the original value remains", () =>
            {
                Assert.False(called);
                Assert.Equal(42, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_ResultOfTAndTError_OrElseAsync_Failure_InvokesFallback()
    {
        Result<int, string>? result = null;

        return Scenario.Define("A failed custom-error result task invokes its fallback", _asyncComposition)
            .When("The failed task result is given a successful fallback", async () =>
            {
                result = await Task.FromResult(Result<int, string>.Failure("bad")).OrElseAsync(() => Task.FromResult(Result<int, string>.Success(99)));
            })
            .Then("The fallback result succeeds with its value", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(99, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_Option_BindAsync_Some_Chains()
    {
        Option<string>? result = null;

        return Scenario.Define("A present option task binds to a transformed option", _asyncComposition)
            .When("The present option is bound asynchronously", async () =>
            {
                result = await Task.FromResult(Option<int>.Some(42)).BindAsync(value => Task.FromResult(Option<string>.Some($"val: {value}")));
            })
            .Then("The composed option contains the transformed value", () =>
            {
                Assert.True(result?.IsSome);
                Assert.Equal("val: 42", result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_Option_WhereAsync_PredicateTrue_KeepsSome()
    {
        Option<int>? result = null;

        return Scenario.Define("Asynchronous filtering keeps a present option that matches", _asyncComposition)
            .When("The present option is checked by an asynchronous true predicate", async () =>
            {
                result = await Task.FromResult(Option<int>.Some(42)).WhereAsync(value => Task.FromResult(value > 10));
            })
            .Then("The option remains present with its original value", () =>
            {
                Assert.True(result?.IsSome);
                Assert.Equal(42, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_Option_TapAsync_Some_Executes()
    {
        var observed = 0;
        Option<int>? result = null;

        return Scenario.Define("Tapping a present option task observes its value", _asyncComposition)
            .When("The present option task is tapped", async () =>
            {
                result = await Task.FromResult(Option<int>.Some(42)).TapAsync(value =>
                {
                    observed = value;
                    return Task.CompletedTask;
                });
            })
            .Then("The callback observes the value and the option remains present", () =>
            {
                Assert.Equal(42, observed);
                Assert.True(result?.IsSome);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_Option_OrElseAsync_Some_ShortCircuits()
    {
        var called = false;
        Option<int>? result = null;

        return Scenario.Define("A present option task skips its fallback", _asyncComposition)
            .When("The present option task is given an asynchronous fallback", async () =>
            {
                result = await Task.FromResult(Option<int>.Some(42)).OrElseAsync(() =>
                {
                    called = true;
                    return Task.FromResult(Option<int>.Some(0));
                });
            })
            .Then("The fallback is skipped and the original value remains", () =>
            {
                Assert.False(called);
                Assert.Equal(42, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_Option_OrElseAsync_None_InvokesFallback()
    {
        Option<int>? result = null;

        return Scenario.Define("An absent option task invokes its fallback", _asyncComposition)
            .When("The absent option task is given a present fallback", async () =>
            {
                result = await Task.FromResult(Option<int>.None).OrElseAsync(() => Task.FromResult(Option<int>.Some(99)));
            })
            .Then("The fallback option is present with its value", () =>
            {
                Assert.True(result?.IsSome);
                Assert.Equal(99, result?.Value);
            })
            .VerifyAsync();
    }
}
