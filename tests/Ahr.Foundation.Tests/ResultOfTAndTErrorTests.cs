namespace Ahr.Foundation.Tests;

public sealed class ResultOfTAndTErrorTests
{
    private sealed record Problem(string Code, int Severity);
    private sealed record Request(string Route, RequestContext Context, IReadOnlyDictionary<string, string> Headers);
    private sealed record RequestContext(string CorrelationId, User User);
    private sealed record User(string Id, IReadOnlyList<string> Roles);
    private sealed record DomainProblem(string Code, ProblemContext Context);
    private sealed record ProblemContext(string Operation, IReadOnlyList<string> Details);

    [Fact]
    public void Success_NullValue_ThrowsWithValueParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<string, Problem>.Success(null!));
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Failure_NullError_ThrowsWithErrorParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<int, Problem>.Failure(null!));
        Assert.Equal("error", exception.ParamName);
    }

    [Fact]
    public void Default_IsSuccess_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = default(Result<int, string>).IsSuccess);
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void Map_Success_TransformsValue()
    {
        Result<string, Problem> result = Result<int, Problem>.Success(21).Map(value => (value * 2).ToString());
        Assert.Equal("42", result.Value);
    }

    [Fact]
    public void MapError_RecordErrorWithExpression_TransformsError()
    {
        var original = new Problem("E1", 1);
        Result<int, Problem> result = Result<int, Problem>.Failure(original).MapError(error => error with { Severity = 2 });
        Assert.Equal(new Problem("E1", 2), result.Error);
    }

    [Fact]
    public void Ensure_ExistingFailure_DoesNotInvokePredicate()
    {
        var calls = 0;
        Result<int, string> result = Result<int, string>.Failure("bad").Ensure(_ => { calls++; return true; }, "other");
        Assert.Equal(0, calls);
        Assert.Equal("bad", result.Error);
    }

    [Fact]
    public void ToOption_Success_ReturnsSome()
    {
        var option = Result<int, string>.Success(7).ToOption();
        Assert.True(option.IsSome);
        Assert.Equal(7, option.Value);
    }

    [Fact]
    public void ToOption_Failure_ReturnsNone()
    {
        var option = Result<int, string>.Failure("bad").ToOption();
        Assert.True(option.IsNone);
    }

    [Fact]
    public void GetValueOrDefault_Success_ReturnsValue() => Assert.Equal(7, Result<int, string>.Success(7).GetValueOrDefault());

    [Fact]
    public void GetValueOrDefault_Failure_ReturnsDefault() => Assert.Equal(0, Result<int, string>.Failure("bad").GetValueOrDefault());

    [Fact]
    public void GetValueOrDefault_FailureWithFallback_ReturnsFallback() => Assert.Equal("fallback", Result<string, string>.Failure("bad").GetValueOrDefault("fallback"));

    [Fact]
    public void GetValueOrDefault_SuccessWithFallback_ReturnsValue() => Assert.Equal("value", Result<string, string>.Success("value").GetValueOrDefault("fallback"));

    [Fact]
    public void GetValueOrDefault_NullFallback_ThrowsWithFallbackParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<string, string>.Failure("bad").GetValueOrDefault(null!));
        Assert.Equal("fallback", exception.ParamName);
    }

    [Fact]
    public void Default_GetValueOrDefault_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => default(Result<int, string>).GetValueOrDefault());
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void OrElse_Success_ReturnsOriginalAndIgnoresFallback()
    {
        Result<int, string> result = Result<int, string>.Success(1).OrElse(Result<int, string>.Success(2));
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public void OrElse_Failure_ReturnsEagerFallback()
    {
        Result<int, string> result = Result<int, string>.Failure("bad").OrElse(Result<int, string>.Success(2));
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void OrElse_Func_Success_DoesNotInvokeFallback()
    {
        var calls = 0;
        Result<int, string> result = Result<int, string>.Success(1).OrElse(() => { calls++; return Result<int, string>.Success(2); });
        Assert.Equal(1, result.Value);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void OrElse_Func_Failure_InvokesFallback()
    {
        var calls = 0;
        Result<int, string> result = Result<int, string>.Failure("bad").OrElse(() => { calls++; return Result<int, string>.Success(2); });
        Assert.Equal(2, result.Value);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void OrElse_FuncNullFallback_ThrowsWithFallbackParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<int, string>.Failure("bad").OrElse(null!));
        Assert.Equal("fallback", exception.ParamName);
    }

    [Fact]
    public void OrElse_Func_Default_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => default(Result<int, string>).OrElse(() => Result<int, string>.Success(1)));
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public async Task OrElseAsync_Success_DoesNotInvokeFallback()
    {
        var calls = 0;
        Result<int, string> result = await Result<int, string>.Success(1).OrElseAsync(() => { calls++; return Task.FromResult(Result<int, string>.Success(2)); });
        Assert.Equal(1, result.Value);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task OrElseAsync_Failure_InvokesFallback()
    {
        var calls = 0;
        Result<int, string> result = await Result<int, string>.Failure("bad").OrElseAsync(() => { calls++; return Task.FromResult(Result<int, string>.Success(2)); });
        Assert.Equal(2, result.Value);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task OrElseAsync_NullFallback_ThrowsWithFallbackParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result<int, string>.Failure("bad").OrElseAsync(null!));
        Assert.Equal("fallback", exception.ParamName);
    }

    [Fact]
    public void Map_NullDelegate_ThrowsWithMapParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<int, string>.Success(1).Map<string>(null!));
        Assert.Equal("map", exception.ParamName);
    }

    [Fact]
    public void Ensure_NullError_ThrowsWithErrorParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<int, string>.Success(1).Ensure(_ => true, null!));
        Assert.Equal("error", exception.ParamName);
    }

    [Fact]
    public void Bind_Associativity_Holds()
    {
        static Result<int, string> Double(int value) => Result<int, string>.Success(value * 2);
        static Result<string, string> Format(int value) => Result<string, string>.Success($"#{value}");
        var source = Result<int, string>.Success(5);
        Assert.Equal(source.Bind(Double).Bind(Format), source.Bind(value => Double(value).Bind(Format)));
    }

    [Fact]
    public void Default_Value_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = default(Result<int, string>).Value);
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void Default_Error_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = default(Result<int, string>).Error);
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void Default_IsFailure_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = default(Result<int, string>).IsFailure);
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void Success_Error_ThrowsWrongBranchException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = Result<int, string>.Success(1).Error);
        Assert.Equal("Cannot access the error of a successful result.", exception.Message);
    }

    [Fact]
    public void Failure_Value_ThrowsWrongBranchException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = Result<int, string>.Failure("err").Value);
        Assert.Equal("Cannot access the value of a failed result.", exception.Message);
    }

    [Fact]
    public void Match_Func_Success_InvokesSuccessBranch()
    {
        var result = Result<int, string>.Success(42).Match(v => $"ok: {v}", e => $"err: {e}");
        Assert.Equal("ok: 42", result);
    }

    [Fact]
    public void Match_Func_Failure_InvokesFailureBranch()
    {
        var result = Result<int, string>.Failure("not found").Match(v => $"ok: {v}", e => $"err: {e}");
        Assert.Equal("err: not found", result);
    }

    [Fact]
    public void Match_Action_Success_InvokesOnlySuccessAction()
    {
        var observed = 0;
        var failureCalled = false;
        var result = Result<int, string>.Success(42);
        Result<int, string> returned = result.Match(v => observed = v, _ => failureCalled = true);

        Assert.Equal(42, observed);
        Assert.False(failureCalled);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Match_Action_Failure_InvokesOnlyFailureAction()
    {
        var successCalled = false;
        var observed = "";
        var result = Result<int, string>.Failure("err");
        Result<int, string> returned = result.Match(_ => successCalled = true, e => observed = e);

        Assert.False(successCalled);
        Assert.Equal("err", observed);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task MatchAsync_Action_Success_InvokesOnlySuccessAsyncAction()
    {
        var observed = 0;
        var failureCalled = false;
        var result = Result<int, string>.Success(42);
        Result<int, string> returned = await result.MatchAsync(v => { observed = v; return Task.CompletedTask; }, _ => { failureCalled = true; return Task.CompletedTask; });

        Assert.Equal(42, observed);
        Assert.False(failureCalled);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task MatchAsync_Action_Failure_InvokesOnlyFailureAsyncAction()
    {
        var successCalled = false;
        var observed = "";
        var result = Result<int, string>.Failure("err");
        Result<int, string> returned = await result.MatchAsync(_ => { successCalled = true; return Task.CompletedTask; }, e => { observed = e; return Task.CompletedTask; });

        Assert.False(successCalled);
        Assert.Equal("err", observed);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Tap_Success_InvokesActionAndReturnsSelf()
    {
        var observed = 0;
        var result = Result<int, string>.Success(42);
        Result<int, string> returned = result.Tap(v => observed = v);

        Assert.Equal(42, observed);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Tap_Failure_DoesNotInvokeAction()
    {
        var tapped = false;
        var result = Result<int, string>.Failure("err");
        Result<int, string> returned = result.Tap(_ => tapped = true);

        Assert.False(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task TapAsync_Success_InvokesAsyncActionAndReturnsSelf()
    {
        var observed = 0;
        var result = Result<int, string>.Success(42);
        Result<int, string> returned = await result.TapAsync(v => { observed = v; return Task.CompletedTask; });

        Assert.Equal(42, observed);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task TapAsync_Failure_DoesNotInvokeAsyncAction()
    {
        var tapped = false;
        var result = Result<int, string>.Failure("err");
        Result<int, string> returned = await result.TapAsync(_ => { tapped = true; return Task.CompletedTask; });

        Assert.False(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void TapError_Success_DoesNotInvokeAction()
    {
        var tapped = false;
        var result = Result<int, string>.Success(42);
        Result<int, string> returned = result.TapError(_ => tapped = true);

        Assert.False(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void TapError_Failure_InvokesAction()
    {
        var observed = "";
        var result = Result<int, string>.Failure("err");
        Result<int, string> returned = result.TapError(e => observed = e);

        Assert.Equal("err", observed);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task TapErrorAsync_Failure_InvokesAsyncAction()
    {
        var observed = "";
        var result = Result<int, string>.Failure("err");
        Result<int, string> returned = await result.TapErrorAsync(e => { observed = e; return Task.CompletedTask; });

        Assert.Equal("err", observed);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task TapErrorAsync_Success_DoesNotInvokeAsyncAction()
    {
        var tapped = false;
        var result = Result<int, string>.Success(42);
        Result<int, string> returned = await result.TapErrorAsync(_ => { tapped = true; return Task.CompletedTask; });

        Assert.False(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Equality_EquivalentSuccesses_HaveSameHashCode()
    {
        var left = Result<int, string>.Success(42);
        var right = Result<int, string>.Success(42);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equality_EquivalentFailures_HaveSameHashCode()
    {
        var left = Result<int, string>.Failure("bad");
        var right = Result<int, string>.Failure("bad");
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Success_ComplexPayload_PreservesExactInstanceThroughBind()
    {
        Request request = CreateRequest();

        Result<Request, DomainProblem> result = Result<Request, DomainProblem>.Success(request)
            .Bind(Result<Request, DomainProblem>.Success);

        Assert.Same(request, result.Value);
        Assert.Equal("admin", Assert.Single(result.Value.Context.User.Roles));
        Assert.Equal("application/json", result.Value.Headers["Accept"]);
    }

    [Fact]
    public void Match_ComplexPayload_ProvidesExactInstance()
    {
        Request request = CreateRequest();

        Request matched = Result<Request, DomainProblem>.Success(request)
            .Match(value => value, _ => throw new Xunit.Sdk.XunitException("Failure branch invoked."));

        Assert.Same(request, matched);
    }

    [Fact]
    public void Failure_ComplexError_ShortCircuitsAndPreservesExactInstance()
    {
        DomainProblem problem = CreateProblem();
        var bindCalled = false;

        Result<Request, DomainProblem> result = Result<Request, DomainProblem>.Failure(problem)
            .Bind(value =>
            {
                bindCalled = true;
                return Result<Request, DomainProblem>.Success(value);
            });

        Assert.False(bindCalled);
        Assert.Same(problem, result.Error);
    }

    [Fact]
    public void MapError_ComplexError_TransformsNestedDataWithoutChangingOriginal()
    {
        DomainProblem problem = CreateProblem();

        Result<Request, DomainProblem> result = Result<Request, DomainProblem>.Failure(problem)
            .MapError(error => error with
            {
                Context = error.Context with
                {
                    Details = [.. error.Context.Details, "retryable"]
                }
            });

        Assert.Equal(["timeout"], problem.Context.Details);
        Assert.Equal(["timeout", "retryable"], result.Error.Context.Details);
    }

    private static Request CreateRequest() =>
        new(
            "/orders",
            new RequestContext("correlation-42", new User("user-7", ["admin"])),
            new Dictionary<string, string> { ["Accept"] = "application/json" });

    private static DomainProblem CreateProblem() =>
        new("gateway-timeout", new ProblemContext("submit-order", ["timeout"]));
}
