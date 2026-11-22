namespace Ahr.Foundation.Tests;

public sealed class TapStepTests
{
    private static readonly Error _failure = Error.FromMessage("failed");

    private static readonly BusinessContext _workflowObservation = BusinessContext
        .Define("workflow-observation", "Workflow observation", "Observe workflow steps while preserving their results and stopping when a workflow fails or has no value.")
        .Capability("observe-workflow-steps", "Run side effects as part of result and option workflows")
        .Outcome("workflow-result-preserved", "A successful value or the original failure remains available after observation")
        .Rule("failed-or-empty-workflow-stops", "Later observation steps do not run after a failure or an empty option")
        .Build();

    private static Result Fail(int _) => Result.Failure(_failure);
    private static Result Pass(int _) => Result.Success();

    [Fact]
    public Task Tap_PayloadFreeStepSucceeds_KeepsValue()
    {
        Result<int>? result = null;

        return Scenario.Define("A successful observed result keeps its value", _workflowObservation)
            .When("A successful result runs a payload-free step", () =>
            {
                result = Result<int>.Success(1).Tap(Pass);
            })
            .Then("The original value remains available", () => Assert.Equal(1, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_PayloadFreeStepFails_ReturnsStepFailure()
    {
        Result<int>? result = null;

        return Scenario.Define("A failed observation returns the step failure", _workflowObservation)
            .When("A successful result runs a failing payload-free step", () =>
            {
                result = Result<int>.Success(1).Tap(Fail);
            })
            .Then("The step failure is returned", () => Assert.Equal(_failure, result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_ExpressionLambdaReturningResult_StopsPipelineOnFailure()
    {
        var reached = false;
        Result<int>? result = null;

        return Scenario.Define("A failed observation stops later result steps", _workflowObservation)
            .When("A failing result step is followed by another step", () =>
            {
                result = Result<int>.Success(1)
                    .Tap(value => Fail(value + 1))
                    .Tap(_ => reached = true);
            })
            .Then("The workflow returns a failure", () => Assert.True(result?.IsFailure))
            .And("The later step is skipped", () => Assert.False(reached))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_ValueStepSucceeds_KeepsOriginalValue()
    {
        Result<int>? result = null;

        return Scenario.Define("A successful value observation keeps the original value", _workflowObservation)
            .When("A successful result runs a value-producing step", () =>
            {
                result = Result<int>.Success(1).Tap(value => Result<string>.Success("other"));
            })
            .Then("The original value remains available", () => Assert.Equal(1, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_ValueStepFails_ReturnsStepFailure()
    {
        Result<int>? result = null;

        return Scenario.Define("A failed value observation returns the step failure", _workflowObservation)
            .When("A successful result runs a failing value-producing step", () =>
            {
                result = Result<int>.Success(1).Tap(_ => Result<string>.Failure(_failure));
            })
            .Then("The step failure is returned", () => Assert.Equal(_failure, result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_AlreadyFailed_SkipsStep()
    {
        var invoked = false;
        var other = Error.FromMessage("other");
        Result<int>? result = null;

        return Scenario.Define("An existing result failure skips observation", _workflowObservation)
            .When("A failed result is passed to an observation step", () =>
            {
                result = Result<int>.Failure(other).Tap(_ =>
                {
                    invoked = true;
                    return Result.Success();
                });
            })
            .Then("The observation step is skipped", () => Assert.False(invoked))
            .And("The original failure is preserved", () => Assert.Equal(other, result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_BlockBodyWithoutReturn_IgnoresStepResult()
    {
        Result<int>? result = null;

        return Scenario.Define("A block-bodied observation ignores an unreturned failure", _workflowObservation)
            .When("A successful result runs a block-bodied step", () =>
            {
                result = Result<int>.Success(1).Tap(value => { _ = Fail(value); });
            })
            .Then("The original result remains successful", () => Assert.True(result?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_NonResultReturningLambda_RemainsAction()
    {
        List<int> values = [];
        Result<int>? result = null;

        return Scenario.Define("An action observation records its side effect", _workflowObservation)
            .When("A successful result runs an action step", () =>
            {
                result = Result<int>.Success(1).Tap(value => values.Add(value + 1));
            })
            .Then("The result remains successful", () => Assert.True(result?.IsSuccess))
            .And("The action records the observed value", () => Assert.Equal([2], values))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_NullPayloadFreeStep_ThrowsWithActionParameter() =>
        Scenario.Define("A missing payload-free observation step is rejected", _workflowObservation)
            .WhenThrows<ArgumentNullException>("A payload-free observer is missing", () =>
                _ = Result<int>.Success(1).Tap(null!))
            .Then("The refusal identifies the action parameter", (ArgumentNullException refusal) =>
                Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Tap_NullValueStep_ThrowsWithActionParameter() =>
        Scenario.Define("A missing value observation step is rejected", _workflowObservation)
            .WhenThrows<ArgumentNullException>("A value observer is missing", () =>
                _ = Result<int>.Success(1).Tap<string>(null!))
            .Then("The refusal identifies the action parameter", (ArgumentNullException refusal) =>
                Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Tap_Uninitialized_Throws() =>
        Scenario.Define("An uninitialized result cannot be observed", _workflowObservation)
            .WhenThrows<InvalidOperationException>("An uninitialized result is passed to an observation step", () =>
                _ = default(Result<int>).Tap(Pass))
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) =>
                Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task TapAsync_PayloadFreeStepSucceeds_KeepsValue()
    {
        Result<int>? result = null;

        return Scenario.Define("An asynchronous payload-free observation keeps its value", _workflowObservation)
            .When("A successful result runs an asynchronous payload-free step", async () =>
            {
                result = await Result<int>.Success(1).TapAsync(value => Task.FromResult(Pass(value)));
            })
            .Then("The original value remains available", () => Assert.Equal(1, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task TapAsync_AsyncLambdaFails_ReturnsStepFailure()
    {
        Result<int>? result = null;

        return Scenario.Define("An asynchronous observation returns its failure", _workflowObservation)
            .When("A successful result runs a failing asynchronous step", async () =>
            {
                result = await Result<int>.Success(1).TapAsync(async value =>
                {
                    await Task.Yield();
                    return Fail(value);
                });
            })
            .Then("The step failure is returned", () => Assert.Equal(_failure, result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task TapAsync_ValueStep_KeepsOriginalValue()
    {
        Result<int>? kept = null;
        Result<int>? failed = null;

        return Scenario.Define("An asynchronous value observation preserves the original outcome", _workflowObservation)
            .When("A successful result runs successful and failing asynchronous value steps", async () =>
            {
                kept = await Result<int>.Success(1).TapAsync(_ => Task.FromResult(Result<string>.Success("other")));
                failed = await Result<int>.Success(1).TapAsync(_ => Task.FromResult(Result<string>.Failure(_failure)));
            })
            .Then("A successful step keeps the original value", () => Assert.Equal(1, kept?.Value))
            .And("A failing step returns its failure", () => Assert.Equal(_failure, failed?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task TapAsync_PlainTask_RemainsSideEffect()
    {
        var invoked = false;
        Result<int>? result = null;

        return Scenario.Define("An asynchronous action observation remains a side effect", _workflowObservation)
            .When("A successful result runs an asynchronous action", async () =>
            {
                result = await Result<int>.Success(1).TapAsync(async _ =>
                {
                    await Task.Yield();
                    invoked = true;
                });
            })
            .Then("The action is invoked", () => Assert.True(invoked))
            .And("The result remains successful", () => Assert.True(result?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task TapAsync_StepReturnsNullTask_Throws() =>
        Scenario.Define("An asynchronous observer must return a task", _workflowObservation)
            .WhenThrows<ArgumentNullException>("An observer returns no task", async () =>
                await Result<int>.Success(1).TapAsync(_ => null!))
            .Then("The refusal identifies the action parameter", (ArgumentNullException refusal) =>
                Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TaskTapAsync_SyncAndAsyncSteps_StopOnFirstFailure()
    {
        var reached = false;
        Result<int>? result = null;

        return Scenario.Define("A task result workflow stops at its first observation failure", _workflowObservation)
            .When("A task result runs synchronous and asynchronous steps", async () =>
            {
                result = await Task.FromResult(Result<int>.Success(1))
                    .TapAsync(value => Pass(value + 1))
                    .TapAsync(value => Task.FromResult(Pass(value)))
                    .TapAsync(_ => Result<string>.Success("other"))
                    .TapAsync(_ => Task.FromResult(Result<string>.Failure(_failure)))
                    .TapAsync(_ => reached = true);
            })
            .Then("The first failure is returned", () => Assert.Equal(_failure, result?.Error))
            .And("Later steps are skipped", () => Assert.False(reached))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskTapAsync_NullTask_ThrowsWithResultTaskParameter()
    {
        Task<Result<int>> resultTask = null!;

        return Scenario.Define("A task result observation requires a result task", _workflowObservation)
            .WhenThrows<ArgumentNullException>("A missing result task is observed", async () =>
                await resultTask.TapAsync(value => Pass(value)))
            .Then("The refusal identifies the result task parameter", (ArgumentNullException refusal) =>
                Assert.Equal("resultTask", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task CustomErrorTap_StepSucceeds_KeepsValue()
    {
        Result<int, int>? result = null;

        return Scenario.Define("A successful custom-error observation keeps its value", _workflowObservation)
            .When("A successful custom-error result runs a value-producing step", () =>
            {
                result = Result<int, int>.Success(1).Tap(_ => Result<string, int>.Success("other"));
            })
            .Then("The original value remains available", () => Assert.Equal(1, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task CustomErrorTap_StepFails_ReturnsStepError()
    {
        Result<int, int>? result = null;

        return Scenario.Define("A failed custom-error observation returns the step error", _workflowObservation)
            .When("A successful custom-error result runs a failing step", () =>
            {
                result = Result<int, int>.Success(1).Tap(_ => Result<string, int>.Failure(404));
            })
            .Then("The step error is returned", () => Assert.Equal(404, result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task CustomErrorTap_AlreadyFailed_SkipsStep()
    {
        var invoked = false;
        Result<int, int>? result = null;

        return Scenario.Define("An existing custom error skips observation", _workflowObservation)
            .When("A failed custom-error result is passed to a step", () =>
            {
                result = Result<int, int>.Failure(500).Tap(_ =>
                {
                    invoked = true;
                    return Result<string, int>.Success("other");
                });
            })
            .Then("The observation step is skipped", () => Assert.False(invoked))
            .And("The original error is preserved", () => Assert.Equal(500, result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task CustomErrorTapAsync_SyncAndAsyncSteps_StopOnFirstFailure()
    {
        var reached = false;
        Result<int, int>? result = null;

        return Scenario.Define("A custom-error task workflow stops at its first observation failure", _workflowObservation)
            .When("A custom-error task runs synchronous and asynchronous steps", async () =>
            {
                result = await Task.FromResult(Result<int, int>.Success(1))
                    .TapAsync(_ => Result<string, int>.Success("other"))
                    .TapAsync(_ => Task.FromResult(Result<string, int>.Failure(404)))
                    .TapAsync(_ => reached = true);
            })
            .Then("The first custom error is returned", () => Assert.Equal(404, result?.Error))
            .And("Later steps are skipped", () => Assert.False(reached))
            .VerifyAsync();
    }

    [Fact]
    public Task CustomErrorTapAsync_InstanceStep_KeepsValue()
    {
        Result<int, int>? result = null;

        return Scenario.Define("An asynchronous custom-error observation keeps its value", _workflowObservation)
            .When("A successful custom-error result runs an asynchronous step", async () =>
            {
                result = await Result<int, int>.Success(1).TapAsync(_ => Task.FromResult(Result<string, int>.Success("other")));
            })
            .Then("The original value remains available", () => Assert.Equal(1, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task ResultTap_StepFails_ReturnsStepFailure()
    {
        var reached = false;
        Result? result = null;

        return Scenario.Define("A failed payload-free observation stops later result steps", _workflowObservation)
            .When("A failing step is followed by another payload-free step", () =>
            {
                result = Result.Success()
                    .Tap(() => Fail(1))
                    .Tap(() => reached = true);
            })
            .Then("The step failure is returned", () => Assert.Equal(_failure, result?.Error))
            .And("The later step is skipped", () => Assert.False(reached))
            .VerifyAsync();
    }

    [Fact]
    public Task ResultTap_StepSucceeds_RemainsSuccess()
    {
        Result? result = null;

        return Scenario.Define("A successful payload-free observation remains successful", _workflowObservation)
            .When("A successful result runs a successful payload-free step", () =>
            {
                result = Result.Success().Tap(() => Pass(1));
            })
            .Then("The result remains successful", () => Assert.True(result?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task ResultTap_ValueStep_FailureStopsAndSuccessDropsValue()
    {
        Result? successful = null;
        Result? failed = null;

        return Scenario.Define("A payload-free value observation keeps success or returns failure", _workflowObservation)
            .When("A successful result runs successful and failing value steps", () =>
            {
                successful = Result.Success().Tap(() => Result<string>.Success("other"));
                failed = Result.Success().Tap(() => Result<string>.Failure(_failure));
            })
            .Then("A successful step leaves the result successful", () => Assert.True(successful?.IsSuccess))
            .And("A failing step returns its failure", () => Assert.Equal(_failure, failed?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task ResultTap_AlreadyFailed_SkipsStep()
    {
        var invoked = false;
        Result? result = null;

        return Scenario.Define("An existing payload-free failure skips observation", _workflowObservation)
            .When("A failed result is passed to a payload-free step", () =>
            {
                result = Result.Failure(_failure).Tap(() =>
                {
                    invoked = true;
                    return Result.Success();
                });
            })
            .Then("The observation step is skipped", () => Assert.False(invoked))
            .And("The result remains failed", () => Assert.True(result?.IsFailure))
            .VerifyAsync();
    }

    [Fact]
    public Task ResultTap_BlockBodyWithoutReturn_IgnoresStepResult()
    {
        Result? result = null;

        return Scenario.Define("A payload-free block observation ignores an unreturned failure", _workflowObservation)
            .When("A successful result runs a block-bodied step", () =>
            {
                result = Result.Success().Tap(() => { _ = Fail(1); });
            })
            .Then("The original result remains successful", () => Assert.True(result?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task ResultTapAsync_Steps_FailureStops()
    {
        Result? payloadFree = null;
        Result? value = null;
        Result? passed = null;

        return Scenario.Define("Asynchronous payload-free observations preserve their outcomes", _workflowObservation)
            .When("A successful result runs failing and successful asynchronous steps", async () =>
            {
                payloadFree = await Result.Success().TapAsync(() => Task.FromResult(Fail(1)));
                value = await Result.Success().TapAsync(() => Task.FromResult(Result<string>.Failure(_failure)));
                passed = await Result.Success().TapAsync(() => Task.FromResult(Result<string>.Success("other")));
            })
            .Then("The payload-free failure is returned", () => Assert.Equal(_failure, payloadFree?.Error))
            .And("The value-step failure is returned", () => Assert.Equal(_failure, value?.Error))
            .And("The successful step leaves the result successful", () => Assert.True(passed?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task ResultTapAsync_StepReturnsNullTask_Throws() =>
        Scenario.Define("A payload-free asynchronous observer must return a task", _workflowObservation)
            .WhenThrows<ArgumentNullException>("An observer returns no task", async () =>
                await Result.Success().TapAsync<string>(() => null!))
            .Then("The refusal identifies the action parameter", (ArgumentNullException refusal) =>
                Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TaskResultTapAsync_SyncAndAsyncSteps_StopOnFirstFailure()
    {
        var reached = false;
        Result? result = null;

        return Scenario.Define("A payload-free task workflow stops at its first observation failure", _workflowObservation)
            .When("A task result runs synchronous and asynchronous steps", async () =>
            {
                result = await Task.FromResult(Result.Success())
                    .TapAsync(() => Pass(1))
                    .TapAsync(() => Task.FromResult(Pass(1)))
                    .TapAsync(() => Result<string>.Success("other"))
                    .TapAsync(() => Task.FromResult(Result<string>.Failure(_failure)))
                    .TapAsync(() => reached = true);
            })
            .Then("The first failure is returned", () => Assert.Equal(_failure, result?.Error))
            .And("Later steps are skipped", () => Assert.False(reached))
            .VerifyAsync();
    }

    [Fact]
    public Task OptionTap_StepReturnsSome_KeepsValue()
    {
        Option<int>? option = null;

        return Scenario.Define("A successful option observation keeps its value", _workflowObservation)
            .When("An option with a value runs a value-producing step", () =>
            {
                option = Option<int>.Some(1).Tap(_ => Option<string>.Some("other"));
            })
            .Then("The original option value remains available", () => Assert.Equal(1, option?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task OptionTap_StepReturnsNone_StopsPipeline()
    {
        var reached = false;
        Option<int>? option = null;

        return Scenario.Define("An empty observation stops later option steps", _workflowObservation)
            .When("A step returning no value is followed by another step", () =>
            {
                option = Option<int>.Some(1)
                    .Tap(_ => Option<string>.None)
                    .Tap(_ => reached = true);
            })
            .Then("The workflow has no value", () => Assert.True(option?.IsNone))
            .And("The later step is skipped", () => Assert.False(reached))
            .VerifyAsync();
    }

    [Fact]
    public Task OptionTap_None_SkipsStep()
    {
        var invoked = false;
        Option<int>? option = null;

        return Scenario.Define("An empty option skips observation", _workflowObservation)
            .When("An empty option is passed to an observation step", () =>
            {
                option = Option<int>.None.Tap(_ =>
                {
                    invoked = true;
                    return Option<string>.Some("other");
                });
            })
            .Then("The observation step is skipped", () => Assert.False(invoked))
            .And("The option remains empty", () => Assert.True(option?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task OptionTapAsync_Steps_NoneStops()
    {
        Option<int>? kept = null;
        Option<int>? stopped = null;

        return Scenario.Define("Asynchronous option observations preserve values or emptiness", _workflowObservation)
            .When("An option runs successful and empty asynchronous steps", async () =>
            {
                kept = await Option<int>.Some(1).TapAsync(_ => Task.FromResult(Option<string>.Some("other")));
                stopped = await Option<int>.Some(1).TapAsync(_ => Task.FromResult(Option<string>.None));
            })
            .Then("A successful step keeps the original value", () => Assert.Equal(1, kept?.Value))
            .And("An empty step returns no value", () => Assert.True(stopped?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskOptionTapAsync_SyncAndAsyncSteps_StopOnNone()
    {
        var reached = false;
        Option<int>? option = null;

        return Scenario.Define("An option task workflow stops at its first empty observation", _workflowObservation)
            .When("An option task runs synchronous and asynchronous steps", async () =>
            {
                option = await Task.FromResult(Option<int>.Some(1))
                    .TapAsync(_ => Option<string>.Some("other"))
                    .TapAsync(_ => Task.FromResult(Option<string>.None))
                    .TapAsync(_ => reached = true);
            })
            .Then("The workflow has no value", () => Assert.True(option?.IsNone))
            .And("Later steps are skipped", () => Assert.False(reached))
            .VerifyAsync();
    }
}
