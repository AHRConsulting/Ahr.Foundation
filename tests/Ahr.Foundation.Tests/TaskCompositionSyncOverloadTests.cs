namespace Ahr.Foundation.Tests;

public sealed class TaskCompositionSyncOverloadTests
{
    private static readonly Error _failure = Error.FromMessage("failed");

    private static readonly BusinessContext _taskComposition = BusinessContext
        .Define("task-functional-composition", "Task functional composition", "Compose Result and Option operations directly from asynchronous sources.")
        .Capability("compose-task-results", "Continue Result pipelines over tasks")
        .Capability("compose-task-options", "Continue Option pipelines over tasks")
        .Outcome("task-pipelines-preserve-outcomes", "Task pipelines preserve success, failure, and absence")
        .Outcome("task-pipelines-short-circuit", "Task pipelines skip synchronous delegates after failure or absence")
        .Rule("compose-without-intermediate-await", "Use task composition extensions without unnecessary intermediate awaits")
        .Build();

    private static Task<Result<int>> SuccessAsync(int value) => Task.FromResult(Result<int>.Success(value));
    private static Task<Result<int>> FailureAsync() => Task.FromResult(Result<int>.Failure(_failure));

    [Fact]
    public Task ResultOfT_MixedSyncAndAsyncPipeline_ComposesWithoutIntermediateAwait()
    {
        string? text = null;

        return Scenario.Define("A task result composes synchronous and asynchronous steps", _taskComposition)
            .When("The value flows through a mixed Result pipeline", async () =>
            {
                text = await SuccessAsync(20)
                    .MapAsync(value => value + 1)
                    .BindAsync(value => Result<int>.Success(value * 2))
                    .EnsureAsync(value => value > 0, _failure)
                    .MapAsync(value => Task.FromResult(value.ToString(System.Globalization.CultureInfo.InvariantCulture)))
                    .MatchAsync(value => value, error => error.Message);
            })
            .Then("The pipeline returns the mapped text", () =>
                Assert.Equal("42", text))
            .VerifyAsync();
    }

    [Fact]
    public Task ResultOfT_AsyncLambda_StillBindsToAsyncOverload()
    {
        Result<int>? result = null;

        return Scenario.Define("A task-returning delegate binds to the asynchronous Map overload", _taskComposition)
            .When("The task result is mapped asynchronously", async () =>
            {
                result = await SuccessAsync(1).MapAsync(value => Task.FromResult(value + 1));
            })
            .Then("The asynchronous overload returns the expected Result value", () =>
            {
                Assert.True(result?.IsSuccess);
                Assert.Equal(2, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task ResultOfT_Failure_SkipsSyncDelegates()
    {
        var invoked = false;
        Result<int>? result = null;

        return Scenario.Define("A failed task result skips synchronous delegates", _taskComposition)
            .When("A failure is passed through a synchronous and asynchronous pipeline", async () =>
            {
                result = await FailureAsync()
                    .MapAsync(value => { invoked = true; return value; })
                    .BindAsync(value => { invoked = true; return Result<int>.Success(value); })
                    .EnsureAsync(_ => { invoked = true; return true; }, _failure)
                    .TapAsync(_ => invoked = true);
            })
            .Then("No delegate runs after the failure", () =>
            {
                Assert.False(invoked);
                Assert.True(result?.IsFailure);
                Assert.Equal(_failure, result?.Error);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task ResultOfT_TapErrorAndMapError_RunOnFailure()
    {
        Error? observed = null;
        Result<int>? result = null;

        return Scenario.Define("A task failure reaches TapError and MapError", _taskComposition)
            .When("The failure is observed and then wrapped", async () =>
            {
                result = await FailureAsync()
                    .TapErrorAsync(error => observed = error)
                    .MapErrorAsync(error => Error.FromMessage($"wrapped: {error.Message}"));
            })
            .Then("TapError sees the original error and MapError returns the wrapped error", () =>
            {
                Assert.Equal(_failure, observed);
                Assert.True(result?.IsFailure);
                Assert.Equal("wrapped: failed", result?.Error.Message);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task ResultOfT_BindToPayloadFree_Composes()
    {
        Result? result = null;

        return Scenario.Define("A task result can bind to a payload-free result", _taskComposition)
            .When("The value-bearing result continues as a payload-free result", async () =>
            {
                result = await SuccessAsync(1).BindAsync(_ => Result.Success());
            })
            .Then("The composed result succeeds", () =>
                Assert.True(result?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task ResultOfT_MatchAsync_AsyncBranches_Composes()
    {
        string? text = null;

        return Scenario.Define("A task result can match with asynchronous branches", _taskComposition)
            .When("The failure is matched with asynchronous handlers", async () =>
            {
                text = await FailureAsync().MatchAsync(_ => Task.FromResult("ok"), error => Task.FromResult(error.Message));
            })
            .Then("The failure handler returns its message", () =>
                Assert.Equal("failed", text))
            .VerifyAsync();
    }

    [Fact]
    public Task ResultOfT_NullSyncDelegate_ThrowsWithParameterName()
    {
        Func<int, int> map = null!;

        return Scenario.Define("A task Result rejects a null synchronous mapper", _taskComposition)
            .WhenThrows<ArgumentNullException>("MapAsync receives a null synchronous mapper", async () =>
                await SuccessAsync(1).MapAsync(map))
            .Then("The mapper argument is identified", (ArgumentNullException refusal) =>
                Assert.Equal("map", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task ResultOfT_NullTask_ThrowsWithResultTaskParameter()
    {
        Task<Result<int>> resultTask = null!;

        return Scenario.Define("Task composition rejects a null Result task", _taskComposition)
            .WhenThrows<ArgumentNullException>("MapAsync receives a null Result task", async () =>
                await resultTask.MapAsync(value => value + 1))
            .Then("The Result task argument is identified", (ArgumentNullException refusal) =>
                Assert.Equal("resultTask", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task Result_SyncOverloads_Compose()
    {
        var tapped = false;
        Result<int>? result = null;

        return Scenario.Define("A payload-free task result composes synchronous overloads", _taskComposition)
            .When("The result is tapped and bound to a value-bearing result", async () =>
            {
                result = await Task.FromResult(Result.Success())
                    .TapAsync(() => tapped = true)
                    .BindAsync(Result.Success)
                    .BindAsync(() => Result<int>.Success(7));
            })
            .Then("The tap runs and the bound result contains its value", () =>
            {
                Assert.True(tapped);
                Assert.True(result?.IsSuccess);
                Assert.Equal(7, result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Result_Failure_TapErrorAndMatch()
    {
        Error? observed = null;
        string? text = null;

        return Scenario.Define("A payload-free task failure reaches TapError and Match", _taskComposition)
            .When("The failure is observed and matched", async () =>
            {
                text = await Task.FromResult(Result.Failure(_failure))
                    .TapErrorAsync(error => observed = error)
                    .MatchAsync(() => "ok", error => error.Message);
            })
            .Then("TapError observes the failure and Match returns its message", () =>
            {
                Assert.Equal(_failure, observed);
                Assert.Equal("failed", text);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Result_MatchAsync_AsyncBranches_Composes()
    {
        string? text = null;

        return Scenario.Define("A payload-free task result matches with asynchronous branches", _taskComposition)
            .When("The success result is matched with asynchronous handlers", async () =>
            {
                text = await Task.FromResult(Result.Success()).MatchAsync(() => Task.FromResult("ok"), error => Task.FromResult(error.Message));
            })
            .Then("The success handler returns its value", () =>
                Assert.Equal("ok", text))
            .VerifyAsync();
    }

    [Fact]
    public Task CustomErrorResult_SyncOverloads_Compose()
    {
        var tapped = 0;
        Result<string, int>? result = null;

        return Scenario.Define("A custom-error task result composes synchronous overloads", _taskComposition)
            .When("The custom-error result passes through the pipeline", async () =>
            {
                result = await Task.FromResult(Result<int, int>.Success(5))
                    .MapAsync(value => value * 2)
                    .BindAsync(value => Result<int, int>.Success(value + 1))
                    .EnsureAsync(value => value > 0, 400)
                    .TapAsync(value => tapped = value)
                    .MapAsync(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            })
            .Then("The tap sees the intermediate value and the pipeline returns its text", () =>
            {
                Assert.Equal(11, tapped);
                Assert.True(result?.IsSuccess);
                Assert.Equal("11", result?.Value);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task CustomErrorResult_Failure_MapErrorTapErrorAndMatch()
    {
        var observed = 0;
        int? code = null;

        return Scenario.Define("A custom-error task failure is observed, mapped, and matched", _taskComposition)
            .When("The failure is observed and mapped", async () =>
            {
                code = await Task.FromResult(Result<int, int>.Failure(404))
                    .TapErrorAsync(error => observed = error)
                    .MapErrorAsync(error => error + 1)
                    .MatchAsync(_ => 0, error => error);
            })
            .Then("The original error is observed and the mapped code is returned", () =>
            {
                Assert.Equal(404, observed);
                Assert.Equal(405, code);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task CustomErrorResult_MatchAsync_AsyncBranches_Composes()
    {
        int? code = null;

        return Scenario.Define("A custom-error task result matches with asynchronous branches", _taskComposition)
            .When("The failure is matched with asynchronous handlers", async () =>
            {
                code = await Task.FromResult(Result<int, int>.Failure(500)).MatchAsync(_ => Task.FromResult(0), Task.FromResult);
            })
            .Then("The failure handler returns the custom error", () =>
                Assert.Equal(500, code))
            .VerifyAsync();
    }

    [Fact]
    public Task Option_SyncOverloads_Compose()
    {
        var tapped = 0;
        string? text = null;

        return Scenario.Define("A task option composes synchronous overloads", _taskComposition)
            .When("The present option is mapped, bound, filtered, tapped, and matched", async () =>
            {
                text = await Task.FromResult(Option<int>.Some(4))
                    .MapAsync(value => value + 1)
                    .BindAsync(value => Option<int>.Some(value * 2))
                    .WhereAsync(value => value > 5)
                    .TapAsync(value => tapped = value)
                    .MatchAsync(value => $"some {value}", () => "none");
            })
            .Then("The tap sees the present value and Match returns its description", () =>
            {
                Assert.Equal(10, tapped);
                Assert.Equal("some 10", text);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Option_None_SkipsSyncDelegatesAndMatchesNone()
    {
        var invoked = false;
        string? text = null;

        return Scenario.Define("An empty task option skips synchronous delegates", _taskComposition)
            .When("The empty option is mapped, filtered, and matched", async () =>
            {
                text = await Task.FromResult(Option<int>.None)
                    .MapAsync(value => { invoked = true; return value; })
                    .WhereAsync(_ => { invoked = true; return true; })
                    .MatchAsync(_ => Task.FromResult("some"), () => Task.FromResult("none"));
            })
            .Then("No value delegate runs and the None handler returns its description", () =>
            {
                Assert.False(invoked);
                Assert.Equal("none", text);
            })
            .VerifyAsync();
    }
}
