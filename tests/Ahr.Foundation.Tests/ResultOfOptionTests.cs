namespace Ahr.Foundation.Tests;

public sealed class ResultOfOptionTests
{
    private static readonly BusinessContext _resultOptionWorkflows = BusinessContext
        .Define("result-backed-options", "Optional values in fallible workflows", "Process found or missing values while preserving success and failure outcomes.")
        .Capability("compose-optional-results", "Match, transform, recover, and observe optional result values")
        .Outcome("result-state-is-preserved", "Result failures and optional-value states survive composition")
        .Rule("only-found-values-continue", "Value-specific operations run only when an optional value is present")
        .Build();

    private static readonly Error _failure = Error.FromMessage("failed");
    private static readonly Error _missing = Error.FromMessage("missing");

    private static Result<Option<int>> Found => Result<Option<int>>.Success(Option<int>.Some(1));
    private static Result<Option<int>> Missing => Result<Option<int>>.Success(Option<int>.None);
    private static Result<Option<int>> Failed => Result<Option<int>>.Failure(_failure);
    private static Result<Option<int>, string> CustomFound => Result<Option<int>, string>.Success(Option<int>.Some(1));
    private static Result<Option<int>, string> CustomMissing => Result<Option<int>, string>.Success(Option<int>.None);
    private static Result<Option<int>, string> CustomFailed => Result<Option<int>, string>.Failure("failed");

    private static string Some(int value) => $"some:{value}";
    private static string None() => "none";
    private static string Fail(Error error) => $"failure:{error.Message}";
    private static string Fail(string error) => $"failure:{error}";

    [Fact]
    public Task Match_SelectsActiveBranch()
    {
        string? found = null;
        string? missing = null;
        string? failed = null;

        return Scenario.Define("A result-backed option selects the handler for its outcome", _resultOptionWorkflows)
            .When("Match receives present, missing, and failed result-backed options", () =>
            {
                found = Found.Match(Some, None, Fail);
                missing = Missing.Match(Some, None, Fail);
                failed = Failed.Match(Some, None, Fail);
            })
            .Then("The found value selects the Some handler", () => Assert.Equal("some:1", found))
            .And("The empty option selects the None handler", () => Assert.Equal("none", missing))
            .And("The failure selects the failure handler", () => Assert.Equal("failure:failed", failed))
            .VerifyAsync();
    }

    [Fact]
    public Task Match_CustomError_SelectsActiveBranch()
    {
        string? found = null;
        string? missing = null;
        string? failed = null;

        return Scenario.Define("Matching a custom-error option selects the handler for its outcome", _resultOptionWorkflows)
            .When("Match receives present, missing, and failed custom-error result-backed options", () =>
            {
                found = CustomFound.Match(Some, None, Fail);
                missing = CustomMissing.Match(Some, None, Fail);
                failed = CustomFailed.Match(Some, None, Fail);
            })
            .Then("The found value selects the Some handler", () => Assert.Equal("some:1", found))
            .And("The empty option selects the None handler", () => Assert.Equal("none", missing))
            .And("The failure selects the failure handler", () => Assert.Equal("failure:failed", failed))
            .VerifyAsync();
    }

    [Fact]
    public Task Match_NullSomeHandler_ThrowsWithExactParameter() =>
        Scenario.Define("Result matching rejects a missing Some handler", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller omits the present-value handler", () =>
                _ = Failed.Match(null!, None, Fail))
            .Then("The refusal identifies the missing Some handler", (ArgumentNullException refusal) =>
                Assert.Equal("onSome", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Match_NullNoneHandler_ThrowsWithExactParameter() =>
        Scenario.Define("Result matching rejects a missing None handler", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller omits the absence handler", () =>
                _ = Failed.Match(Some, null!, Fail))
            .Then("The refusal identifies the missing None handler", (ArgumentNullException refusal) =>
                Assert.Equal("onNone", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Match_NullFailureHandler_ThrowsWithExactParameter() =>
        Scenario.Define("Result matching rejects a missing failure handler", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller omits the failure handler", () =>
                _ = Found.Match(Some, None, null!))
            .Then("The refusal identifies the missing failure handler", (ArgumentNullException refusal) =>
                Assert.Equal("onFailure", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_SelectsActiveBranch()
    {
        string? found = null;
        string? missing = null;
        string? failed = null;

        return Scenario.Define("Asynchronous result matching selects the handler for its outcome", _resultOptionWorkflows)
            .When("MatchAsync processes present, missing, and failed result-backed option branches", async () =>
            {
                found = await Found.MatchAsync(value => Task.FromResult(Some(value)), () => Task.FromResult(None()), error => Task.FromResult(Fail(error)));
                missing = await Missing.MatchAsync(value => Task.FromResult(Some(value)), () => Task.FromResult(None()), error => Task.FromResult(Fail(error)));
                failed = await Failed.MatchAsync(value => Task.FromResult(Some(value)), () => Task.FromResult(None()), error => Task.FromResult(Fail(error)));
            })
            .Then("The found value selects the Some handler", () => Assert.Equal("some:1", found))
            .And("The empty option selects the None handler", () => Assert.Equal("none", missing))
            .And("The failure selects the failure handler", () => Assert.Equal("failure:failed", failed))
            .VerifyAsync();
    }

    [Fact]
    public Task MatchAsync_CustomError_SelectsActiveBranch()
    {
        string? found = null;
        string? missing = null;
        string? failed = null;

        return Scenario.Define("Asynchronous custom-error matching selects the handler for its outcome", _resultOptionWorkflows)
            .When("MatchAsync processes found, missing, and failed custom-error results", async () =>
            {
                found = await CustomFound.MatchAsync(value => Task.FromResult(Some(value)), () => Task.FromResult(None()), error => Task.FromResult(Fail(error)));
                missing = await CustomMissing.MatchAsync(value => Task.FromResult(Some(value)), () => Task.FromResult(None()), error => Task.FromResult(Fail(error)));
                failed = await CustomFailed.MatchAsync(value => Task.FromResult(Some(value)), () => Task.FromResult(None()), error => Task.FromResult(Fail(error)));
            })
            .Then("The found value selects the Some handler", () => Assert.Equal("some:1", found))
            .And("The empty option selects the None handler", () => Assert.Equal("none", missing))
            .And("The failure selects the failure handler", () => Assert.Equal("failure:failed", failed))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_MatchAsync_SynchronousBranches_SelectActiveBranch()
    {
        string? found = null;
        string? missing = null;
        string? failed = null;

        return Scenario.Define("A task-based result selects the active handler with synchronous match branches", _resultOptionWorkflows)
            .When("A result task is matched using synchronous handlers for each optional-value branch", async () =>
            {
                found = await Task.FromResult(Found).MatchAsync(Some, None, Fail);
                missing = await Task.FromResult(CustomMissing).MatchAsync(Some, None, Fail);
                failed = await Task.FromResult(CustomFailed).MatchAsync(Some, None, Fail);
            })
            .Then("The found value selects the Some handler", () => Assert.Equal("some:1", found))
            .And("The empty option selects the None handler", () => Assert.Equal("none", missing))
            .And("The failure selects the failure handler", () => Assert.Equal("failure:failed", failed))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_MatchAsync_AsyncLambdas_SelectActiveBranch()
    {
        string? found = null;
        string? failed = null;

        return Scenario.Define("A task-based result awaits the active asynchronous match handler", _resultOptionWorkflows)
            .When("A result task is matched using asynchronous handlers for present and failed outcomes", async () =>
            {
                found = await Task.FromResult(CustomFound).MatchAsync(
                    async value => { await Task.Yield(); return Some(value); },
                    async () => { await Task.Yield(); return None(); },
                    async error => { await Task.Yield(); return Fail(error); });
                failed = await Task.FromResult(Failed).MatchAsync(
                    async value => { await Task.Yield(); return Some(value); },
                    async () => { await Task.Yield(); return None(); },
                    async error => { await Task.Yield(); return Fail(error); });
            })
            .Then("The Some handler returns the formatted value", () => Assert.Equal("some:1", found))
            .And("The failure handler returns the original failure message", () => Assert.Equal("failure:failed", failed))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_MatchAsync_NullTask_ThrowsWithExactParameter()
    {
        Task<Result<Option<int>>> resultTask = null!;

        return Scenario.Define("Asynchronous result matching requires a result task", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller matches a missing result task", async () => await resultTask.MatchAsync(Some, None, Fail))
            .Then("The refusal identifies the result task", (ArgumentNullException refusal) => Assert.Equal("resultTask", refusal.ParamName))
            .VerifyAsync();
    }

    private sealed class Calls
    {
        public List<string> Log { get; } = [];

        public void OnSome(int value) => Log.Add($"some:{value}");
        public void OnNone() => Log.Add("none");
        public void OnFail(Error error) => Log.Add($"failure:{error.Message}");
        public void OnFail(string error) => Log.Add($"failure:{error}");

        public async Task OnSomeAsync(int value)
        {
            await Task.Yield();
            OnSome(value);
        }

        public async Task OnNoneAsync()
        {
            await Task.Yield();
            OnNone();
        }

        public async Task OnFailAsync(Error error)
        {
            await Task.Yield();
            OnFail(error);
        }

        public async Task OnFailAsync(string error)
        {
            await Task.Yield();
            OnFail(error);
        }
    }

    [Fact]
    public Task MatchActions_StatementLambdas_Compile()
    {
        Calls calls = new();

        return Scenario.Define("Result matching accepts statement-lambda action handlers", _resultOptionWorkflows)
            .When("Block-bodied action handlers match a missing result", () =>
            {
                _ = Missing.Match(
                    onSome: value => calls.OnSome(value),
                    onNone: () => { },
                    onFailure: error => calls.OnFail(error));
            })
            .Then("The other outcome handlers are not called", () => Assert.Empty(calls.Log))
            .VerifyAsync();
    }

    [Fact]
    public Task MatchActions_InvokeOnlyTheActiveBranchOnce()
    {
        Calls found = new();
        Calls missing = new();
        Calls failed = new();

        return Scenario.Define("Result matching invokes only the active action once", _resultOptionWorkflows)
            .When("Action handlers process present, missing, and failed result-backed options", () =>
            {
                _ = Found.Match(found.OnSome, found.OnNone, found.OnFail);
                _ = Missing.Match(missing.OnSome, missing.OnNone, missing.OnFail);
                _ = Failed.Match(failed.OnSome, failed.OnNone, failed.OnFail);
            })
            .Then("Only the Some handler runs for a found value", () => Assert.Equal(["some:1"], found.Log))
            .And("Only the None handler runs for an empty option", () => Assert.Equal(["none"], missing.Log))
            .And("Only the failure handler runs for a failed result", () => Assert.Equal(["failure:failed"], failed.Log))
            .VerifyAsync();
    }

    [Fact]
    public Task MatchActions_CustomError_InvokeOnlyTheActiveBranchOnce()
    {
        Calls found = new();
        Calls missing = new();
        Calls failed = new();

        return Scenario.Define("Custom-error matching invokes only the active action once", _resultOptionWorkflows)
            .When("Action handlers process present, missing, and failed custom-error outcomes", () =>
            {
                _ = CustomFound.Match(value => found.OnSome(value), () => found.OnNone(), error => found.OnFail(error));
                _ = CustomMissing.Match(missing.OnSome, missing.OnNone, missing.OnFail);
                _ = CustomFailed.Match(failed.OnSome, failed.OnNone, failed.OnFail);
            })
            .Then("Only the Some handler runs for a found value", () => Assert.Equal(["some:1"], found.Log))
            .And("Only the None handler runs for an empty option", () => Assert.Equal(["none"], missing.Log))
            .And("Only the failure handler runs for a failed result", () => Assert.Equal(["failure:failed"], failed.Log))
            .VerifyAsync();
    }

    [Fact]
    public Task MatchActions_ReturnOriginalResult()
    {
        Result<Option<int>>? found = null;
        Result<Option<int>>? missing = null;
        Result<Option<int>>? failed = null;
        Result<Option<int>, string>? customFailed = null;

        return Scenario.Define("Action-based matching returns the original result", _resultOptionWorkflows)
            .When("Action-style Match handles each result-backed option outcome", () =>
            {
                found = Found.Match(_ => { }, () => { }, _ => { });
                missing = Missing.Match(_ => { }, () => { }, _ => { });
                failed = Failed.Match(_ => { }, () => { }, _ => { });
                customFailed = CustomFailed.Match(_ => { }, () => { }, _ => { });
            })
            .Then("The found value is preserved", () => Assert.Equal(1, found?.Value.Value))
            .And("The empty option is preserved", () => Assert.True(missing?.Value.IsNone))
            .And("The standard failure is preserved", () => Assert.Equal(_failure, failed?.Error))
            .And("The custom failure is preserved", () => Assert.Equal("failed", customFailed?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task Match_ValueReturningLambdas_StillReturnTheValue()
    {
        string? text = null;
        int? length = null;

        return Scenario.Define("Value-returning match handlers return the selected branch value", _resultOptionWorkflows)
            .When("Value-returning Match handlers process each result-backed option outcome", () =>
            {
                text = Found.Match(value => $"some:{value}", () => "none", error => error.Message);
                length = CustomMissing.Match(value => value, () => 0, error => error.Length);
            })
            .Then("The Some handler can return text", () => Assert.Equal("some:1", text))
            .And("The None handler can return a number", () => Assert.Equal(0, length))
            .VerifyAsync();
    }

    [Fact]
    public Task MatchActions_ThrowingInactiveHandlers_DoNotThrowAndReturnOriginal()
    {
        Result<Option<int>>? unchanged = null;

        return Scenario.Define("Throwing inactive handlers do not affect a successful result match", _resultOptionWorkflows)
            .When("A successful result is matched with action delegates", () =>
            {
                unchanged = Found.Match(_ => { }, () => throw new InvalidOperationException("none"), _ => throw new InvalidOperationException("failure"));
            })
            .Then("The original contained value remains available", () =>
            {
                Assert.True(unchanged?.Value.IsSome);
                Assert.Equal(1, unchanged?.Value.GetValueOrDefault());
            })
            .VerifyAsync();
    }

    [Fact]
    public Task MatchActions_ThrowingAbsenceHandler_PropagatesWhenNone() =>
        Scenario.Define("An exception from the active absence action propagates", _resultOptionWorkflows)
            .WhenThrows<InvalidOperationException>("The absence action throws while matching an empty result", () =>
            _ = Missing.Match(_ => { }, () => throw new InvalidOperationException("none"), _ => { }))
            .Then("The original action exception is preserved", (InvalidOperationException exception) => Assert.Equal("none", exception.Message))
            .VerifyAsync();

    [Fact]
    public Task MatchActions_ThrowingPresentValueHandler_PropagatesWhenSome() =>
        Scenario.Define("An exception from the active present-value action propagates", _resultOptionWorkflows)
            .WhenThrows<InvalidOperationException>("The present-value action throws while matching a found result", () =>
            _ = Found.Match(_ => throw new InvalidOperationException("some"), () => throw new InvalidOperationException("none"), _ => throw new InvalidOperationException("failure")))
            .Then("The active action exception is preserved", (InvalidOperationException exception) => Assert.Equal("some", exception.Message))
            .VerifyAsync();

    [Fact]
    public Task MatchActions_NullSomeAction_ThrowsWithExactParameter() =>
        Scenario.Define("Action matching rejects a missing Some action", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller omits the present-value action", () =>
                _ = Failed.Match(null!, () => { }, _ => { }))
            .Then("The refusal identifies the missing Some action", (ArgumentNullException refusal) =>
                Assert.Equal("onSome", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchActions_NullNoneAction_ThrowsWithExactParameter() =>
        Scenario.Define("Action matching rejects a missing None action", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller omits the absence action", () =>
                _ = Found.Match(_ => { }, null!, _ => { }))
            .Then("The refusal identifies the missing None action", (ArgumentNullException refusal) =>
                Assert.Equal("onNone", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchActions_NullFailureAction_ThrowsWithExactParameter() =>
        Scenario.Define("Action matching rejects a missing failure action", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller omits the failure action", () =>
                _ = CustomFound.Match(_ => { }, () => { }, null!))
            .Then("The refusal identifies the missing failure action", (ArgumentNullException refusal) =>
                Assert.Equal("onFailure", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsyncActions_InvokeOnlyTheActiveBranchOnceAndReturnOriginal()
    {
        Calls found = new();
        Calls missing = new();
        Calls failed = new();
        Result<Option<int>>? foundResult = null;
        Result<Option<int>, string>? missingResult = null;
        Result<Option<int>>? failedResult = null;

        return Scenario.Define("Asynchronous action matching invokes one handler and returns the original result", _resultOptionWorkflows)
            .When("Asynchronous action handlers process the active result-backed option branch", async () =>
            {
                foundResult = await Found.MatchAsync(found.OnSomeAsync, found.OnNoneAsync, found.OnFailAsync);
                missingResult = await CustomMissing.MatchAsync(value => missing.OnSomeAsync(value), () => missing.OnNoneAsync(), error => missing.OnFailAsync(error));
                failedResult = await Failed.MatchAsync(
                    async value => await failed.OnSomeAsync(value),
                    async () => await failed.OnNoneAsync(),
                    async error => await failed.OnFailAsync(error));
            })
            .Then("Only the Some handler runs for a found value", () => Assert.Equal(["some:1"], found.Log))
            .And("Only the None handler runs for an empty option", () => Assert.Equal(["none"], missing.Log))
            .And("Only the failure handler runs for a failed result", () => Assert.Equal(["failure:failed"], failed.Log))
            .And("The found value is preserved", () => Assert.Equal(1, foundResult?.Value.Value))
            .And("The empty option is preserved", () => Assert.True(missingResult?.Value.IsNone))
            .And("The failure is preserved", () => Assert.Equal(_failure, failedResult?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_MatchAsyncActions_InvokeOnlyTheActiveBranchOnce()
    {
        Calls found = new();
        Calls failed = new();
        Result<Option<int>>? foundResult = null;
        Result<Option<int>, string>? failedResult = null;

        return Scenario.Define("A task-based result invokes only the active match action", _resultOptionWorkflows)
            .When("A result task is matched with asynchronous action handlers", async () =>
            {
                foundResult = await Task.FromResult(Found).MatchAsync(found.OnSome, found.OnNone, found.OnFail);
                failedResult = await Task.FromResult(CustomFailed).MatchAsync(value => failed.OnSome(value), () => { }, error => failed.OnFail(error));
            })
            .Then("Only the Some action runs for a found value", () => Assert.Equal(["some:1"], found.Log))
            .And("Only the failure action runs for a failed result", () => Assert.Equal(["failure:failed"], failed.Log))
            .And("The found value is preserved", () => Assert.Equal(1, foundResult?.Value.Value))
            .And("The custom failure is preserved", () => Assert.Equal("failed", failedResult?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_MatchAsyncTaskDelegates_PreferTheNonGenericOverload()
    {
        Calls missing = new();
        Calls failed = new();
        Result<Option<int>>? missingResult = null;
        Result<Option<int>, string>? failedResult = null;

        return Scenario.Define("Task-based matching selects the non-generic overload for task-returning handlers", _resultOptionWorkflows)
            .When("A result task is matched with Task-returning handlers", async () =>
            {
                missingResult = await Task.FromResult(Missing).MatchAsync(value => missing.OnSomeAsync(value), () => missing.OnNoneAsync(), error => missing.OnFailAsync(error));
                failedResult = await Task.FromResult(CustomFailed).MatchAsync(failed.OnSomeAsync, failed.OnNoneAsync, failed.OnFailAsync);
            })
            .Then("Only the None action runs for an empty option", () => Assert.Equal(["none"], missing.Log))
            .And("Only the failure action runs for a failed result", () => Assert.Equal(["failure:failed"], failed.Log))
            .And("The empty option is preserved", () => Assert.True(missingResult?.Value.IsNone))
            .And("The custom failure is preserved", () => Assert.Equal("failed", failedResult?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_MatchAsyncActions_NullTask_ThrowsWithExactParameter()
    {
        Task<Result<Option<int>>> resultTask = null!;

        return Scenario.Define("Asynchronous action matching rejects a null result task", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller matches a null result task", async () =>
                await resultTask.MatchAsync(_ => { }, () => { }, _ => { }))
            .Then("The refusal identifies the missing result task", (ArgumentNullException refusal) =>
                Assert.Equal("resultTask", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_MatchAsyncActions_NullNoneAction_ThrowsWithExactParameter() =>
        Scenario.Define("Asynchronous action matching rejects a missing None action", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller supplies no absence action", async () =>
                await Task.FromResult(Found).MatchAsync(_ => Task.CompletedTask, null!, _ => Task.CompletedTask))
            .Then("The refusal identifies the missing None action", (ArgumentNullException refusal) =>
                Assert.Equal("onNone", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TapSome_RunsOnlyForFoundValueAndReturnsOriginal()
    {
        Calls calls = new();
        Result<Option<int>>? found = null;
        Result<Option<int>>? missing = null;
        Result<Option<int>>? failed = null;
        Result<Option<int>, string>? customFound = null;
        Result<Option<int>, string>? customMissing = null;
        Result<Option<int>, string>? customFailed = null;

        return Scenario.Define("TapSome observes present values and preserves the original result", _resultOptionWorkflows)
            .When("TapSome receives found, missing, and failed result-backed options", () =>
            {
                found = Found.TapSome(calls.OnSome);
                missing = Missing.TapSome(calls.OnSome);
                failed = Failed.TapSome(calls.OnSome);
                customFound = CustomFound.TapSome(calls.OnSome);
                customMissing = CustomMissing.TapSome(calls.OnSome);
                customFailed = CustomFailed.TapSome(calls.OnSome);
            })
            .Then("The standard result keeps its found value", () => Assert.Equal(1, found?.Value.Value))
            .And("The standard empty option is preserved", () => Assert.True(missing?.Value.IsNone))
            .And("The standard failure is preserved", () => Assert.Equal(_failure, failed?.Error))
            .And("The custom-error result keeps its found value", () => Assert.Equal(1, customFound?.Value.Value))
            .And("The custom-error empty option is preserved", () => Assert.True(customMissing?.Value.IsNone))
            .And("The custom failure is preserved", () => Assert.Equal("failed", customFailed?.Error))
            .And("Only found values reach the callback", () => Assert.Equal(["some:1", "some:1"], calls.Log))
            .VerifyAsync();
    }

    [Fact]
    public Task TapSomeAsync_RunsOnlyForFoundValueAndReturnsOriginal()
    {
        Calls calls = new();
        Result<Option<int>>? found = null;
        Result<Option<int>>? missing = null;
        Result<Option<int>, string>? failed = null;

        return Scenario.Define("Asynchronous TapSome observes present values and preserves the original result", _resultOptionWorkflows)
            .When("TapSomeAsync processes a found optional value and preserves other outcomes", async () =>
            {
                found = await Found.TapSomeAsync(calls.OnSomeAsync);
                missing = await Missing.TapSomeAsync(calls.OnSomeAsync);
                failed = await CustomFailed.TapSomeAsync(calls.OnSomeAsync);
            })
            .Then("The found value is preserved", () => Assert.Equal(1, found?.Value.Value))
            .And("The empty option is preserved", () => Assert.True(missing?.Value.IsNone))
            .And("The custom failure is preserved", () => Assert.Equal("failed", failed?.Error))
            .And("Only the found value reaches the callback", () => Assert.Equal(["some:1"], calls.Log))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_TapSomeAsync_RunsSynchronousAndAsynchronousActions()
    {
        Calls calls = new();
        Result<Option<int>>? synchronous = null;
        Result<Option<int>, string>? asynchronous = null;
        Result<Option<int>>? failed = null;

        return Scenario.Define("A task-based result runs synchronous and asynchronous TapSome actions", _resultOptionWorkflows)
            .When("A result task runs synchronous and Task-returning TapSomeAsync callbacks", async () =>
            {
                synchronous = await Task.FromResult(Found).TapSomeAsync(calls.OnSome);
                asynchronous = await Task.FromResult(CustomFound).TapSomeAsync(calls.OnSomeAsync);
                failed = await Task.FromResult(Failed).TapSomeAsync(calls.OnSomeAsync);
            })
            .Then("Both callback forms observe the found value", () => Assert.Equal(["some:1", "some:1"], calls.Log))
            .And("The synchronous callback preserves the value", () => Assert.Equal(1, synchronous?.Value.Value))
            .And("The asynchronous callback preserves the value", () => Assert.Equal(1, asynchronous?.Value.Value))
            .And("The task preserves the original failure", () => Assert.Equal(_failure, failed?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_TapSomeAsync_TaskReturningLambda_IsAwaited()
    {
        Calls calls = new();

        return Scenario.Define("A task-based TapSome awaits its task-returning action before completing", _resultOptionWorkflows)
            .When("TapSomeAsync receives a callback returning Task from a result task", async () =>
            {
                _ = await Task.FromResult(Found).TapSomeAsync(value => calls.OnSomeAsync(value));
            })
            .Then("The callback task completes before the pipeline returns", () => Assert.Equal(["some:1"], calls.Log))
            .VerifyAsync();
    }

    [Fact]
    public Task TapSome_NullSynchronousAction_ThrowsWithExactParameter()
    {
        Action<int> action = null!;

        return Scenario.Define("Synchronous result-option observation rejects a missing action", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller observes a result without an action", () =>
                _ = Failed.TapSome(action))
            .Then("The refusal identifies the missing action", (ArgumentNullException refusal) =>
                Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task TapSome_NullAsynchronousAction_ThrowsWithExactParameter() =>
        Scenario.Define("Asynchronous result-option observation rejects a missing action", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller asynchronously observes a result without an action", async () =>
                await CustomMissing.TapSomeAsync(null!))
            .Then("The refusal identifies the missing action", (ArgumentNullException refusal) =>
                Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TapSome_NullResultTask_ThrowsWithExactParameter()
    {
        Task<Result<Option<int>>> resultTask = null!;

        return Scenario.Define("Asynchronous result-option observation rejects a null result task", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller asynchronously observes a null result task", async () =>
                await resultTask.TapSomeAsync(_ => { }))
            .Then("The refusal identifies the missing result task", (ArgumentNullException refusal) =>
                Assert.Equal("resultTask", refusal.ParamName))
            .VerifyAsync();
    }

    private static Result Save(int value) => value > 0 ? Result.Success() : Result.Failure(_missing);
    private static Result<string> SaveWithId(int value) => value > 100 ? Result<string>.Success($"id:{value}") : Result<string>.Failure(_missing);
    private static Result<string, string> CustomSave(int value) => value > 100 ? Result<string, string>.Success($"id:{value}") : Result<string, string>.Failure("rejected");

    [Fact]
    public Task TapSome_StepSucceeds_KeepsOriginalResult()
    {
        Result<Option<int>>? found = null;
        Result<Option<int>>? missing = null;
        Result<Option<int>>? failed = null;

        return Scenario.Define("A successful TapSome step preserves the original result", _resultOptionWorkflows)
            .When("TapSome receives found, missing, and failed outcomes with a result-returning step", () =>
            {
                found = Found.TapSome(value => Save(value));
                missing = Missing.TapSome(value => Save(value));
                failed = Failed.TapSome(value => Save(value));
            })
            .Then("The successful step preserves the found value", () => Assert.Equal(1, found?.Value.Value))
            .And("The empty option is preserved", () => Assert.True(missing?.Value.IsNone))
            .And("The original failure is preserved", () => Assert.Equal(_failure, failed?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task TapSome_StepFails_StopsWithItsFailure()
    {
        Result<Option<int>>? standard = null;
        Result<Option<int>>? typed = null;
        Result<Option<int>, string>? custom = null;

        return Scenario.Define("A failed TapSome step stops the pipeline with its error", _resultOptionWorkflows)
            .When("TapSome runs a failing result-returning step for a found value", () =>
            {
                standard = Found.TapSome(value => Save(-value));
                typed = Found.TapSome(value => SaveWithId(value));
                custom = CustomFound.TapSome(value => CustomSave(value));
            })
            .Then("The payload-free step returns its failure", () => Assert.Equal(_missing, standard?.Error))
            .And("The value-returning step returns its failure", () => Assert.Equal(_missing, typed?.Error))
            .And("The custom-error step returns its failure", () => Assert.Equal("rejected", custom?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task TapSome_RunsStepOnlyForFoundValue()
    {
        Result<Option<int>>? missing = null;
        Result<Option<int>>? failed = null;
        Result<Option<int>, string>? customFailed = null;
        Result<Option<int>>? found = null;
        var calls = 0;
        var callsBeforeFound = 0;

        return Scenario.Define("TapSome runs its step only when an option contains a value", _resultOptionWorkflows)
            .When("TapSome runs a counting step for missing and failed results, then a found one", () =>
            {
                Result Count(int value)
                {
                    calls++;
                    return Result.Failure(_missing);
                }

                missing = Missing.TapSome(Count);
                failed = Failed.TapSome(Count);
                customFailed = CustomFailed.TapSome(value => CustomSave(value));
                callsBeforeFound = calls;
                found = Found.TapSome(Count);
            })
            .Then("The empty option is preserved", () => Assert.True(missing?.Value.IsNone))
            .And("The standard failure is preserved", () => Assert.Equal(_failure, failed?.Error))
            .And("The custom failure is preserved", () => Assert.Equal("failed", customFailed?.Error))
            .And("No step runs before a value is found", () => Assert.Equal(0, callsBeforeFound))
            .And("The found branch returns the step failure", () => Assert.Equal(_missing, found?.Error))
            .And("The step runs once for the found value", () => Assert.Equal(1, calls))
            .VerifyAsync();
    }

    [Fact]
    public Task TapSome_ResultTry_RestoresOnlyWhenFound()
    {
        List<int>? restored = null;
        Result<Option<int>>? found = null;
        Result<Option<int>>? failed = null;

        return Scenario.Define("TapSome preserves the value on success and returns failure when its side effect throws", _resultOptionWorkflows)
            .When("TapSome wraps Result.Try around the found optional value", () =>
            {
                restored = [];

                found = Found.TapSome(value => Result.Try(() => restored.Add(value)));
                failed = Found.TapSome(value => Result.Try(() => throw new InvalidOperationException("corrupt")));
            })
            .Then("The step observes only the found value", () => Assert.Equal([1], restored))
            .And("The successful step preserves the found value", () => Assert.Equal(1, found?.Value.Value))
            .And("The thrown exception becomes a failure", () => Assert.True(failed?.IsFailure))
            .VerifyAsync();
    }

    [Fact]
    public Task TapSomeAsync_StepFails_StopsWithItsFailure()
    {
        Result<Option<int>>? succeeded = null;
        Result<Option<int>>? failed = null;
        Result<Option<int>>? typedFailure = null;
        Result<Option<int>, string>? customFailure = null;
        Result<Option<int>, string>? customMissing = null;

        return Scenario.Define("An asynchronous TapSome step preserves success and propagates its failure", _resultOptionWorkflows)
            .When("TapSomeAsync runs succeeding and failing result-returning steps", async () =>
            {
                succeeded = await Found.TapSomeAsync(value => Task.FromResult(Save(value)));
                failed = await Found.TapSomeAsync(value => Task.FromResult(Save(-value)));
                typedFailure = await Found.TapSomeAsync(value => Task.FromResult(SaveWithId(value)));
                customFailure = await CustomFound.TapSomeAsync(async value => { await Task.Yield(); return CustomSave(value); });
                customMissing = await CustomMissing.TapSomeAsync(value => Task.FromResult(CustomSave(value)));
            })
            .Then("The successful step preserves the found value", () => Assert.Equal(1, succeeded?.Value.Value))
            .And("The payload-free step returns its failure", () => Assert.Equal(_missing, failed?.Error))
            .And("The value-returning step returns its failure", () => Assert.Equal(_missing, typedFailure?.Error))
            .And("The custom-error step returns its failure", () => Assert.Equal("rejected", customFailure?.Error))
            .And("The custom-error empty option is preserved", () => Assert.True(customMissing?.Value.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskReceiver_TapSomeAsync_StepFails_StopsWithItsFailure()
    {
        Result<Option<int>>? standardFailure = null;
        Result<Option<int>>? typedFailure = null;
        Result<Option<int>>? standardTaskFailure = null;
        Result<Option<int>>? typedTaskFailure = null;
        Result<Option<int>, string>? customFailure = null;
        Result<Option<int>, string>? customTaskFailure = null;
        Result<Option<int>>? succeeded = null;

        return Scenario.Define("A task-based TapSome preserves success and propagates result-returning step failures", _resultOptionWorkflows)
            .When("A result task runs result-returning TapSomeAsync callbacks", async () =>
            {
                standardFailure = await Task.FromResult(Found).TapSomeAsync(value => Save(-value));
                typedFailure = await Task.FromResult(Found).TapSomeAsync(value => SaveWithId(value));
                standardTaskFailure = await Task.FromResult(Found).TapSomeAsync(value => Task.FromResult(Save(-value)));
                typedTaskFailure = await Task.FromResult(Found).TapSomeAsync(value => Task.FromResult(SaveWithId(value)));
                customFailure = await Task.FromResult(CustomFound).TapSomeAsync(value => CustomSave(value));
                customTaskFailure = await Task.FromResult(CustomFound).TapSomeAsync(value => Task.FromResult(CustomSave(value)));
                succeeded = await Task.FromResult(Found).TapSomeAsync(value => Save(value));
            })
            .Then("The asynchronous payload-free step returns its failure", () => Assert.Equal(_missing, standardFailure?.Error))
            .And("The asynchronous value-returning step returns its failure", () => Assert.Equal(_missing, typedFailure?.Error))
            .And("The task receiver preserves the payload-free step failure", () => Assert.Equal(_missing, standardTaskFailure?.Error))
            .And("The task receiver preserves the value-returning step failure", () => Assert.Equal(_missing, typedTaskFailure?.Error))
            .And("The custom-error step returns its failure", () => Assert.Equal("rejected", customFailure?.Error))
            .And("The task receiver preserves the custom step failure", () => Assert.Equal("rejected", customTaskFailure?.Error))
            .And("The successful step preserves the found value", () => Assert.Equal(1, succeeded?.Value.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task TapSome_NullSynchronousStep_ThrowsWithExactParameter() =>
        Scenario.Define("Synchronous result-option observation rejects a missing step", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller observes a result synchronously without a step", () =>
                _ = Failed.TapSome(null!))
            .Then("The refusal identifies the missing action", (ArgumentNullException refusal) =>
                Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TapSome_NullAsynchronousStep_ThrowsWithExactParameter()
    {
        Func<int, Task<Result<string, string>>> action = null!;

        return Scenario.Define("Asynchronous result-option observation rejects a missing step", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller asynchronously observes a result without a step", async () =>
                await CustomMissing.TapSomeAsync(action))
            .Then("The refusal identifies the missing action", (ArgumentNullException refusal) =>
                Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task SomeOr_UnwrapsSomeAndUsesFallbackForNone()
    {
        Result<int>? found = null;
        Result<int>? missing = null;
        Result<int>? failed = null;
        Result<int, string>? customFound = null;
        Result<int, string>? customMissing = null;
        Result<int, string>? customFailed = null;

        return Scenario.Define("SomeOr returns a present value and uses a fallback for None", _resultOptionWorkflows)
            .When("SomeOr receives found, missing, and failed outcomes", () =>
            {
                found = Found.SomeOr(9);
                missing = Missing.SomeOr(9);
                failed = Failed.SomeOr(9);
                customFound = CustomFound.SomeOr(9);
                customMissing = CustomMissing.SomeOr(9);
                customFailed = CustomFailed.SomeOr(9);
            })
            .Then("The standard result returns the found value", () => Assert.Equal(1, found?.Value))
            .And("The standard result uses the fallback for absence", () => Assert.Equal(9, missing?.Value))
            .And("The standard failure is preserved", () => Assert.Equal(_failure, failed?.Error))
            .And("The custom-error result returns the found value", () => Assert.Equal(1, customFound?.Value))
            .And("The custom-error result uses the fallback for absence", () => Assert.Equal(9, customMissing?.Value))
            .And("The custom failure is preserved", () => Assert.Equal("failed", customFailed?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task SomeOrAsync_ContinuesPipeline()
    {
        Result<int>? standard = null;
        Result<int, string>? custom = null;

        return Scenario.Define("SomeOrAsync supplies a fallback for a missing value and preserves existing failures", _resultOptionWorkflows)
            .When("SomeOrAsync supplies the missing value before the next task-composed step", async () =>
            {
                standard = await Task.FromResult(Missing).SomeOrAsync(9).MapAsync(value => value + 1);
                custom = await Task.FromResult(CustomFailed).SomeOrAsync(9).MapAsync(value => value + 1);
            })
            .Then("The fallback value advances through the pipeline", () => Assert.Equal(10, standard?.Value))
            .And("The existing custom failure is preserved", () => Assert.Equal("failed", custom?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task SomeOr_NullFallback_ThrowsWithExactParameter() =>
        Scenario.Define("Optional-result unwrapping rejects a missing fallback", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller supplies a null fallback for a missing option", () =>
                _ = Result<Option<string>>.Success(Option<string>.None).SomeOr(null!))
            .Then("The refusal identifies the missing fallback", (ArgumentNullException refusal) =>
                Assert.Equal("fallback", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task SomeOr_NullResultTask_ThrowsWithExactParameter()
    {
        Task<Result<Option<string>, string>> resultTask = null!;

        return Scenario.Define("Optional-result unwrapping rejects a null result task", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller unwraps a null result task", async () =>
                await resultTask.SomeOrAsync("fallback"))
            .Then("The refusal identifies the missing result task", (ArgumentNullException refusal) =>
                Assert.Equal("resultTask", refusal.ParamName))
            .VerifyAsync();
    }

    private static Result<Option<int>> Fallback() => Result<Option<int>>.Success(Option<int>.Some(9));
    private static Result<Option<int>, string> CustomFallback() => Result<Option<int>, string>.Success(Option<int>.Some(9));

    [Fact]
    public Task BindNone_TriesFallbackOnlyForNone()
    {
        Result<Option<int>>? found = null;
        Result<Option<int>>? missing = null;
        Result<Option<int>>? failed = null;
        Result<Option<int>, string>? customFound = null;
        Result<Option<int>, string>? customMissing = null;
        Result<Option<int>, string>? customFailed = null;

        return Scenario.Define("BindNone tries its fallback only for None", _resultOptionWorkflows)
            .When("BindNone receives found, missing, and failed results", () =>
            {
                found = Found.BindNone(Fallback);
                missing = Missing.BindNone(Fallback);
                failed = Failed.BindNone(Fallback);
                customFound = CustomFound.BindNone(CustomFallback);
                customMissing = CustomMissing.BindNone(CustomFallback);
                customFailed = CustomFailed.BindNone(CustomFallback);
            })
            .Then("The standard result retains the found value", () => Assert.Equal(1, found?.Value.Value))
            .And("The alternate lookup supplies the missing standard value", () => Assert.Equal(9, missing?.Value.Value))
            .And("The standard failure is preserved", () => Assert.Equal(_failure, failed?.Error))
            .And("The custom-error result retains the found value", () => Assert.Equal(1, customFound?.Value.Value))
            .And("The alternate lookup supplies the missing custom-error value", () => Assert.Equal(9, customMissing?.Value.Value))
            .And("The custom failure is preserved", () => Assert.Equal("failed", customFailed?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task BindNone_InvokesFallbackOnlyForNoneAndKeepsItsOutcome()
    {
        var calls = 0;
        var callsBeforeFallback = 0;
        Result<Option<int>>? missing = null;
        Result<Option<int>>? missingNone = null;

        return Scenario.Define("BindNone uses the fallback outcome only for None", _resultOptionWorkflows)
            .When("BindNone runs a counting fallback for found, missing, and failed results", () =>
            {
                Result<Option<int>> Count()
                {
                    calls++;
                    return Result<Option<int>>.Failure(_missing);
                }

                _ = Found.BindNone(Count);
                _ = Failed.BindNone(Count);
                callsBeforeFallback = calls;
                missing = Missing.BindNone(Count);
                missingNone = Missing.BindNone(() => Missing);
            })
            .Then("The fallback does not run before absence", () => Assert.Equal(0, callsBeforeFallback))
            .And("The fallback failure is preserved", () => Assert.Equal(_missing, missing?.Error))
            .And("The tracked fallback runs once", () => Assert.Equal(1, calls))
            .And("An empty fallback leaves the option empty", () => Assert.True(missingNone?.Value.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task BindNoneAsync_TriesFallbackOnlyForNone()
    {
        Result<Option<int>>? found = null;
        Result<Option<int>>? missing = null;
        Result<Option<int>, string>? customFailed = null;
        Result<Option<int>>? taskMissing = null;
        Result<Option<int>, string>? customTaskMissing = null;
        Result<Option<int>, string>? customTaskFound = null;

        return Scenario.Define("Asynchronous BindNone tries its fallback only for None", _resultOptionWorkflows)
            .When("BindNoneAsync receives found, missing, and failed results", async () =>
            {
                found = await Found.BindNoneAsync(() => Task.FromResult(Fallback()));
                missing = await Missing.BindNoneAsync(() => Task.FromResult(Fallback()));
                customFailed = await CustomFailed.BindNoneAsync(() => Task.FromResult(CustomFallback()));
                taskMissing = await Task.FromResult(Missing).BindNoneAsync(Fallback);
                customTaskMissing = await Task.FromResult(CustomMissing).BindNoneAsync(async () => { await Task.Yield(); return CustomFallback(); });
                customTaskFound = await Task.FromResult(CustomFound).BindNoneAsync(() => Task.FromResult(CustomFallback()));
            })
            .Then("The found value is preserved", () => Assert.Equal(1, found?.Value.Value))
            .And("The alternate lookup supplies the missing value", () => Assert.Equal(9, missing?.Value.Value))
            .And("The custom failure is preserved", () => Assert.Equal("failed", customFailed?.Error))
            .And("The task receiver gets the alternate value", () => Assert.Equal(9, taskMissing?.Value.Value))
            .And("The custom-error task receiver gets the alternate value", () => Assert.Equal(9, customTaskMissing?.Value.Value))
            .And("The custom-error task receiver retains the found value", () => Assert.Equal(1, customTaskFound?.Value.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task BindNone_NullSynchronousBinder_ThrowsWithExactParameter() =>
        Scenario.Define("Missing-option recovery rejects a missing synchronous binder", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller tries synchronous recovery without a fallback function", () =>
                _ = Found.BindNone(null!))
            .Then("The refusal identifies the missing binder", (ArgumentNullException refusal) =>
                Assert.Equal("bind", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task BindNone_NullAsynchronousBinder_ThrowsWithExactParameter() =>
        Scenario.Define("Missing-option recovery rejects a missing asynchronous binder", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller tries asynchronous recovery without a fallback function", async () =>
                await CustomFound.BindNoneAsync(null!))
            .Then("The refusal identifies the missing binder", (ArgumentNullException refusal) =>
                Assert.Equal("bind", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task BindNone_NullResultTask_ThrowsWithExactParameter()
    {
        Task<Result<Option<int>>> resultTask = null!;

        return Scenario.Define("Missing-option recovery rejects a null result task", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller asynchronously recovers from a null result task", async () =>
                await resultTask.BindNoneAsync(Fallback))
            .Then("The refusal identifies the missing result task", (ArgumentNullException refusal) =>
                Assert.Equal("resultTask", refusal.ParamName))
            .VerifyAsync();
    }


    [Fact]
    public Task EnsureSome_UnwrapsSomeAndFailsNone()
    {
        Result<int>? found = null;
        Result<int>? missing = null;

        return Scenario.Define("EnsureSome unwraps Some and fails for None", _resultOptionWorkflows)
            .When("EnsureSome receives found and missing outcomes", () =>
            {
                found = Found.EnsureSome(_missing);
                missing = Missing.EnsureSome(_missing);
            })
            .Then("The found value is returned", () => Assert.Equal(1, found?.Value))
            .And("The missing value becomes the supplied failure", () => Assert.Equal(_missing, missing?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task EnsureSome_AlreadyFailed_KeepsOriginalError()
    {
        Result<int>? failed = null;

        return Scenario.Define("EnsureSome preserves an existing failure", _resultOptionWorkflows)
            .When("EnsureSome receives only a failed outcome", () =>
            {
                failed = Failed.EnsureSome(_missing);
            })
            .Then("An existing failure keeps its original error", () => Assert.Equal(_failure, failed?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task EnsureSome_CustomError_UnwrapsSomeAndFailsNone()
    {
        Result<int, string>? found = null;
        Result<int, string>? missing = null;
        Result<int, string>? failed = null;

        return Scenario.Define("EnsureSome handles Some and None with a custom error", _resultOptionWorkflows)
            .When("EnsureSome receives found, missing, and failed outcomes", () =>
            {
                found = CustomFound.EnsureSome("missing");
                missing = CustomMissing.EnsureSome("missing");
                failed = CustomFailed.EnsureSome("missing");
            })
            .Then("The found value is returned", () => Assert.Equal(1, found?.Value))
            .And("The missing value becomes the supplied custom failure", () => Assert.Equal("missing", missing?.Error))
            .And("The existing custom failure is preserved", () => Assert.Equal("failed", failed?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task EnsureSome_InvalidStandardError_ThrowsWithExactParameter() =>
        Scenario.Define("EnsureSome rejects an invalid error on a failed result", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("EnsureSome is called on a failed result with a default error", () =>
                _ = Failed.EnsureSome(default))
            .Then("The refusal identifies the invalid error", (ArgumentNullException refusal) =>
                Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task EnsureSome_NullCustomError_ThrowsWithExactParameter() =>
        Scenario.Define("EnsureSome rejects a null custom error on a failed result", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A failed custom-error result is ensured with a null error", () =>
                _ = CustomFailed.EnsureSome(null!))
            .Then("The refusal identifies the missing error", (ArgumentNullException refusal) =>
                Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task EnsureSomeAsync_ContinuesPipeline()
    {
        Result<int>? standard = null;
        Result<int, string>? custom = null;

        return Scenario.Define("Asynchronous EnsureSome continues with Some and fails for None", _resultOptionWorkflows)
            .When("EnsureSomeAsync validates the found value before the next task-composed step", async () =>
            {
                standard = await Task.FromResult(Found).EnsureSomeAsync(_missing).MapAsync(value => value + 1);
                custom = await Task.FromResult(CustomMissing).EnsureSomeAsync("missing").MapAsync(value => value + 1);
            })
            .Then("The found value advances through the pipeline", () => Assert.Equal(2, standard?.Value))
            .And("The missing value becomes the supplied custom failure", () => Assert.Equal("missing", custom?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task MapSome_MapsFoundValueAndPassesOthersThrough()
    {
        Result<Option<int>>? found = null;
        Result<Option<int>>? missing = null;
        Result<Option<int>>? failed = null;
        Result<Option<int>, string>? customFound = null;
        Result<Option<int>, string>? customMissing = null;
        Result<Option<int>, string>? customFailed = null;

        return Scenario.Define("MapSome transforms Some and preserves other outcomes", _resultOptionWorkflows)
            .When("MapSome receives found, missing, and failed outcomes", () =>
            {
                found = Found.MapSome(value => value + 1);
                missing = Missing.MapSome(value => value + 1);
                failed = Failed.MapSome(value => value + 1);
                customFound = CustomFound.MapSome(value => value + 1);
                customMissing = CustomMissing.MapSome(value => value + 1);
                customFailed = CustomFailed.MapSome(value => value + 1);
            })
            .Then("The standard found value is mapped", () => Assert.Equal(2, found?.Value.Value))
            .And("The standard empty option is preserved", () => Assert.True(missing?.Value.IsNone))
            .And("The standard failure is preserved", () => Assert.Equal(_failure, failed?.Error))
            .And("The custom-error found value is mapped", () => Assert.Equal(2, customFound?.Value.Value))
            .And("The custom-error empty option is preserved", () => Assert.True(customMissing?.Value.IsNone))
            .And("The custom failure is preserved", () => Assert.Equal("failed", customFailed?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task MapSome_InvokesMappingOnlyForFoundValue()
    {
        var calls = 0;
        Result<Option<int>>? missing = null;
        Result<Option<int>>? failed = null;
        Result<Option<int>>? found = null;
        var callsBeforeFound = 0;

        return Scenario.Define("MapSome skips its mapper when no value is present", _resultOptionWorkflows)
            .When("MapSome runs a counting mapper for missing and failed results, then a found one", () =>
            {
                int Count(int value)
                {
                    calls++;
                    return value;
                }

                missing = Missing.MapSome(Count);
                failed = Failed.MapSome(Count);
                callsBeforeFound = calls;
                found = Found.MapSome(Count);
            })
            .Then("The empty option is preserved", () => Assert.True(missing?.Value.IsNone))
            .And("The original failure is preserved", () => Assert.Equal(_failure, failed?.Error))
            .And("The mapper is skipped before a value is found", () => Assert.Equal(0, callsBeforeFound))
            .And("The mapper runs once", () => Assert.Equal(1, calls))
            .And("The found value is passed to the mapper", () => Assert.Equal(1, found?.Value.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task MapSome_NullMappingResult_Throws() =>
        Scenario.Define("Mapping a present value must produce a valid option", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A present-value mapper returns null", () => _ = Found.MapSome<int, string>(_ => null!))
            .Then("The refusal identifies the missing mapped value", (ArgumentNullException refusal) => Assert.Equal("value", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MapSomeAsync_MapsFoundValueAndPassesOthersThrough()
    {
        Result<Option<int>>? found = null;
        Result<Option<int>, string>? missing = null;
        Result<Option<int>, string>? failed = null;
        Result<Option<int>>? taskFound = null;
        Result<Option<int>, string>? customTaskFound = null;

        return Scenario.Define("Asynchronous MapSome transforms Some and preserves other outcomes", _resultOptionWorkflows)
            .When("MapSomeAsync receives found, missing, and failed outcomes", async () =>
            {
                found = await Found.MapSomeAsync(value => Task.FromResult(value + 1));
                missing = await CustomMissing.MapSomeAsync(value => Task.FromResult(value + 1));
                failed = await CustomFailed.MapSomeAsync(value => Task.FromResult(value + 1));
                taskFound = await Task.FromResult(Found).MapSomeAsync(value => value + 1);
                customTaskFound = await Task.FromResult(CustomFound).MapSomeAsync(async value => { await Task.Yield(); return value + 1; });
            })
            .Then("The found value is mapped", () => Assert.Equal(2, found?.Value.Value))
            .And("The empty option is preserved", () => Assert.True(missing?.Value.IsNone))
            .And("The custom failure is preserved", () => Assert.Equal("failed", failed?.Error))
            .And("The task receiver maps the found value", () => Assert.Equal(2, taskFound?.Value.Value))
            .And("The custom-error task receiver maps the found value", () => Assert.Equal(2, customTaskFound?.Value.Value))
            .VerifyAsync();
    }

    private static Result<Option<string>> Lookup(int value) => Result<Option<string>>.Success(Option<string>.Some($"order:{value}"));
    private static Result<Option<string>, string> CustomLookup(int value) => Result<Option<string>, string>.Success(Option<string>.Some($"order:{value}"));

    [Fact]
    public Task BindSome_ContinuesFoundValueAndPassesOthersThrough()
    {
        Result<Option<string>>? found = null;
        Result<Option<string>>? missing = null;
        Result<Option<string>>? failed = null;
        Result<Option<string>, string>? customFound = null;
        Result<Option<string>, string>? customMissing = null;
        Result<Option<string>, string>? customFailed = null;

        return Scenario.Define("BindSome continues from Some and preserves other outcomes", _resultOptionWorkflows)
            .When("BindSome receives found, missing, and failed outcomes", () =>
            {
                found = Found.BindSome(Lookup);
                missing = Missing.BindSome(Lookup);
                failed = Failed.BindSome(Lookup);
                customFound = CustomFound.BindSome(CustomLookup);
                customMissing = CustomMissing.BindSome(CustomLookup);
                customFailed = CustomFailed.BindSome(CustomLookup);
            })
            .Then("The binder supplies the standard found value", () => Assert.Equal("order:1", found?.Value.Value))
            .And("The standard empty option is preserved", () => Assert.True(missing?.Value.IsNone))
            .And("The standard failure is preserved", () => Assert.Equal(_failure, failed?.Error))
            .And("The binder supplies the custom-error found value", () => Assert.Equal("order:1", customFound?.Value.Value))
            .And("The custom-error empty option is preserved", () => Assert.True(customMissing?.Value.IsNone))
            .And("The custom failure is preserved", () => Assert.Equal("failed", customFailed?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task BindSome_InnerOutcomeIsKept()
    {
        Result<Option<string>>? missing = null;
        Result<Option<string>>? failed = null;

        return Scenario.Define("BindSome preserves the outcome returned by its binder", _resultOptionWorkflows)
            .When("The binder returns an empty option or a failure for a found value", () =>
            {
                missing = Found.BindSome(_ => Result<Option<string>>.Success(Option<string>.None));
                failed = Found.BindSome(_ => Result<Option<string>>.Failure(_missing));
            })
            .Then("The inner empty option is preserved", () => Assert.True(missing?.Value.IsNone))
            .And("The inner failure is preserved", () => Assert.Equal(_missing, failed?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task BindSomeAsync_ContinuesFoundValueAndPassesOthersThrough()
    {
        Result<Option<string>>? found = null;
        Result<Option<string>>? missing = null;
        Result<Option<string>, string>? customFailed = null;
        Result<Option<string>>? taskFound = null;
        Result<Option<string>, string>? customTaskMissing = null;

        return Scenario.Define("Asynchronous BindSome continues from Some and preserves other outcomes", _resultOptionWorkflows)
            .When("BindSomeAsync receives found, missing, and failed outcomes", async () =>
            {
                found = await Found.BindSomeAsync(value => Task.FromResult(Lookup(value)));
                missing = await Missing.BindSomeAsync(value => Task.FromResult(Lookup(value)));
                customFailed = await CustomFailed.BindSomeAsync(value => Task.FromResult(CustomLookup(value)));
                taskFound = await Task.FromResult(Found).BindSomeAsync(Lookup);
                customTaskMissing = await Task.FromResult(CustomMissing).BindSomeAsync(value => Task.FromResult(CustomLookup(value)));
            })
            .Then("The asynchronous binder provides the found value", () => Assert.Equal("order:1", found?.Value.Value))
            .And("The empty option is preserved", () => Assert.True(missing?.Value.IsNone))
            .And("The custom failure is preserved", () => Assert.Equal("failed", customFailed?.Error))
            .And("The task receiver binds the found value", () => Assert.Equal("order:1", taskFound?.Value.Value))
            .And("The custom-error task receiver preserves the empty option", () => Assert.True(customTaskMissing?.Value.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task MapSome_NullMapper_ThrowsWithExactParameter() =>
        Scenario.Define("MapSome rejects a missing mapper", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller maps a failed result without a mapper", () =>
                _ = Failed.MapSome<int, int>(null!))
            .Then("The refusal identifies the missing mapper", (ArgumentNullException refusal) =>
                Assert.Equal("map", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task BindSome_NullBinder_ThrowsWithExactParameter() =>
        Scenario.Define("BindSome rejects a missing binder", _resultOptionWorkflows)
            .WhenThrows<ArgumentNullException>("A caller binds a failed custom-error result without a continuation", () =>
                _ = CustomFailed.BindSome<int, string, int>(null!))
            .Then("The refusal identifies the missing binder", (ArgumentNullException refusal) =>
                Assert.Equal("bind", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Chain_BindSomeMapSomeMatch_ReadsAsOnePipeline()
    {
        string? text = null;

        return Scenario.Define("A result-backed option composes BindSome, MapSome, and Match", _resultOptionWorkflows)
            .When("A found value passes through BindSome, MapSome, and Match in one pipeline", async () =>
            {
                text = await Task.FromResult(CustomFound)
                    .BindSomeAsync(value => Task.FromResult(CustomLookup(value)))
                    .MapSomeAsync(order => order.Length)
                    .MatchAsync(length => $"{length}", () => "none", error => error);
            })
            .Then("The composed lookup, mapping, and match produce the expected value", () => Assert.Equal("7", text))
            .VerifyAsync();
    }

    [Fact]
    public Task Bind_Identity_FlattensNestedResults()
    {
        Result? unitSuccess = null;
        Result? unitFailure = null;
        Result<int>? typedSuccess = null;
        Result<int, string>? customSuccess = null;
        Result<int, string>? customFailure = null;

        return Scenario.Define("Binding an inner result flattens nested results", _resultOptionWorkflows)
            .When("Identity binding flattens nested successes and failures", () =>
            {
                unitSuccess = Result<Result>.Success(Result.Success()).Bind(inner => inner);
                unitFailure = Result<Result>.Success(Result.Failure(_failure)).Bind(inner => inner);
                typedSuccess = Result<Result<int>>.Success(Result<int>.Success(1)).Bind(inner => inner);
                customSuccess = Result<Result<int, string>, string>.Success(Result<int, string>.Success(1)).Bind(inner => inner);
                customFailure = Result<Result<int, string>, string>.Failure("failed").Bind(inner => inner);
            })
            .Then("The payload-free inner success is preserved", () => Assert.True(unitSuccess?.IsSuccess))
            .And("The payload-free inner failure is preserved", () => Assert.Equal(_failure, unitFailure?.Error))
            .And("The inner typed success retains its value", () => Assert.Equal(1, typedSuccess?.Value))
            .And("The inner custom-error success retains its value", () => Assert.Equal(1, customSuccess?.Value))
            .And("The outer custom failure is preserved", () => Assert.Equal("failed", customFailure?.Error))
            .VerifyAsync();
    }
}
