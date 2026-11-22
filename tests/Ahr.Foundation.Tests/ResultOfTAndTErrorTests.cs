namespace Ahr.Foundation.Tests;

public sealed class ResultOfTAndTErrorTests
{
    private static readonly BusinessContext _domainErrorHandling = BusinessContext
        .Define("domain-error-outcomes", "Application outcomes with domain errors", "Preserve successful application data and typed failure details across workflows.")
        .Capability("handle-typed-outcomes", "Handle returned values and domain-specific failure information")
        .Outcome("domain-outcome-preserved", "The valid value or typed error remains available through composition.")
        .Rule("read-only-valid-outcome-branch", "A value or error is read only from its matching result branch.")
        .Build();

    private sealed record Problem(string Code, int Severity);
    private sealed record Request(string Route, RequestContext Context, IReadOnlyDictionary<string, string> Headers);
    private sealed record RequestContext(string CorrelationId, User User);
    private sealed record User(string Id, IReadOnlyList<string> Roles);
    private sealed record DomainProblem(string Code, ProblemContext Context);
    private sealed record ProblemContext(string Operation, IReadOnlyList<string> Details);

    [Fact]
    public Task Success_NullValue_ThrowsWithValueParameter() =>
        Scenario.Define("A custom-error result rejects a null success value", _domainErrorHandling)
            .WhenThrows<ArgumentNullException>("A null value is passed to the successful-result factory", () => _ = Result<string, Problem>.Success(null!))
            .Then("The refusal identifies the value parameter", (ArgumentNullException refusal) => Assert.Equal("value", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Failure_NullError_ThrowsWithErrorParameter() =>
        Scenario.Define("A custom-error result rejects a null failure error", _domainErrorHandling)
            .WhenThrows<ArgumentNullException>("A null error is passed to the failure factory", () => _ = Result<int, Problem>.Failure(null!))
            .Then("The refusal identifies the error parameter", (ArgumentNullException refusal) => Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Default_IsSuccess_ThrowsConsistentUninitializedException() =>
        Scenario.Define("An uninitialized custom-error result cannot report success", _domainErrorHandling)
            .WhenThrows<InvalidOperationException>("IsSuccess is read from a default-constructed result", () => _ = default(Result<int, string>).IsSuccess)
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Map_Success_TransformsValue()
    {
        Result<string, Problem>? result = null;

        return Scenario.Define("Mapping a successful custom-error result transforms its value", _domainErrorHandling)
            .When("Map transforms the value in a successful typed-error result", () =>
                result = Result<int, Problem>.Success(21).Map(value => (value * 2).ToString()))
            .Then("The successful value is transformed to the expected string", () => Assert.Equal("42", result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task MapError_RecordErrorWithExpression_TransformsError()
    {
        Problem original = new("E1", 1);
        Result<int, Problem>? result = null;

        return Scenario.Define("Mapping a Problem record error preserves its value semantics", _domainErrorHandling)
            .When("MapError updates the Problem record held by a failed result", () =>
                result = Result<int, Problem>.Failure(original).MapError(error => error with { Severity = 2 }))
            .Then("The mapped Problem has updated severity", () => Assert.Equal(new Problem("E1", 2), result?.Error))
            .And("The original Problem remains unchanged", () => Assert.Equal(new Problem("E1", 1), original))
            .VerifyAsync();
    }

    [Fact]
    public Task Ensure_ExistingFailure_DoesNotInvokePredicate()
    {
        var calls = 0;
        Result<int, string>? result = null;

        return Scenario.Define("Validation skips its predicate for an existing failure", _domainErrorHandling)
            .When("Ensure receives an existing failure and a predicate", () =>
                result = Result<int, string>.Failure("bad").Ensure(_ => { calls++; return true; }, "other"))
            .Then("Ensure skips the predicate", () => Assert.Equal(0, calls))
            .And("The original typed error is retained", () => Assert.Equal("bad", result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task ToOption_Success_ReturnsSome()
    {
        Option<int>? option = null;

        return Scenario.Define("A successful custom-error result converts to Some", _domainErrorHandling)
            .When("A successful result is converted to an option", () => option = Result<int, string>.Success(7).ToOption())
            .Then("A successful result becomes a present option", () => Assert.True(option?.IsSome))
            .And("The option contains the successful value", () => Assert.Equal(7, option?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task ToOption_Failure_ReturnsNone()
    {
        Option<int>? option = null;

        return Scenario.Define("A failed custom-error result converts to None", _domainErrorHandling)
            .When("A failed result is converted to an option", () => option = Result<int, string>.Failure("bad").ToOption())
            .Then("A failed result becomes an empty option", () => Assert.True(option?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrDefault_Success_ReturnsValue()
    {
        int? value = null;

        return Scenario.Define("Reading a successful custom-error result returns its value", _domainErrorHandling)
            .When("GetValueOrDefault reads the value from a successful result", () => value = Result<int, string>.Success(7).GetValueOrDefault())
            .Then("A successful result returns its value", () => Assert.Equal(7, value))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrDefault_Failure_ReturnsDefault()
    {
        int? value = null;

        return Scenario.Define("Reading a failed custom-error result returns the default value", _domainErrorHandling)
            .When("GetValueOrDefault reads the default value from a failed result", () => value = Result<int, string>.Failure("bad").GetValueOrDefault())
            .Then("A failed result returns the default value", () => Assert.Equal(0, value))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrDefault_FailureWithFallback_ReturnsFallback()
    {
        string? value = null;

        return Scenario.Define("A failed custom-error result returns the supplied fallback", _domainErrorHandling)
            .When("GetValueOrDefault reads the explicit fallback from a failed result", () => value = Result<string, string>.Failure("bad").GetValueOrDefault("fallback"))
            .Then("A failed result returns the supplied fallback", () => Assert.Equal("fallback", value))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrDefault_SuccessWithFallback_ReturnsValue()
    {
        string? value = null;

        return Scenario.Define("A successful custom-error result ignores the supplied fallback", _domainErrorHandling)
            .When("GetValueOrDefault reads the value from a successful result", () => value = Result<string, string>.Success("value").GetValueOrDefault("fallback"))
            .Then("A successful result returns its value", () => Assert.Equal("value", value))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrDefault_NullFallback_ThrowsWithFallbackParameter() =>
        Scenario.Define("Reading a custom-error result rejects a null fallback", _domainErrorHandling)
            .WhenThrows<ArgumentNullException>("GetValueOrDefault receives a null fallback value", () => _ = Result<string, string>.Failure("bad").GetValueOrDefault(null!))
            .Then("The refusal identifies the fallback parameter", (ArgumentNullException refusal) => Assert.Equal("fallback", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Default_GetValueOrDefault_ThrowsConsistentUninitializedException() =>
        Scenario.Define("An uninitialized custom-error result cannot return a default value", _domainErrorHandling)
            .WhenThrows<InvalidOperationException>("GetValueOrDefault is called on a default-constructed result", () => _ = default(Result<int, string>).GetValueOrDefault())
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task OrElse_Success_ReturnsOriginalAndIgnoresFallback()
    {
        Result<int, string>? result = null;

        return Scenario.Define("OrElse keeps a successful custom-error result and ignores the eager fallback", _domainErrorHandling)
            .When("OrElse receives a successful result and an eager fallback", () =>
                result = Result<int, string>.Success(1).OrElse(Result<int, string>.Success(2)))
            .Then("The original success is returned and the eager fallback is ignored", () => Assert.Equal(1, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_Failure_ReturnsEagerFallback()
    {
        Result<int, string>? result = null;

        return Scenario.Define("OrElse replaces a failed custom-error result with the eager fallback", _domainErrorHandling)
            .When("OrElse receives a failed result and an eager fallback", () =>
                result = Result<int, string>.Failure("bad").OrElse(Result<int, string>.Success(2)))
            .Then("The failed result is replaced by the eager fallback", () => Assert.Equal(2, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_Func_Success_DoesNotInvokeFallback()
    {
        var calls = 0;
        Result<int, string>? result = null;

        return Scenario.Define("OrElse skips the fallback function for a successful custom-error result", _domainErrorHandling)
            .When("OrElse receives a successful result and a lazy fallback", () =>
                result = Result<int, string>.Success(1).OrElse(() => { calls++; return Result<int, string>.Success(2); }))
            .Then("The existing success is returned", () => Assert.Equal(1, result?.Value))
            .And("The lazy fallback is not invoked", () => Assert.Equal(0, calls))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_Func_Failure_InvokesFallback()
    {
        var calls = 0;
        Result<int, string>? result = null;

        return Scenario.Define("OrElse invokes the fallback function for a failed custom-error result", _domainErrorHandling)
            .When("OrElse receives a failed result and a lazy fallback", () =>
                result = Result<int, string>.Failure("bad").OrElse(() => { calls++; return Result<int, string>.Success(2); }))
            .Then("The fallback is invoked", () => Assert.Equal(1, calls))
            .And("Its successful result is returned", () => Assert.Equal(2, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_FuncNullFallback_ThrowsWithFallbackParameter() =>
        Scenario.Define("A custom-error result rejects a null fallback function", _domainErrorHandling)
            .WhenThrows<ArgumentNullException>("OrElse receives a null lazy fallback", () => _ = Result<int, string>.Failure("bad").OrElse(null!))
            .Then("The refusal identifies the fallback parameter", (ArgumentNullException refusal) => Assert.Equal("fallback", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task OrElse_Func_Default_ThrowsConsistentUninitializedException() =>
        Scenario.Define("OrElse rejects an uninitialized custom-error result", _domainErrorHandling)
            .WhenThrows<InvalidOperationException>("OrElse is called on a default-constructed result with a valid fallback", () => _ = default(Result<int, string>).OrElse(() => Result<int, string>.Success(1)))
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task OrElseAsync_Success_DoesNotInvokeFallback()
    {
        var calls = 0;
        Result<int, string>? result = null;

        return Scenario.Define("Asynchronous OrElse skips the fallback for a successful custom-error result", _domainErrorHandling)
            .When("OrElseAsync receives a successful result and an asynchronous fallback", async () =>
                result = await Result<int, string>.Success(1).OrElseAsync(() => { calls++; return Task.FromResult(Result<int, string>.Success(2)); }))
            .Then("The existing success is returned", () => Assert.Equal(1, result?.Value))
            .And("The asynchronous fallback is not invoked", () => Assert.Equal(0, calls))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElseAsync_Failure_InvokesFallback()
    {
        var calls = 0;
        Result<int, string>? result = null;

        return Scenario.Define("Asynchronous OrElse invokes the fallback for a failed custom-error result", _domainErrorHandling)
            .When("OrElseAsync receives a failed result and an asynchronous fallback", async () =>
                result = await Result<int, string>.Failure("bad").OrElseAsync(() => { calls++; return Task.FromResult(Result<int, string>.Success(2)); }))
            .Then("The fallback is invoked", () => Assert.Equal(1, calls))
            .And("Its successful result is returned", () => Assert.Equal(2, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElseAsync_NullFallback_ThrowsWithFallbackParameter() =>
        Scenario.Define("Asynchronous OrElse rejects a null fallback function for a custom-error result", _domainErrorHandling)
            .WhenThrows<ArgumentNullException>("OrElseAsync receives a null asynchronous fallback", async () => await Result<int, string>.Failure("bad").OrElseAsync(null!))
            .Then("The refusal identifies the fallback parameter", (ArgumentNullException refusal) => Assert.Equal("fallback", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Map_NullDelegate_ThrowsWithMapParameter() =>
        Scenario.Define("Mapping a custom-error result rejects a null mapper", _domainErrorHandling)
            .WhenThrows<ArgumentNullException>("Map receives a null mapping function", () => _ = Result<int, string>.Success(1).Map<string>(null!))
            .Then("The refusal identifies the map parameter", (ArgumentNullException refusal) => Assert.Equal("map", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Ensure_NullError_ThrowsWithErrorParameter() =>
        Scenario.Define("Validation rejects a null failure error", _domainErrorHandling)
            .WhenThrows<ArgumentNullException>("Ensure receives a null failure value", () => _ = Result<int, string>.Success(1).Ensure(_ => true, null!))
            .Then("The refusal identifies the error parameter", (ArgumentNullException refusal) => Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Bind_Associativity_Holds()
    {
        var expected = Result<string, string>.Success("#10");
        Result<string, string>? leftAssociated = null;
        Result<string, string>? rightAssociated = null;

        return Scenario.Define("Typed result binding preserves associativity", _domainErrorHandling)
            .When("Bind composes Double and Format in both association orders", () =>
            {
                static Result<int, string> Double(int value) => Result<int, string>.Success(value * 2);

                static Result<string, string> Format(int value) => Result<string, string>.Success($"#{value}");

                var source = Result<int, string>.Success(5);

                leftAssociated = source.Bind(Double).Bind(Format);
                rightAssociated = source.Bind(value => Double(value).Bind(Format));
            })
            .Then("The left-associated bind result has the expected value", () => Assert.Equal(expected, leftAssociated))
            .And("The right-associated bind result has the expected value", () => Assert.Equal(expected, rightAssociated))
            .VerifyAsync();
    }

    [Fact]
    public Task Default_Value_ThrowsConsistentUninitializedException() =>
        Scenario.Define("An uninitialized custom-error result cannot expose a value", _domainErrorHandling)
            .WhenThrows<InvalidOperationException>("Value is read from a default-constructed result", () => _ = default(Result<int, string>).Value)
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Default_Error_ThrowsConsistentUninitializedException() =>
        Scenario.Define("An uninitialized custom-error result cannot expose an error", _domainErrorHandling)
            .WhenThrows<InvalidOperationException>("Error is read from a default-constructed result", () => _ = default(Result<int, string>).Error)
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Default_IsFailure_ThrowsConsistentUninitializedException() =>
        Scenario.Define("An uninitialized custom-error result cannot report failure", _domainErrorHandling)
            .WhenThrows<InvalidOperationException>("IsFailure is read from a default-constructed result", () => _ = default(Result<int, string>).IsFailure)
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Success_Error_ThrowsWrongBranchException() =>
        Scenario.Define("A successful custom-error result cannot expose an error", _domainErrorHandling)
            .WhenThrows<InvalidOperationException>("Error is read from a successful result", () => _ = Result<int, string>.Success(1).Error)
            .Then("The result explains that it succeeded and has no error", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.ErrorOfSuccess, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Failure_Value_ThrowsWrongBranchException() =>
        Scenario.Define("A failed custom-error result cannot expose a value", _domainErrorHandling)
            .WhenThrows<InvalidOperationException>("Value is read from a failed result", () => _ = Result<int, string>.Failure("err").Value)
            .Then("The result explains that it failed and has no value", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.ValueOfFailure, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Match_Func_Success_InvokesSuccessBranch()
    {
        string? matched = null;

        return Scenario.Define("Matching a successful custom-error result selects its success handler", _domainErrorHandling)
            .When("A successful result is matched with value-returning handlers", () =>
                matched = Result<int, string>.Success(42).Match(v => $"ok: {v}", e => $"err: {e}"))
            .Then("The success handler returns its expected value", () => Assert.Equal("ok: 42", matched))
            .VerifyAsync();
    }

    [Fact]
    public Task Match_Func_Failure_InvokesFailureBranch()
    {
        string? matched = null;

        return Scenario.Define("Matching a failed custom-error result selects its failure handler", _domainErrorHandling)
            .When("A failed result is matched with value-returning handlers", () =>
                matched = Result<int, string>.Failure("not found").Match(v => $"ok: {v}", e => $"err: {e}"))
            .Then("The failure handler returns its expected value", () => Assert.Equal("err: not found", matched))
            .VerifyAsync();
    }

    [Fact]
    public Task Match_Action_Success_InvokesOnlySuccessAction()
    {
        var observed = 0;
        var failureCalled = false;
        var result = Result<int, string>.Success(42);
        Result<int, string>? returned = null;

        return Scenario.Define("Matching a successful custom-error result invokes only its success action", _domainErrorHandling)
            .When("A successful result is matched with success and failure actions", () =>
            {
                returned = result.Match(v => observed = v, _ => failureCalled = true);
            })
            .Then("The success action receives the value", () => Assert.Equal(42, observed))
            .And("The failure action is skipped", () => Assert.False(failureCalled))
            .And("The original result is returned", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task Match_Action_Failure_InvokesOnlyFailureAction()
    {
        var successCalled = false;
        string? observed = null;
        var result = Result<int, string>.Failure("err");
        Result<int, string>? returned = null;

        return Scenario.Define("Matching a failed custom-error result invokes only its failure action", _domainErrorHandling)
            .When("A failed result is matched with success and failure actions", () =>
            {
                returned = result.Match(_ => successCalled = true, e => observed = e);
            })
            .Then("The success action is skipped", () => Assert.False(successCalled))
            .And("The failure action receives the error", () => Assert.Equal("err", observed))
            .And("The original result is returned", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task MatchAsync_Action_Success_InvokesOnlySuccessAsyncAction()
    {
        var observed = 0;
        var failureCalled = false;
        var result = Result<int, string>.Success(42);
        Result<int, string>? returned = null;

        return Scenario.Define("Asynchronous matching invokes only the success action for a custom-error result", _domainErrorHandling)
            .When("A successful result is matched with asynchronous action handlers", async () =>
            {
                returned = await result.MatchAsync(v => { observed = v; return Task.CompletedTask; }, _ => { failureCalled = true; return Task.CompletedTask; });
            })
            .Then("The success action receives the value", () => Assert.Equal(42, observed))
            .And("The failure action is skipped", () => Assert.False(failureCalled))
            .And("The original result is returned", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task MatchAsync_Action_Failure_InvokesOnlyFailureAsyncAction()
    {
        var successCalled = false;
        string? observed = null;
        var result = Result<int, string>.Failure("err");
        Result<int, string>? returned = null;

        return Scenario.Define("Asynchronous matching invokes only the failure action for a custom-error result", _domainErrorHandling)
            .When("A failed result is matched with asynchronous action handlers", async () =>
            {
                returned = await result.MatchAsync(_ => { successCalled = true; return Task.CompletedTask; }, e => { observed = e; return Task.CompletedTask; });
            })
            .Then("The success action is skipped", () => Assert.False(successCalled))
            .And("The failure action receives the error", () => Assert.Equal("err", observed))
            .And("The original result is returned", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_Success_InvokesActionAndReturnsSelf()
    {
        var observed = 0;
        var result = Result<int, string>.Success(42);
        Result<int, string>? returned = null;

        return Scenario.Define("Tap observes a successful custom-error result and returns it unchanged", _domainErrorHandling)
            .When("Tap observes the value from a successful result", () =>
            {
                returned = result.Tap(v => observed = v);
            })
            .Then("The action receives the value", () => Assert.Equal(42, observed))
            .And("Tap returns the original successful result", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_Failure_DoesNotInvokeAction()
    {
        var tapped = false;
        var result = Result<int, string>.Failure("err");
        Result<int, string>? returned = null;

        return Scenario.Define("Tap skips observation for a failed custom-error result", _domainErrorHandling)
            .When("Tap receives a failed result", () =>
            {
                returned = result.Tap(_ => tapped = true);
            })
            .Then("The action is skipped", () => Assert.False(tapped))
            .And("The original result is preserved", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task TapAsync_Success_InvokesAsyncActionAndReturnsSelf()
    {
        var observed = 0;
        var result = Result<int, string>.Success(42);
        Result<int, string>? returned = null;

        return Scenario.Define("Asynchronous Tap observes a successful custom-error result and returns it unchanged", _domainErrorHandling)
            .When("TapAsync observes the value from a successful result", async () =>
            {
                returned = await result.TapAsync(v => { observed = v; return Task.CompletedTask; });
            })
            .Then("The asynchronous action receives the value", () => Assert.Equal(42, observed))
            .And("The original result is returned", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task TapAsync_Failure_DoesNotInvokeAsyncAction()
    {
        var tapped = false;
        var result = Result<int, string>.Failure("err");
        Result<int, string>? returned = null;

        return Scenario.Define("Asynchronous Tap skips observation for a failed custom-error result", _domainErrorHandling)
            .When("TapAsync receives a failed result", async () =>
            {
                returned = await result.TapAsync(_ => { tapped = true; return Task.CompletedTask; });
            })
            .Then("The asynchronous action is skipped", () => Assert.False(tapped))
            .And("The original result is preserved", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task TapError_Success_DoesNotInvokeAction()
    {
        var tapped = false;
        var result = Result<int, string>.Success(42);
        Result<int, string>? returned = null;

        return Scenario.Define("TapError skips observation for a successful custom-error result", _domainErrorHandling)
            .When("TapError receives a successful result", () =>
            {
                returned = result.TapError(_ => tapped = true);
            })
            .Then("The error action is skipped", () => Assert.False(tapped))
            .And("The original result is preserved", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task TapError_Failure_InvokesAction()
    {
        string? observed = null;
        var result = Result<int, string>.Failure("err");
        Result<int, string>? returned = null;

        return Scenario.Define("TapError observes a failed custom-error result and returns it unchanged", _domainErrorHandling)
            .When("TapError observes the error from a failed result", () =>
            {
                returned = result.TapError(e => observed = e);
            })
            .Then("The failure observer receives the typed error", () => Assert.Equal("err", observed))
            .And("TapError returns the original result", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task TapErrorAsync_Failure_InvokesAsyncAction()
    {
        string? observed = null;
        var result = Result<int, string>.Failure("err");
        Result<int, string>? returned = null;

        return Scenario.Define("Asynchronous TapError observes a failed custom-error result", _domainErrorHandling)
            .When("TapErrorAsync observes the error from a failed result", async () =>
            {
                returned = await result.TapErrorAsync(e => { observed = e; return Task.CompletedTask; });
            })
            .Then("The asynchronous failure observer receives the original error", () => Assert.Equal("err", observed))
            .And("TapErrorAsync returns the original result", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task TapErrorAsync_Success_DoesNotInvokeAsyncAction()
    {
        var tapped = false;
        var result = Result<int, string>.Success(42);
        Result<int, string>? returned = null;

        return Scenario.Define("Asynchronous TapError skips observation for a successful custom-error result", _domainErrorHandling)
            .When("TapErrorAsync receives a successful result", async () =>
            {
                returned = await result.TapErrorAsync(_ => { tapped = true; return Task.CompletedTask; });
            })
            .Then("The asynchronous error action is skipped", () => Assert.False(tapped))
            .And("The original result is preserved", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task Equality_EquivalentSuccesses_HaveSameHashCode()
    {
        var left = Result<int, string>.Success(42);
        var right = Result<int, string>.Success(42);
        bool? areEqual = null;
        bool? hashesMatch = null;

        return Scenario.Define("Equivalent successful custom-error results have equal hash codes", _domainErrorHandling)
            .When("Two equivalent successful results are compared", () =>
            {
                areEqual = left.Equals(right);
                hashesMatch = left.GetHashCode() == right.GetHashCode();
            })
            .Then("The successful results are equal", () => Assert.True(areEqual))
            .And("Equivalent results have equal hash codes", () => Assert.True(hashesMatch))
            .VerifyAsync();
    }

    [Fact]
    public Task Equality_EquivalentFailures_HaveSameHashCode()
    {
        var left = Result<int, string>.Failure("bad");
        var right = Result<int, string>.Failure("bad");
        bool? areEqual = null;
        bool? hashesMatch = null;

        return Scenario.Define("Equivalent failed custom-error results have equal hash codes", _domainErrorHandling)
            .When("Two equivalent failed results are compared", () =>
            {
                areEqual = left.Equals(right);
                hashesMatch = left.GetHashCode() == right.GetHashCode();
            })
            .Then("The failed results are equal", () => Assert.True(areEqual))
            .And("Equivalent results have equal hash codes", () => Assert.True(hashesMatch))
            .VerifyAsync();
    }

    [Fact]
    public Task Success_ComplexPayload_PreservesExactInstanceThroughBind()
    {
        Request request = CreateRequest();
        Result<Request, DomainProblem>? result = null;

        return Scenario.Define("Binding preserves the exact successful payload instance", _domainErrorHandling)
            .When("A successful result binds a nested Request payload", () =>
            {
                result = Result<Request, DomainProblem>.Success(request).Bind(Result<Request, DomainProblem>.Success);
            })
            .Then("The success path preserves the exact payload instance through Bind", () => Assert.Same(request, result?.Value))
            .And("The payload retains the admin role", () => Assert.Equal(["admin"], result?.Value.Context.User.Roles))
            .And("The payload retains its Accept header", () => Assert.Equal("application/json", result?.Value.Headers["Accept"]))
            .VerifyAsync();
    }

    [Fact]
    public Task Match_ComplexPayload_ProvidesExactInstance()
    {
        Request request = CreateRequest();
        Request? matched = null;

        return Scenario.Define("Matching returns the exact successful custom-error payload instance", _domainErrorHandling)
            .When("A nested Request is provided to a successful-result handler", () =>
            {
                matched = Result<Request, DomainProblem>.Success(request)
                    .Match(value => value, _ => throw new Xunit.Sdk.XunitException("Failure branch invoked."));
            })
            .Then("The success handler receives the exact payload instance", () => Assert.Same(request, matched))
            .VerifyAsync();
    }

    [Fact]
    public Task Failure_ComplexError_ShortCircuitsAndPreservesExactInstance()
    {
        DomainProblem problem = CreateProblem();
        var bindCalled = false;
        Result<Request, DomainProblem>? result = null;

        return Scenario.Define("A failed custom-error result preserves its error instance while short-circuiting", _domainErrorHandling)
            .When("A failed result contains a nested DomainProblem error", () =>
            {
                result = Result<Request, DomainProblem>.Failure(problem)
                    .Bind(value =>
                    {
                        bindCalled = true;
                        return Result<Request, DomainProblem>.Success(value);
                    });
            })
            .Then("The bind continuation is skipped", () => Assert.False(bindCalled))
            .And("The failed result preserves the exact error instance", () => Assert.Same(problem, result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task MapError_ComplexError_TransformsNestedDataWithoutChangingOriginal()
    {
        DomainProblem? problem = null;
        Result<Request, DomainProblem>? result = null;

        return Scenario.Define("Error mapping changes nested data without mutating the original error", _domainErrorHandling)
            .When("MapError transforms nested DomainProblem data on a failed result", () =>
            {
                problem = CreateProblem();
                result = Result<Request, DomainProblem>.Failure(problem)
                    .MapError(error => error with
                    {
                        Context = error.Context with
                        {
                            Details = [.. error.Context.Details, "retryable"]
                        }
                    });
            })
            .Then("The original error retains its nested data", () => Assert.Equal(["timeout"], problem?.Context.Details))
            .And("The mapped error contains the updated nested data", () => Assert.Equal(["timeout", "retryable"], result?.Error.Context.Details))
            .VerifyAsync();
    }

    private static Request CreateRequest() =>
        new(
            "/orders",
            new RequestContext("correlation-42", new User("user-7", ["admin"])),
            new Dictionary<string, string> { ["Accept"] = "application/json" });

    private static DomainProblem CreateProblem() =>
        new("gateway-timeout", new ProblemContext("submit-order", ["timeout"]));
}
