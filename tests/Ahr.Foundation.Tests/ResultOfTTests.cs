namespace Ahr.Foundation.Tests;

public sealed class ResultOfTTests
{
    private static readonly BusinessContext _valueResultHandling = BusinessContext
        .Define("value-result-workflows", "Application value-result workflows", "Carry successful application data and explain unsuccessful operations.")
        .Capability("handle-value-outcomes", "Handle returned values and failures in application workflows")
        .Outcome("value-or-failure-preserved", "Successful data or its failure detail remains available through composition.")
        .Rule("read-only-valid-outcome-branch", "A value or error is read only from its matching result branch.")
        .Build();

    private sealed record Person(string Name, int Age);
    private sealed record Order(int Id, Customer Customer, IReadOnlyList<OrderLine> Lines);
    private sealed record Customer(string Name, Address Address);
    private sealed record Address(string City, string Country);
    private sealed record OrderLine(string Sku, int Quantity);

    [Fact]
    public Task Success_NullValue_ThrowsWithValueParameter() =>
        Scenario.Define("A successful result rejects a null value", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("A null value is passed to the successful-result factory", () => _ = Result<string>.Success(null!))
            .Then("The refusal identifies the value parameter", (ArgumentNullException refusal) => Assert.Equal("value", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Failure_DefaultError_ThrowsWithErrorParameter() =>
        Scenario.Define("A failed result rejects a default error", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("A default error is passed to the failure factory", () => _ = Result<int>.Failure(default))
            .Then("The refusal identifies the error parameter", (ArgumentNullException refusal) => Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Default_Value_ThrowsConsistentUninitializedException() =>
        Scenario.Define("An uninitialized result rejects value access", _valueResultHandling)
            .WhenThrows<InvalidOperationException>("Value is read from a default-constructed result", () => _ = default(Result<int>).Value)
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Default_IsSuccess_ThrowsConsistentUninitializedException() =>
        Scenario.Define("An uninitialized result cannot report its success state", _valueResultHandling)
            .WhenThrows<InvalidOperationException>("IsSuccess is read from a default-constructed result", () => _ = default(Result<int>).IsSuccess)
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Default_Map_ThrowsConsistentUninitializedException() =>
        Scenario.Define("An uninitialized result cannot be mapped", _valueResultHandling)
            .WhenThrows<InvalidOperationException>("Map is called on a default-constructed result", () => _ = default(Result<int>).Map(value => value + 1))
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Default_BindWithNullDelegate_ThrowsConsistentUninitializedException() =>
        Scenario.Define("Binding an uninitialized result throws before invoking the binder", _valueResultHandling)
            .WhenThrows<InvalidOperationException>("Bind is called on a default-constructed result with a null continuation", () => _ = default(Result<int>).Bind<string>(null!))
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Failure_Value_ThrowsWrongBranchException()
    {
        var result = Result<int>.Failure(new Error("bad"));

        return Scenario.Define("A failed result cannot expose a value", _valueResultHandling)
            .WhenThrows<InvalidOperationException>("Value is read from a failed result", () => _ = result.Value)
            .Then("The result explains that it failed and has no value", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.ValueOfFailure, exception.Message))
            .VerifyAsync();
    }

    [Fact]
    public Task Success_Error_ThrowsWrongBranchException() =>
        Scenario.Define("A successful result cannot expose an error", _valueResultHandling)
            .WhenThrows<InvalidOperationException>("Error is read from a successful result", () => _ = Result<int>.Success(1).Error)
            .Then("The result explains that it succeeded and has no error", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.ErrorOfSuccess, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Map_RecordPayloadWithExpression_PreservesRecordSemantics()
    {
        Result<Person>? mapped = null;

        return Scenario.Define("Mapping a record payload preserves its value semantics", _valueResultHandling)
            .When("Map updates the Age field of a Person in a successful result", () =>
            {
                mapped = Result<Person>.Success(new Person("Ada", 30)).Map(person => person with { Age = 31 });
            })
            .Then("The updated record retains its value semantics", () => Assert.Equal(new Person("Ada", 31), mapped?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task MapError_RecordErrorWithExpression_PreservesRecordSemantics()
    {
        Result<int>? mapped = null;

        return Scenario.Define("Mapping a record error preserves its value semantics", _valueResultHandling)
            .When("MapError updates the Error record held by a failed result", () =>
            {
                mapped = Result<int>.Failure(new Error("bad")).MapError(error => error with { Message = "worse" });
            })
            .Then("The updated record retains its value semantics", () => Assert.Equal(new Error("worse"), mapped?.Error))
            .VerifyAsync();
    }

    private sealed record ApiError(string Code);

    [Fact]
    public Task MapError_ToCustomError_ChangesOnlyTheFailure()
    {
        Result<int, ApiError>? failed = null;
        Result<int, ApiError>? succeeded = null;

        return Scenario.Define("Error mapping changes the failure while preserving successful values", _valueResultHandling)
            .When("MapError converts a built-in failure to ApiError while leaving success values unchanged", () =>
            {
                failed = Result<int>.Failure(new Error("bad")).MapError(error => new ApiError(error.Message));
                succeeded = Result<int>.Success(1).MapError(error => new ApiError(error.Message));
            })
            .Then("The failure is converted to ApiError", () => Assert.Equal(new ApiError("bad"), failed?.Error))
            .And("Successful values remain unchanged", () => Assert.Equal(1, succeeded?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task MapError_ReturningError_StillKeepsResultOfT()
    {
        object? result = null;
        Error? mappedError = null;

        return Scenario.Define("Mapping an error to Error preserves the generic result type", _valueResultHandling)
            .When("MapError returns a new Error for a failed Result<T>", () =>
            {
#pragma warning disable IDE0008
                var mapped = Result<int>.Failure(new Error("bad")).MapError(error => new Error($"{error.Message}!"));
#pragma warning restore IDE0008

                result = mapped;
                mappedError = mapped.Error;
            })
            .Then("The error mapping retains the generic Result<T> type", () => Assert.IsType<Result<int>>(result))
            .And("The mapped error is returned", () => Assert.Equal(new Error("bad!"), mappedError))
            .VerifyAsync();
    }

    [Fact]
    public Task MapErrorAsync_ToCustomError_ChangesOnlyTheFailure()
    {
        Result<int, ApiError>? direct = null;
        Result<int, ApiError>? synchronous = null;
        Result<int, ApiError>? asynchronous = null;
        object? unchanged = null;

        return Scenario.Define("Asynchronous error mapping preserves successful values", _valueResultHandling)
            .When("MapErrorAsync maps built-in failures to ApiError for synchronous and asynchronous delegates", async () =>
            {
                direct = await Result<int>.Failure(new Error("bad")).MapErrorAsync(error => Task.FromResult(new ApiError(error.Message)));
                synchronous = await Task.FromResult(Result<int>.Failure(new Error("bad"))).MapErrorAsync(error => new ApiError(error.Message));
                asynchronous = await Task.FromResult(Result<int>.Success(1)).MapErrorAsync(error => Task.FromResult(new ApiError(error.Message)));

#pragma warning disable IDE0008
                var unchangedResult = await Task.FromResult(Result<int>.Failure(new Error("bad"))).MapErrorAsync(error => Task.FromResult(new Error("worse")));
#pragma warning restore IDE0008

                unchanged = unchangedResult;
            })
            .Then("The direct asynchronous delegate maps the failure", () => Assert.Equal(new ApiError("bad"), direct?.Error))
            .And("The synchronous delegate maps the failure", () => Assert.Equal(new ApiError("bad"), synchronous?.Error))
            .And("The asynchronous delegate preserves the successful value", () => Assert.Equal(1, asynchronous?.Value))
            .And("Mapping the Error type retains Result<T>", () => Assert.IsType<Result<int>>(unchanged))
            .VerifyAsync();
    }

    [Fact]
    public Task MapError_ToCustomError_NullMapper_ThrowsWithMapParameter() =>
        Scenario.Define("Error mapping rejects a null function", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("MapError receives a null mapping function while converting a failure", () => _ = Result<int>.Failure(new Error("bad")).MapError<ApiError>(null!))
            .Then("The refusal identifies the map parameter", (ArgumentNullException refusal) => Assert.Equal("map", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task ToResult_DropsTheValueAndKeepsTheFailure()
    {
        var succeeded = false;
        Error? synchronousFailure = null;
        var asynchronouslySucceeded = false;
        Error? asynchronousFailure = null;

        return Scenario.Define("Converting to a payload-free result preserves failure and drops the value", _valueResultHandling)
            .When("Success and failure outcomes are converted to payload-free Result values", async () =>
            {
                succeeded = Result<int>.Success(1).ToResult().IsSuccess;
                synchronousFailure = Result<int>.Failure(new Error("bad")).ToResult().Error;
                asynchronouslySucceeded = (await Task.FromResult(Result<int>.Success(1)).ToResultAsync()).IsSuccess;
                asynchronousFailure = (await Task.FromResult(Result<int>.Failure(new Error("bad"))).ToResultAsync()).Error;
            })
            .Then("Synchronous conversion preserves success", () => Assert.True(succeeded))
            .And("Synchronous conversion preserves the error", () => Assert.Equal(new Error("bad"), synchronousFailure))
            .And("Asynchronous conversion preserves success", () => Assert.True(asynchronouslySucceeded))
            .And("Asynchronous conversion preserves the error", () => Assert.Equal(new Error("bad"), asynchronousFailure))
            .VerifyAsync();
    }

    [Fact]
    public Task ToResultAsync_EndsAResultOfOptionChain()
    {
        Result? restored = null;

        return Scenario.Define("Asynchronous conversion ends a result-backed option chain", _valueResultHandling)
            .When("A task-composed Result<Option<T>> is converted to a payload-free result", async () =>
            {
                restored = await Task.FromResult(Result<Option<int>>.Success(Option<int>.Some(1)))
                                    .TapSomeAsync(_ => Result.Success())
                                    .ToResultAsync();
            })
            .Then("The optional-result pipeline ends as a payload-free result", () => Assert.True(restored?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task Map_Failure_DoesNotInvokeMapper()
    {
        var calls = 0;
        Result<string>? mapped = null;

        return Scenario.Define("Mapping a failed result skips the mapper", _valueResultHandling)
            .When("Map receives a failed result and a value-mapping delegate", () =>
            {
                mapped = Result<int>.Failure(new Error("bad")).Map(value => { calls++; return value.ToString(); });
            })
            .Then("The mapper is skipped", () => Assert.Equal(0, calls))
            .And("The original failure is preserved", () => Assert.Equal(new Error("bad"), mapped?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task Ensure_FalsePredicate_ChangesSuccessToFailure()
    {
        Result<int>? ensured = null;

        return Scenario.Define("A failing predicate turns a successful result into failure", _valueResultHandling)
            .When("Ensure checks a successful value against a predicate that rejects it", () =>
            {
                ensured = Result<int>.Success(3).Ensure(value => value > 5, new Error("small"));
            })
            .Then("The rejected value becomes a failure", () => Assert.True(ensured?.IsFailure))
            .And("The supplied error is returned", () => Assert.Equal(new Error("small"), ensured?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task Bind_LeftIdentity_Holds()
    {
        Result<int>? bound = null;
        static Result<int> AddOne(int value) => Result<int>.Success(value + 1);

        return Scenario.Define("Binding a successful value applies the provided function", _valueResultHandling)
            .When("Bind applies AddOne to the same value as direct function application", () =>
            {
                bound = Result<int>.Success(4).Bind(AddOne);
            })
            .Then("Binding a success matches applying the continuation directly", () => Assert.Equal(AddOne(4), bound))
            .VerifyAsync();
    }

    [Fact]
    public Task Bind_RightIdentity_Holds()
    {
        var source = Result<int>.Success(4);
        Result<int>? bound = null;

        return Scenario.Define("Binding with Success preserves the original result", _valueResultHandling)
            .When("Bind applies the success factory to an existing successful result", () =>
            {
                bound = source.Bind(Result<int>.Success);
            })
            .Then("Binding the success factory returns the original result", () => Assert.Equal(source, bound))
            .VerifyAsync();
    }

    [Fact]
    public Task Map_Identity_Holds()
    {
        var source = Result<int>.Failure(new Error("bad"));
        Result<int>? mapped = null;

        return Scenario.Define("Mapping with the identity function preserves the result", _valueResultHandling)
            .When("Map receives a failed result and applies the identity function", () =>
            {
                mapped = source.Map(value => value);
            })
            .Then("Mapping the identity function returns the original result", () => Assert.Equal(source, mapped))
            .VerifyAsync();
    }

    [Fact]
    public Task ExplicitConversion_RoundTrip_PreservesResult()
    {
        var original = Result<int>.Success(42);
        Result<int>? roundTrip = null;

        return Scenario.Define("Converting between result shapes preserves the outcome", _valueResultHandling)
            .When("A successful result is converted to a custom-error form and back", () =>
            {
                var generic = (Result<int, Error>)original;
                roundTrip = (Result<int>)generic;
            })
            .Then("Converting between result error forms and back preserves the original", () => Assert.Equal(original, roundTrip))
            .VerifyAsync();
    }

    [Fact]
    public Task ExplicitConversion_FromUninitializedGenericResult_ThrowsConsistentException() =>
        Scenario.Define("Converting an uninitialized generic result throws", _valueResultHandling)
            .WhenThrows<InvalidOperationException>("A default-constructed custom-error result is converted to Result<T>", () => _ = (Result<int>)default(Result<int, Error>))
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task ExplicitConversion_ToUninitializedGenericResult_ThrowsConsistentException() =>
        Scenario.Define("Converting to an uninitialized generic result throws", _valueResultHandling)
            .WhenThrows<InvalidOperationException>("A default-constructed Result<T> is converted to a custom-error result", () => _ = (Result<int, Error>)default(Result<int>))
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Bind_NullDelegate_ThrowsWithBindParameter() =>
        Scenario.Define("Binding a result rejects a null function", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("Bind receives a null continuation", () => _ = Result<int>.Success(1).Bind<string>(null!))
            .Then("The refusal identifies the bind parameter", (ArgumentNullException refusal) => Assert.Equal("bind", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Equality_EquivalentSuccesses_HaveSameHashCode()
    {
        var left = Result<int>.Success(42);
        var right = Result<int>.Success(42);
        bool? areEqual = null;
        bool? hashesMatch = null;

        return Scenario.Define("Equivalent successful results have equal hash codes", _valueResultHandling)
            .When("The successful results are compared", () =>
            {
                areEqual = left.Equals(right);
                hashesMatch = left.GetHashCode() == right.GetHashCode();
            })
            .Then("The successful results are equal", () => Assert.True(areEqual))
            .And("Equivalent results have equal hash codes", () => Assert.True(hashesMatch))
            .VerifyAsync();
    }

    [Fact]
    public Task Match_Action_Success_InvokesOnlySuccessAction()
    {
        var observed = 0;
        var failureCalled = false;
        var result = Result<int>.Success(42);
        Result<int>? returned = null;

        return Scenario.Define("Matching a successful result invokes only its success action", _valueResultHandling)
            .When("A successful result is matched with success and failure actions", () =>
            {
                returned = result.Match(val => observed = val, _ => failureCalled = true);
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
        Error? observed = null;
        Error expected = new("err");
        var result = Result<int>.Failure(expected);
        Result<int>? returned = null;

        return Scenario.Define("Matching a failed result invokes only its failure action", _valueResultHandling)
            .When("A failed result is matched with success and failure actions", () =>
            {
                returned = result.Match(_ => successCalled = true, err => observed = err);
            })
            .Then("The success action is skipped", () => Assert.False(successCalled))
            .And("The failure action receives the error", () => Assert.Equal(expected, observed))
            .And("The original result is returned", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task Match_Action_NullSuccessAction_ThrowsWithExactParameter() =>
        Scenario.Define("Result matching requires a success action", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("Match receives a null onSuccess handler", () => _ = Result<int>.Success(42).Match(null!, _ => { }))
            .Then("The refusal identifies the onSuccess handler", (ArgumentNullException refusal) => Assert.Equal("onSuccess", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Match_Action_NullFailureAction_ThrowsWithExactParameter() =>
        Scenario.Define("Result matching requires a failure action", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("Match receives a null onFailure handler", () => _ = Result<int>.Success(42).Match(_ => { }, null!))
            .Then("The refusal identifies the onFailure handler", (ArgumentNullException refusal) => Assert.Equal("onFailure", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_Action_Success_InvokesOnlySuccessAsyncAction()
    {
        var observed = 0;
        var failureCalled = false;
        var result = Result<int>.Success(42);
        Result<int>? returned = null;

        return Scenario.Define("Asynchronous matching invokes only the success action", _valueResultHandling)
            .When("A successful result is matched with asynchronous action handlers", async () =>
            {
                returned = await result.MatchAsync(val => { observed = val; return Task.CompletedTask; }, _ => { failureCalled = true; return Task.CompletedTask; });
            })
            .Then("The asynchronous success action receives the value", () => Assert.Equal(42, observed))
            .And("The asynchronous failure action is skipped", () => Assert.False(failureCalled))
            .And("The original result is returned", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task MatchAsync_Action_Failure_InvokesOnlyFailureAsyncAction()
    {
        var successCalled = false;
        Error? observed = null;
        Error expected = new("err");
        var result = Result<int>.Failure(expected);
        Result<int>? returned = null;

        return Scenario.Define("Asynchronous matching invokes only the failure action", _valueResultHandling)
            .When("A failed result is matched with asynchronous action handlers", async () =>
            {
                returned = await result.MatchAsync(_ => { successCalled = true; return Task.CompletedTask; }, err => { observed = err; return Task.CompletedTask; });
            })
            .Then("The asynchronous success action is skipped", () => Assert.False(successCalled))
            .And("The asynchronous failure action receives the error", () => Assert.Equal(expected, observed))
            .And("The original result is returned", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task Bind_ToPayloadFreeResult_Success_ReturnsBoundResult()
    {
        Result? bound = null;

        return Scenario.Define("Binding to a payload-free result returns its success", _valueResultHandling)
            .When("Bind continues a successful value into a payload-free Result", () =>
            {
                bound = Result<int>.Success(42).Bind(val => val > 0 ? Result.Success() : Result.Failure(new Error("neg")));
            })
            .Then("The bound payload-free result is a success", () => Assert.True(bound?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task Bind_ToPayloadFreeResult_Failure_PropagatesErrorWithoutInvokingBind()
    {
        var invoked = false;
        Result? bound = null;

        return Scenario.Define("A failed result skips payload-free binding and preserves its error", _valueResultHandling)
            .When("Bind receives a failed result before a payload-free continuation", () =>
            {
                bound = Result<int>.Failure(new Error("bad")).Bind(_ => { invoked = true; return Result.Success(); });
            })
            .Then("The binder is not invoked", () => Assert.False(invoked))
            .And("The bound result remains a failure", () => Assert.True(bound?.IsFailure))
            .And("The original error is propagated", () => Assert.Equal(new Error("bad"), bound?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task Bind_ToPayloadFreeResult_NullBind_ThrowsWithExactParameter() =>
        Scenario.Define("Payload-free binding rejects a null function", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("Bind receives a null continuation for a payload-free result", () => _ = Result<int>.Success(42).Bind(null!))
            .Then("The refusal identifies the bind parameter", (ArgumentNullException refusal) => Assert.Equal("bind", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task BindAsync_ToPayloadFreeResult_Success_ReturnsBoundResult()
    {
        Result? bound = null;

        return Scenario.Define("Asynchronous binding returns a payload-free success result", _valueResultHandling)
            .When("BindAsync continues a successful value into a payload-free Result", async () =>
            {
                bound = await Result<int>.Success(42).BindAsync(val => Task.FromResult(val > 0 ? Result.Success() : Result.Failure(new Error("neg"))));
            })
            .Then("The bound payload-free result is a success", () => Assert.True(bound?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task BindAsync_ToPayloadFreeResult_Failure_PropagatesErrorWithoutInvokingBind()
    {
        var invoked = false;
        Result? bound = null;

        return Scenario.Define("A failed result skips asynchronous payload-free binding and preserves its error", _valueResultHandling)
            .When("BindAsync receives a failed result before a payload-free continuation", async () =>
            {
                bound = await Result<int>.Failure(new Error("bad")).BindAsync(_ => { invoked = true; return Task.FromResult(Result.Success()); });
            })
            .Then("The binder is not invoked", () => Assert.False(invoked))
            .And("The bound result remains a failure", () => Assert.True(bound?.IsFailure))
            .And("The original error is propagated", () => Assert.Equal(new Error("bad"), bound?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task BindAsync_ToPayloadFreeResult_NullBind_ThrowsWithExactParameter() =>
        Scenario.Define("Asynchronous payload-free binding rejects a null function", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("BindAsync receives a null continuation for a payload-free result", async () => await Result<int>.Success(42).BindAsync(null!))
            .Then("The refusal identifies the bind parameter", (ArgumentNullException refusal) => Assert.Equal("bind", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Tap_Success_InvokesActionAndReturnsSelf()
    {
        var observed = 0;
        var result = Result<int>.Success(42);
        Result<int>? returned = null;

        return Scenario.Define("Tap observes a successful result and returns it unchanged", _valueResultHandling)
            .When("Tap observes the value from a successful result", () =>
            {
                returned = result.Tap(val => observed = val);
            })
            .Then("The action receives the successful value", () => Assert.Equal(42, observed))
            .And("Tap returns the original successful result", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_Failure_DoesNotInvokeAction()
    {
        var tapped = false;
        var result = Result<int>.Failure(new Error("err"));
        Result<int>? returned = null;

        return Scenario.Define("Tap skips observation for a failed result", _valueResultHandling)
            .When("Tap receives a failed result", () =>
            {
                returned = result.Tap(_ => tapped = true);
            })
            .Then("The action is skipped", () => Assert.False(tapped))
            .And("The original failed result is preserved", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_NullAction_ThrowsWithExactParameter()
    {
        Action<int> action = null!;

        return Scenario.Define("Tap rejects a null observer", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("Tap receives a null observation action", () => _ = Result<int>.Success(42).Tap(action))
            .Then("The refusal identifies the action parameter", (ArgumentNullException refusal) => Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task TapAsync_Success_InvokesAsyncActionAndReturnsSelf()
    {
        var observed = 0;
        var result = Result<int>.Success(42);
        Result<int>? returned = null;

        return Scenario.Define("Asynchronous Tap observes a successful result and returns it unchanged", _valueResultHandling)
            .When("TapAsync observes the value from a successful result", async () =>
            {
                returned = await result.TapAsync(val => { observed = val; return Task.CompletedTask; });
            })
            .Then("The asynchronous action receives the successful value", () => Assert.Equal(42, observed))
            .And("TapAsync returns the original successful result", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task TapAsync_Failure_DoesNotInvokeAsyncAction()
    {
        var tapped = false;
        var result = Result<int>.Failure(new Error("err"));
        Result<int>? returned = null;

        return Scenario.Define("Asynchronous Tap skips observation for a failed result", _valueResultHandling)
            .When("TapAsync receives a failed result", async () =>
            {
                returned = await result.TapAsync(_ => { tapped = true; return Task.CompletedTask; });
            })
            .Then("The asynchronous action is skipped", () => Assert.False(tapped))
            .And("The original failed result is preserved", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task TapAsync_NullAction_ThrowsWithExactParameter()
    {
        Func<int, Task> action = null!;

        return Scenario.Define("Asynchronous Tap rejects a null observer", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("TapAsync receives a null observation action", async () => await Result<int>.Success(42).TapAsync(action))
            .Then("The refusal identifies the action parameter", (ArgumentNullException refusal) => Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task TapError_Success_DoesNotInvokeAction()
    {
        var tapped = false;
        var result = Result<int>.Success(42);
        Result<int>? returned = null;

        return Scenario.Define("TapError skips observation for a successful result", _valueResultHandling)
            .When("TapError receives a successful result", () =>
            {
                returned = result.TapError(_ => tapped = true);
            })
            .Then("The failure observer is skipped", () => Assert.False(tapped))
            .And("The original successful result is preserved", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task TapError_Failure_InvokesAction()
    {
        Error? observed = null;
        Error error = new("err");
        var result = Result<int>.Failure(error);
        Result<int>? returned = null;

        return Scenario.Define("TapError observes a failed result and returns it unchanged", _valueResultHandling)
            .When("TapError observes the error from a failed result", () =>
            {
                returned = result.TapError(err => observed = err);
            })
            .Then("The failure observer receives the error", () => Assert.Equal(error, observed))
            .And("The original failed result is preserved", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task TapError_NullAction_ThrowsWithExactParameter() =>
        Scenario.Define("TapError rejects a null observer", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("TapError receives a null failure observer", () => _ = Result<int>.Failure(new Error("err")).TapError(null!))
            .Then("The refusal identifies the action parameter", (ArgumentNullException refusal) => Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TapErrorAsync_Failure_InvokesAsyncAction()
    {
        Error? observed = null;
        Error error = new("err");
        var result = Result<int>.Failure(error);
        Result<int>? returned = null;

        return Scenario.Define("Asynchronous TapError observes a failed result", _valueResultHandling)
            .When("TapErrorAsync observes the error from a failed result", async () =>
            {
                returned = await result.TapErrorAsync(err => { observed = err; return Task.CompletedTask; });
            })
            .Then("The asynchronous failure observer receives the original error", () => Assert.Equal(error, observed))
            .And("The original failed result is preserved", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task TapErrorAsync_Success_DoesNotInvokeAsyncAction()
    {
        var tapped = false;
        var result = Result<int>.Success(42);
        Result<int>? returned = null;

        return Scenario.Define("Asynchronous TapError skips observation for a successful result", _valueResultHandling)
            .When("TapErrorAsync receives a successful result", async () =>
            {
                returned = await result.TapErrorAsync(_ => { tapped = true; return Task.CompletedTask; });
            })
            .Then("The asynchronous failure observer is skipped", () => Assert.False(tapped))
            .And("The original successful result is preserved", () => Assert.Equal(result, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task TapErrorAsync_NullAction_ThrowsWithExactParameter() =>
        Scenario.Define("Asynchronous TapError rejects a null observer", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("TapErrorAsync receives a null failure observer", async () => await Result<int>.Failure(new Error("err")).TapErrorAsync(null!))
            .Then("The refusal identifies the action parameter", (ArgumentNullException refusal) => Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task ToOption_Success_ReturnsSome()
    {
        Option<int>? option = null;

        return Scenario.Define("Converting a successful result to an option yields Some", _valueResultHandling)
            .When("A successful result is converted to an option", () =>
            {
                option = Result<int>.Success(42).ToOption();
            })
            .Then("A successful result becomes a present option", () => Assert.True(option?.IsSome))
            .And("The option contains the successful value", () => Assert.Equal(42, option?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task ToOption_Failure_ReturnsNone()
    {
        Option<int>? option = null;

        return Scenario.Define("Converting a failed result to an option yields None", _valueResultHandling)
            .When("A failed result is converted to an option", () =>
            {
                option = Result<int>.Failure(new Error("bad")).ToOption();
            })
            .Then("A failed result becomes an empty option", () => Assert.True(option?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrDefault_Success_ReturnsValue()
    {
        int? value = null;

        return Scenario.Define("Reading a successful result returns its value", _valueResultHandling)
            .When("GetValueOrDefault reads the value from a successful result", () =>
            {
                value = Result<int>.Success(42).GetValueOrDefault();
            })
            .Then("A successful result returns its value", () => Assert.Equal(42, value))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrDefault_Failure_ReturnsDefault()
    {
        int? value = null;

        return Scenario.Define("Reading a failed result returns the default value", _valueResultHandling)
            .When("GetValueOrDefault reads the default value from a failed result", () =>
            {
                value = Result<int>.Failure(new Error("bad")).GetValueOrDefault();
            })
            .Then("A failed result returns the default value", () => Assert.Equal(0, value))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrDefault_FailureWithFallback_ReturnsFallback()
    {
        string? value = null;

        return Scenario.Define("Reading a failed result returns the supplied fallback", _valueResultHandling)
            .When("GetValueOrDefault reads the explicit fallback from a failed result", () =>
            {
                value = Result<string>.Failure(new Error("bad")).GetValueOrDefault("fallback");
            })
            .Then("A failed result returns the supplied fallback", () => Assert.Equal("fallback", value))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrDefault_SuccessWithFallback_ReturnsValue()
    {
        string? value = null;

        return Scenario.Define("A successful result returns its value instead of the supplied fallback", _valueResultHandling)
            .When("GetValueOrDefault reads the value from a successful result", () =>
            {
                value = Result<string>.Success("value").GetValueOrDefault("fallback");
            })
            .Then("A successful result returns its value", () => Assert.Equal("value", value))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrDefault_NullFallback_ThrowsWithFallbackParameter() =>
        Scenario.Define("Reading a result rejects a null fallback", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("GetValueOrDefault receives a null fallback value", () => _ = Result<string>.Failure(new Error("bad")).GetValueOrDefault(null!))
            .Then("The refusal identifies the fallback parameter", (ArgumentNullException refusal) => Assert.Equal("fallback", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Default_GetValueOrDefault_ThrowsConsistentUninitializedException() =>
        Scenario.Define("An uninitialized result cannot return a default value", _valueResultHandling)
            .WhenThrows<InvalidOperationException>("GetValueOrDefault is called on a default-constructed result", () => _ = default(Result<int>).GetValueOrDefault())
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) => Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();

    [Fact]
    public Task OrElse_Success_ReturnsOriginalAndIgnoresFallback()
    {
        Result<int>? result = null;

        return Scenario.Define("OrElse keeps a successful result and ignores the eager fallback", _valueResultHandling)
            .When("OrElse receives a successful result and an eager fallback", () =>
            {
                result = Result<int>.Success(1).OrElse(Result<int>.Success(2));
            })
            .Then("The original success is returned and the eager fallback is ignored", () => Assert.Equal(1, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_Failure_ReturnsEagerFallback()
    {
        Result<int>? result = null;

        return Scenario.Define("OrElse replaces a failed result with the eager fallback", _valueResultHandling)
            .When("OrElse receives a failed result and an eager fallback", () =>
            {
                result = Result<int>.Failure(new Error("bad")).OrElse(Result<int>.Success(2));
            })
            .Then("The failed result is replaced by the eager fallback", () => Assert.Equal(2, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_Func_Success_DoesNotInvokeFallback()
    {
        var calls = 0;
        Result<int>? result = null;

        return Scenario.Define("OrElse skips the fallback function for a successful result", _valueResultHandling)
            .When("OrElse receives a successful result and a lazy fallback", () =>
            {
                result = Result<int>.Success(1).OrElse(() => { calls++; return Result<int>.Success(2); });
            })
            .Then("The existing success is returned", () => Assert.Equal(1, result?.Value))
            .And("The lazy fallback is not invoked", () => Assert.Equal(0, calls))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_Func_Failure_InvokesFallback()
    {
        var calls = 0;
        Result<int>? result = null;

        return Scenario.Define("OrElse invokes the fallback function for a failed result", _valueResultHandling)
            .When("OrElse receives a failed result and a lazy fallback", () =>
            {
                result = Result<int>.Failure(new Error("bad")).OrElse(() => { calls++; return Result<int>.Success(2); });
            })
            .Then("The fallback is invoked", () => Assert.Equal(1, calls))
            .And("Its successful result is returned", () => Assert.Equal(2, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_FuncNullFallback_ThrowsWithFallbackParameter() =>
        Scenario.Define("OrElse rejects a null fallback function", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("OrElse receives a null lazy fallback", () => _ = Result<int>.Failure(new Error("bad")).OrElse(null!))
            .Then("The refusal identifies the fallback parameter", (ArgumentNullException refusal) => Assert.Equal("fallback", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task OrElseAsync_Success_DoesNotInvokeFallback()
    {
        var calls = 0;
        Result<int>? result = null;

        return Scenario.Define("Asynchronous OrElse skips the fallback for a successful result", _valueResultHandling)
            .When("OrElseAsync receives a successful result and an asynchronous fallback", async () =>
            {
                result = await Result<int>.Success(1).OrElseAsync(() => { calls++; return Task.FromResult(Result<int>.Success(2)); });
            })
            .Then("The existing success is returned", () => Assert.Equal(1, result?.Value))
            .And("The asynchronous fallback is not invoked", () => Assert.Equal(0, calls))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElseAsync_Failure_InvokesFallback()
    {
        var calls = 0;
        Result<int>? result = null;

        return Scenario.Define("Asynchronous OrElse invokes the fallback for a failed result", _valueResultHandling)
            .When("OrElseAsync receives a failed result and an asynchronous fallback", async () =>
            {
                result = await Result<int>.Failure(new Error("bad")).OrElseAsync(() => { calls++; return Task.FromResult(Result<int>.Success(2)); });
            })
            .Then("The asynchronous fallback is invoked", () => Assert.Equal(1, calls))
            .And("Its successful result is returned", () => Assert.Equal(2, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElseAsync_NullFallback_ThrowsWithFallbackParameter() =>
        Scenario.Define("Asynchronous OrElse rejects a null fallback function", _valueResultHandling)
            .WhenThrows<ArgumentNullException>("OrElseAsync receives a null asynchronous fallback", async () => await Result<int>.Failure(new Error("bad")).OrElseAsync(null!))
            .Then("The refusal identifies the fallback parameter", (ArgumentNullException refusal) => Assert.Equal("fallback", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Success_ComplexPayload_PreservesExactInstanceAndNestedData()
    {
        Order order = CreateOrder();
        Result<Order>? result = null;

        return Scenario.Define("A successful result preserves its payload instance and nested data", _valueResultHandling)
            .When("A successful result is created with an Order payload", () =>
            {
                result = Result<Order>.Success(order);
            })
            .Then("The result preserves the Order instance", () => Assert.Same(order, result?.Value))
            .And("The nested customer retains the expected city", () => Assert.Equal("London", result?.Value.Customer.Address.City))
            .And("The order retains both lines", () => Assert.Equal(2, result?.Value.Lines.Count))
            .VerifyAsync();
    }

    [Fact]
    public Task Composition_ComplexPayload_PassesExactInstanceThroughSuccessPath()
    {
        Order order = CreateOrder();
        Order? tapped = null;
        Result<Order>? result = null;

        return Scenario.Define("Composition preserves a successful payload instance through the pipeline", _valueResultHandling)
            .When("A nested Order is passed through a success-only composition", () =>
            {
                result = Result<Order>.Success(order)
                                    .Ensure(value => value.Lines.Count > 0, new Error("empty order"))
                                    .Tap(value => tapped = value)
                                    .Bind(Result<Order>.Success);
            })
            .Then("The observer receives the exact Order instance", () => Assert.Same(order, tapped))
            .And("The result preserves that instance", () => Assert.Same(order, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task Match_ComplexPayload_ProvidesExactInstance()
    {
        Order order = CreateOrder();
        Order? matched = null;

        return Scenario.Define("Matching returns the exact successful payload instance", _valueResultHandling)
            .When("A nested Order is provided to a successful-result handler", () =>
            {
                matched = Result<Order>.Success(order).Match(value => value, _ => throw new Xunit.Sdk.XunitException("Failure branch invoked."));
            })
            .Then("The success handler receives the exact payload instance", () => Assert.Same(order, matched))
            .VerifyAsync();
    }

    [Fact]
    public Task ToOption_ComplexPayload_PreservesExactInstance()
    {
        Order order = CreateOrder();
        Option<Order>? option = null;

        return Scenario.Define("Converting to an option preserves the successful payload instance", _valueResultHandling)
            .When("A nested payload in a successful result is converted to an option", () =>
            {
                option = Result<Order>.Success(order).ToOption();
            })
            .Then("The option contains the exact successful payload instance", () => Assert.Same(order, option?.Value))
            .VerifyAsync();
    }

    private static Order CreateOrder() =>
        new(
            42,
            new Customer("Ada", new Address("London", "UK")),
            [new OrderLine("BOOK", 1), new OrderLine("PEN", 3)]);
}
