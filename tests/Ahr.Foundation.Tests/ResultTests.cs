namespace Ahr.Foundation.Tests;

public sealed class ResultTests
{
    private static readonly BusinessContext _resultWorkflows = BusinessContext
        .Define("result-based-workflows", "Result-based workflows", "Represent completed and failed operations and choose the matching recovery path.")
        .Capability("inspect-operation-outcomes", "Inspect whether an operation succeeded or failed")
        .Capability("select-outcome-handlers", "Run only the handler for the operation outcome")
        .Capability("compose-operation-results", "Continue successful operations and preserve failures")
        .Capability("observe-operation-outcomes", "Observe success or failure without changing the result")
        .Capability("recover-failed-operations", "Provide a fallback only when an operation fails")
        .Outcome("failures-retain-their-error", "Failed operations retain their original error")
        .Outcome("observations-preserve-the-result", "Observing an operation does not change its result")
        .Rule("uninitialized-results-are-rejected", "An uninitialized result cannot be inspected or composed")
        .Rule("required-handlers-are-present", "Required outcome handlers and fallback operations must be supplied")
        .Build();

    [Fact]
    public Task Success_Inspection_ReportsInitializedSuccess()
    {
        Result? result = null;

        return Scenario.Define("A completed operation reports success", _resultWorkflows)
            .When("The successful outcome is inspected", () => result = Result.Success())
            .Then("The result is initialized and successful", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.False(result?.IsFailure);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Failure_Inspection_ReportsError()
    {
        Error error = new("failure");
        Result? result = null;

        return Scenario.Define("A failed operation reports its error", _resultWorkflows)
            .When("The operation fails with an error", () => result = Result.Failure(error))
            .Then("The failure and its original error are available", () =>
            {
                Assert.True(result?.IsFailure);
                Assert.Equal(error, result?.Error);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Failure_DefaultError_ThrowsWithErrorParameter() =>
        Scenario.Define("A failed operation requires an error", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A failure is created without an error", () => _ = Result.Failure(default))
            .Then("The refusal identifies the error parameter", (ArgumentNullException refusal) => Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Default_IsSuccess_ThrowsConsistentUninitializedException() =>
        Scenario.Define("An uninitialized outcome cannot be inspected for success", _resultWorkflows)
            .WhenThrows<InvalidOperationException>("Success is inspected on an uninitialized result", () => _ = default(Result).IsSuccess)
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) =>
                Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Default_IsFailure_ThrowsConsistentUninitializedException() =>
        Scenario.Define("An uninitialized outcome cannot be inspected for failure", _resultWorkflows)
            .WhenThrows<InvalidOperationException>("Failure is inspected on an uninitialized result", () => _ = default(Result).IsFailure)
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) =>
                Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Default_Error_ThrowsConsistentUninitializedException() =>
        Scenario.Define("An uninitialized outcome has no error", _resultWorkflows)
            .WhenThrows<InvalidOperationException>("The error is read from an uninitialized result", () => _ = default(Result).Error)
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) =>
                Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Default_Match_ThrowsConsistentUninitializedException() =>
        Scenario.Define("An uninitialized outcome cannot select a handler", _resultWorkflows)
            .WhenThrows<InvalidOperationException>("A handler is selected for an uninitialized result", () =>
                _ = default(Result).Match(() => 1, _ => 0))
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) =>
                Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Success_Error_ThrowsWrongBranchException() =>
        Scenario.Define("A successful operation does not expose a failure error", _resultWorkflows)
            .WhenThrows<InvalidOperationException>("The error is read from a successful result", () => _ = Result.Success().Error)
            .Then("The result explains that it succeeded and has no error", (InvalidOperationException exception) =>
                Assert.Equal(ResultTestMessages.ErrorOfSuccess, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Match_Success_InvokesOnlySuccessBranch()
    {
        var failures = 0;
        int? value = null;

        return Scenario.Define("A successful outcome selects only its success handler", _resultWorkflows)
            .When("The outcome is matched", () =>
                value = Result.Success().Match(() => 42, _ =>
                {
                    failures++;
                    return 0;
                }))
            .Then("The success value is returned and failure handling is skipped", () =>
            {
                Assert.Equal(42, value);
                Assert.Equal(0, failures);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Match_NullSuccessDelegate_ThrowsWithExactParameter() =>
        Scenario.Define("Outcome matching requires a success handler", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A successful result is matched without a success handler", () =>
                _ = Result.Success().Match(null!, _ => 0))
            .Then("The refusal identifies the success handler", (ArgumentNullException refusal) => Assert.Equal("onSuccess", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_Success_InvokesOnlySuccessBranchAndReturnsItsValue()
    {
        var failures = 0;
        int? value = null;

        return Scenario.Define("An asynchronous successful outcome selects only its success handler", _resultWorkflows)
            .When("The outcome is matched asynchronously", async () =>
                value = await Result.Success().MatchAsync(
                    () => Task.FromResult(42),
                    _ =>
                    {
                        failures++;
                        return Task.FromResult(0);
                    }))
            .Then("The success value is returned and failure handling is skipped", () =>
            {
                Assert.Equal(42, value);
                Assert.Equal(0, failures);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task MatchAsync_Failure_InvokesOnlyFailureBranchWithErrorAndReturnsItsValue()
    {
        var successes = 0;
        Error error = new("bad");
        Error? observed = null;
        int? value = null;

        return Scenario.Define("An asynchronous failed outcome selects its failure handler", _resultWorkflows)
            .When("The outcome is matched asynchronously", async () =>
                value = await Result.Failure(error).MatchAsync(
                    () =>
                    {
                        successes++;
                        return Task.FromResult(0);
                    },
                    received =>
                    {
                        observed = received;
                        return Task.FromResult(99);
                    }))
            .Then("The failure handler receives the error and returns its value", () =>
            {
                Assert.Equal(error, observed);
                Assert.Equal(99, value);
                Assert.Equal(0, successes);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task MatchAsync_NullSuccessDelegate_ThrowsWithExactParameter() =>
        Scenario.Define("Asynchronous outcome matching requires a success handler", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A successful result is matched without a success handler", async () =>
                await Result.Success().MatchAsync(null!, _ => Task.FromResult(0)))
            .Then("The refusal identifies the success handler", (ArgumentNullException refusal) => Assert.Equal("onSuccess", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_NullFailureDelegate_ThrowsWithExactParameter() =>
        Scenario.Define("Asynchronous outcome matching requires a failure handler", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A failed result is matched without a failure handler", async () =>
                await Result.Failure(new Error("bad")).MatchAsync(() => Task.FromResult(0), null!))
            .Then("The refusal identifies the failure handler", (ArgumentNullException refusal) => Assert.Equal("onFailure", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_SuccessDelegateReturnsNullTask_ThrowsWithExactParameter() =>
        Scenario.Define("A success handler must return an asynchronous operation", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("The success handler returns no task", async () =>
                await Result.Success().MatchAsync(() => null!, _ => Task.FromResult(0)))
            .Then("The refusal identifies the success handler", (ArgumentNullException refusal) => Assert.Equal("onSuccess", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_FailureDelegateReturnsNullTask_ThrowsWithExactParameter() =>
        Scenario.Define("A failure handler must return an asynchronous operation", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("The failure handler returns no task", async () =>
                await Result.Failure(new Error("bad")).MatchAsync(() => Task.FromResult(0), _ => null!))
            .Then("The refusal identifies the failure handler", (ArgumentNullException refusal) => Assert.Equal("onFailure", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_Uninitialized_ThrowsConsistentUninitializedException() =>
        Scenario.Define("An uninitialized outcome cannot select an asynchronous handler", _resultWorkflows)
            .WhenThrows<InvalidOperationException>("A handler is selected for an uninitialized result", async () =>
                await default(Result).MatchAsync(() => Task.FromResult(1), _ => Task.FromResult(0)))
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) =>
                Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Bind_Failure_DoesNotInvokeDelegate()
    {
        var calls = 0;
        Result? result = null;

        return Scenario.Define("Composing a failed operation skips the next operation", _resultWorkflows)
            .When("A failed result is bound to another operation", () =>
                result = Result.Failure(new Error("bad")).Bind(() =>
                {
                    calls++;
                    return Result.Success();
                }))
            .Then("The next operation is skipped and the original error is retained", () =>
            {
                Assert.Equal(0, calls);
                Assert.Equal(new Error("bad"), result?.Error);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TapError_Failure_InvokesActionAndPreservesResult()
    {
        Error? observed = null;
        var original = Result.Failure(new Error("bad"));
        Result? returned = null;

        return Scenario.Define("Observing a failed operation preserves its error and result", _resultWorkflows)
            .When("The failure is observed", () =>
            {
                returned = original.TapError(error => observed = error);
            })
            .Then("The error is observed without changing the result", () =>
            {
                Assert.Equal(new Error("bad"), observed);
                Assert.Equal(original, returned);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Equality_SameStateAndError_AreEqualWithSameHashCode()
    {
        var left = Result.Failure(new Error("bad"));
        var right = Result.Failure(new Error("bad"));
        bool? areEqual = null;
        bool? hashesMatch = null;

        return Scenario.Define("Equivalent failed outcomes have equal values and hash codes", _resultWorkflows)
            .When("The failures with the same error are compared", () =>
            {
                areEqual = left.Equals(right);
                hashesMatch = left.GetHashCode() == right.GetHashCode();
            })
            .Then("The failures compare equally", () => Assert.True(areEqual))
            .And("The failures have matching hash codes", () => Assert.True(hashesMatch))
            .VerifyAsync();
    }

    [Fact]
    public Task Match_Action_Success_InvokesOnlySuccessAction()
    {
        var successCalled = false;
        var failureCalled = false;
        var result = Result.Success();
        Result? returned = null;

        return Scenario.Define("A successful outcome invokes only its success action", _resultWorkflows)
            .When("The outcome is matched with actions", () =>
            {
                returned = result.Match(() => { successCalled = true; }, _ => failureCalled = true);
            })
            .Then("The success action runs, failure handling is skipped, and the result is preserved", () =>
            {
                Assert.True(successCalled);
                Assert.False(failureCalled);
                Assert.Equal(result, returned);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Match_Action_Failure_InvokesOnlyFailureAction()
    {
        var successCalled = false;
        var failureCalled = false;
        Error error = new("err");
        Error? observed = null;
        var result = Result.Failure(error);
        Result? returned = null;

        return Scenario.Define("A failed outcome invokes only its failure action", _resultWorkflows)
            .When("The outcome is matched with actions", () =>
            {
                returned = result.Match(() => successCalled = true, received =>
                {
                    failureCalled = true;
                    observed = received;
                });
            })
            .Then("The failure action receives the error and the result is preserved", () =>
            {
                Assert.False(successCalled);
                Assert.True(failureCalled);
                Assert.Equal(error, observed);
                Assert.Equal(result, returned);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Match_Action_NullSuccessAction_ThrowsWithExactParameter() =>
        Scenario.Define("Action matching requires a success action", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A successful result is matched without a success action", () =>
                _ = Result.Success().Match(null!, _ => { }))
            .Then("The refusal identifies the success action", (ArgumentNullException refusal) => Assert.Equal("onSuccess", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Match_Action_NullFailureAction_ThrowsWithExactParameter() =>
        Scenario.Define("Action matching requires a failure action", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A result is matched without a failure action", () =>
                _ = Result.Success().Match(() => { }, null!))
            .Then("The refusal identifies the failure action", (ArgumentNullException refusal) => Assert.Equal("onFailure", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_Action_Success_InvokesOnlySuccessAsyncAction()
    {
        var successCalled = false;
        var failureCalled = false;
        var result = Result.Success();
        Result? returned = null;

        return Scenario.Define("An asynchronous successful outcome invokes only its success action", _resultWorkflows)
            .When("The outcome is matched with asynchronous actions", async () =>
            {
                returned = await result.MatchAsync(
                    () =>
                    {
                        successCalled = true;
                        return Task.CompletedTask;
                    },
                    _ =>
                    {
                        failureCalled = true;
                        return Task.CompletedTask;
                    });
            })
            .Then("The success action runs, failure handling is skipped, and the result is preserved", () =>
            {
                Assert.True(successCalled);
                Assert.False(failureCalled);
                Assert.Equal(result, returned);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task MatchAsync_Action_Failure_InvokesOnlyFailureAsyncAction()
    {
        var successCalled = false;
        var failureCalled = false;
        Error error = new("err");
        Error? observed = null;
        var result = Result.Failure(error);
        Result? returned = null;

        return Scenario.Define("An asynchronous failed outcome invokes only its failure action", _resultWorkflows)
            .When("The outcome is matched with asynchronous actions", async () =>
            {
                returned = await result.MatchAsync(
                    () =>
                    {
                        successCalled = true;
                        return Task.CompletedTask;
                    },
                    received =>
                    {
                        failureCalled = true;
                        observed = received;
                        return Task.CompletedTask;
                    });
            })
            .Then("The failure action receives the error and the result is preserved", () =>
            {
                Assert.False(successCalled);
                Assert.True(failureCalled);
                Assert.Equal(error, observed);
                Assert.Equal(result, returned);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task MatchAsync_Action_NullSuccessAction_ThrowsWithExactParameter() =>
        Scenario.Define("Asynchronous action matching requires a success action", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A successful result is matched without a success action", async () =>
                await Result.Success().MatchAsync(null!, _ => Task.CompletedTask))
            .Then("The refusal identifies the success action", (ArgumentNullException refusal) => Assert.Equal("onSuccess", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_Action_NullFailureAction_ThrowsWithExactParameter() =>
        Scenario.Define("Asynchronous action matching requires a failure action", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A result is matched without a failure action", async () =>
                await Result.Success().MatchAsync(() => Task.CompletedTask, null!))
            .Then("The refusal identifies the failure action", (ArgumentNullException refusal) => Assert.Equal("onFailure", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Tap_Success_InvokesActionAndReturnsSelf()
    {
        var tapped = false;
        var result = Result.Success();
        Result? returned = null;

        return Scenario.Define("Observing a successful operation invokes its action and preserves the result", _resultWorkflows)
            .When("The successful outcome is observed", () =>
            {
                returned = result.Tap(() => tapped = true);
            })
            .Then("The action runs and the original result is returned", () =>
            {
                Assert.True(tapped);
                Assert.Equal(result, returned);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_Failure_DoesNotInvokeAction()
    {
        var tapped = false;
        var result = Result.Failure(new Error("err"));
        Result? returned = null;

        return Scenario.Define("Observing a failed operation skips the success action", _resultWorkflows)
            .When("The failed outcome is observed", () =>
            {
                returned = result.Tap(() => tapped = true);
            })
            .Then("The action is skipped and the original result is returned", () =>
            {
                Assert.False(tapped);
                Assert.Equal(result, returned);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_NullAction_ThrowsWithExactParameter()
    {
        Action action = null!;

        return Scenario.Define("Success observation requires an action", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A successful result is observed without an action", () => _ = Result.Success().Tap(action))
            .Then("The refusal identifies the action parameter", (ArgumentNullException refusal) => Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task TapAsync_Success_InvokesAsyncActionAndReturnsSelf()
    {
        var tapped = false;
        var result = Result.Success();
        Result? returned = null;

        return Scenario.Define("Asynchronous observation of success invokes its action and preserves the result", _resultWorkflows)
            .When("The successful outcome is observed asynchronously", async () =>
            {
                returned = await result.TapAsync(() =>
                {
                    tapped = true;
                    return Task.CompletedTask;
                });
            })
            .Then("The action runs and the original result is returned", () =>
            {
                Assert.True(tapped);
                Assert.Equal(result, returned);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TapAsync_Failure_DoesNotInvokeAsyncAction()
    {
        var tapped = false;
        var result = Result.Failure(new Error("err"));
        Result? returned = null;

        return Scenario.Define("Asynchronous observation of failure skips the success action", _resultWorkflows)
            .When("The failed outcome is observed asynchronously", async () =>
            {
                returned = await result.TapAsync(() =>
                {
                    tapped = true;
                    return Task.CompletedTask;
                });
            })
            .Then("The action is skipped and the original result is returned", () =>
            {
                Assert.False(tapped);
                Assert.Equal(result, returned);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TapAsync_NullAction_ThrowsWithExactParameter()
    {
        Func<Task> action = null!;

        return Scenario.Define("Asynchronous success observation requires an action", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A successful result is asynchronously observed without an action", async () =>
                await Result.Success().TapAsync(action))
            .Then("The refusal identifies the action parameter", (ArgumentNullException refusal) => Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task TapError_Success_DoesNotInvokeAction()
    {
        var tapped = false;
        var result = Result.Success();
        Result? returned = null;

        return Scenario.Define("Observing a successful operation skips the failure action", _resultWorkflows)
            .When("The successful outcome is observed for failure", () =>
            {
                returned = result.TapError(_ => tapped = true);
            })
            .Then("The failure action is skipped and the original result is returned", () =>
            {
                Assert.False(tapped);
                Assert.Equal(result, returned);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TapError_NullAction_ThrowsWithExactParameter() =>
        Scenario.Define("Failure observation requires an action", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A failed result is observed without an action", () =>
                _ = Result.Failure(new Error("err")).TapError(null!))
            .Then("The refusal identifies the action parameter", (ArgumentNullException refusal) => Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TapErrorAsync_Failure_InvokesAsyncAction()
    {
        var tapped = false;
        Error error = new("err");
        Error? observed = null;
        var result = Result.Failure(error);
        Result? returned = null;

        return Scenario.Define("Asynchronous observation of failure invokes its action", _resultWorkflows)
            .When("The failed outcome is observed asynchronously", async () =>
            {
                returned = await result.TapErrorAsync(received =>
                {
                    tapped = true;
                    observed = received;
                    return Task.CompletedTask;
                });
            })
            .Then("The action receives the error and the original result is returned", () =>
            {
                Assert.True(tapped);
                Assert.Equal(error, observed);
                Assert.Equal(result, returned);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TapErrorAsync_Success_DoesNotInvokeAsyncAction()
    {
        var tapped = false;
        var result = Result.Success();
        Result? returned = null;

        return Scenario.Define("Asynchronous observation of success skips the failure action", _resultWorkflows)
            .When("The successful outcome is observed asynchronously for failure", async () =>
            {
                returned = await result.TapErrorAsync(_ =>
                {
                    tapped = true;
                    return Task.CompletedTask;
                });
            })
            .Then("The failure action is skipped and the original result is returned", () =>
            {
                Assert.False(tapped);
                Assert.Equal(result, returned);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task TapErrorAsync_NullAction_ThrowsWithExactParameter() =>
        Scenario.Define("Asynchronous failure observation requires an action", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A failed result is observed without an action", async () =>
                await Result.Failure(new Error("err")).TapErrorAsync(null!))
            .Then("The refusal identifies the action parameter", (ArgumentNullException refusal) => Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Bind_ToResultOfT_Success_ReturnsBoundResult()
    {
        Result<int>? bound = null;

        return Scenario.Define("Composing a successful operation returns the next value", _resultWorkflows)
            .When("A successful outcome is bound to a value-producing operation", () =>
                bound = Result.Success().Bind(() => Result<int>.Success(123)))
            .Then("The composed result succeeds with the next value", () =>
            {
                Assert.True(bound?.IsSuccess);
                Assert.Equal(123, bound?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Bind_ToResultOfT_Failure_PropagatesErrorWithoutInvokingBind()
    {
        var invoked = false;
        Result<int>? bound = null;

        return Scenario.Define("Composing a failed operation preserves its error", _resultWorkflows)
            .When("The failed outcome is bound to a value-producing operation", () =>
                bound = Result.Failure(new Error("bad")).Bind(() =>
                {
                    invoked = true;
                    return Result<int>.Success(123);
                }))
            .Then("The next operation is skipped and its error is propagated", () =>
            {
                Assert.False(invoked);
                Assert.True(bound?.IsFailure);
                Assert.Equal(new Error("bad"), bound?.Error);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Bind_ToResultOfT_NullBind_ThrowsWithExactParameter() =>
        Scenario.Define("Composing a result requires the next operation", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A successful result is bound without an operation", () =>
                _ = Result.Success().Bind<int>(null!))
            .Then("The refusal identifies the bind parameter", (ArgumentNullException refusal) => Assert.Equal("bind", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task BindAsync_ToResultOfT_Success_ReturnsBoundResult()
    {
        Result<int>? bound = null;

        return Scenario.Define("Asynchronous composition returns the next successful value", _resultWorkflows)
            .When("A successful outcome is bound to an asynchronous value-producing operation", async () =>
                bound = await Result.Success().BindAsync(() => Task.FromResult(Result<int>.Success(123))))
            .Then("The composed result succeeds with the next value", () =>
            {
                Assert.True(bound?.IsSuccess);
                Assert.Equal(123, bound?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task BindAsync_ToResultOfT_Failure_PropagatesErrorWithoutInvokingBind()
    {
        var invoked = false;
        Result<int>? bound = null;

        return Scenario.Define("Asynchronous composition preserves a failed operation", _resultWorkflows)
            .When("The failed outcome is bound to an asynchronous operation", async () =>
                bound = await Result.Failure(new Error("bad")).BindAsync(() =>
                {
                    invoked = true;
                    return Task.FromResult(Result<int>.Success(123));
                }))
            .Then("The next operation is skipped and its error is propagated", () =>
            {
                Assert.False(invoked);
                Assert.True(bound?.IsFailure);
                Assert.Equal(new Error("bad"), bound?.Error);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task BindAsync_ToResultOfT_NullBind_ThrowsWithExactParameter() =>
        Scenario.Define("Asynchronous composition requires the next operation", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A successful result is bound without an asynchronous operation", async () =>
                await Result.Success().BindAsync<int>(null!))
            .Then("The refusal identifies the bind parameter", (ArgumentNullException refusal) => Assert.Equal("bind", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task OrElse_Success_ReturnsOriginalAndIgnoresFallback()
    {
        Result? result = null;

        return Scenario.Define("A successful operation ignores an eager fallback", _resultWorkflows)
            .When("A successful result is given a fallback result", () =>
                result = Result.Success().OrElse(Result.Failure(new Error("fallback"))))
            .Then("The original success is retained", () => Assert.True(result?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_Failure_ReturnsEagerFallback()
    {
        Result? result = null;

        return Scenario.Define("A failed operation is replaced by its eager fallback", _resultWorkflows)
            .When("A failed result is given a successful fallback", () =>
                result = Result.Failure(new Error("bad")).OrElse(Result.Success()))
            .Then("The fallback success is returned", () => Assert.True(result?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_Func_Success_DoesNotInvokeFallback()
    {
        var calls = 0;
        Result? result = null;

        return Scenario.Define("A successful operation does not create its fallback", _resultWorkflows)
            .When("A successful result is given a fallback factory", () =>
                result = Result.Success().OrElse(() =>
                {
                    calls++;
                    return Result.Failure(new Error("fallback"));
                }))
            .Then("The fallback factory is skipped and success is retained", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(0, calls);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_Func_Failure_InvokesFallback()
    {
        var calls = 0;
        Result? result = null;

        return Scenario.Define("A failed operation creates and returns its fallback", _resultWorkflows)
            .When("A failed result is given a successful fallback factory", () =>
                result = Result.Failure(new Error("bad")).OrElse(() =>
                {
                    calls++;
                    return Result.Success();
                }))
            .Then("The fallback factory runs once and its success is returned", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(1, calls);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_FuncNullFallback_ThrowsWithFallbackParameter() =>
        Scenario.Define("Recovery requires a fallback factory", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A failed result is given no fallback factory", () =>
                _ = Result.Failure(new Error("bad")).OrElse(null!))
            .Then("The refusal identifies the fallback parameter", (ArgumentNullException refusal) => Assert.Equal("fallback", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task OrElseAsync_Success_DoesNotInvokeFallback()
    {
        var calls = 0;
        Result? result = null;

        return Scenario.Define("An asynchronous successful operation skips fallback creation", _resultWorkflows)
            .When("A successful result is given an asynchronous fallback factory", async () =>
                result = await Result.Success().OrElseAsync(() =>
                {
                    calls++;
                    return Task.FromResult(Result.Failure(new Error("fallback")));
                }))
            .Then("The fallback factory is skipped and success is retained", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(0, calls);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task OrElseAsync_Failure_InvokesFallback()
    {
        var calls = 0;
        Result? result = null;

        return Scenario.Define("An asynchronous failed operation recovers through its fallback", _resultWorkflows)
            .When("A failed result is given an asynchronous successful fallback", async () =>
                result = await Result.Failure(new Error("bad")).OrElseAsync(() =>
                {
                    calls++;
                    return Task.FromResult(Result.Success());
                }))
            .Then("The fallback runs once and its success is returned", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(1, calls);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task OrElseAsync_NullFallback_ThrowsWithFallbackParameter() =>
        Scenario.Define("Asynchronous recovery requires a fallback factory", _resultWorkflows)
            .WhenThrows<ArgumentNullException>("A failed result is given no asynchronous fallback", async () =>
                await Result.Failure(new Error("bad")).OrElseAsync(null!))
            .Then("The refusal identifies the fallback parameter", (ArgumentNullException refusal) => Assert.Equal("fallback", refusal.ParamName))
            .VerifyAsync();
}
