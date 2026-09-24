namespace Ahr.Foundation.Tests;

public sealed class ResultOfTTests
{
    private sealed record Person(string Name, int Age);
    private sealed record Order(int Id, Customer Customer, IReadOnlyList<OrderLine> Lines);
    private sealed record Customer(string Name, Address Address);
    private sealed record Address(string City, string Country);
    private sealed record OrderLine(string Sku, int Quantity);

    [Fact]
    public void Success_NullValue_ThrowsWithValueParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<string>.Success(null!));
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void Failure_DefaultError_ThrowsWithErrorParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<int>.Failure(default));
        Assert.Equal("error", exception.ParamName);
    }

    [Fact]
    public void Default_Value_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = default(Result<int>).Value);
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void Default_IsSuccess_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = default(Result<int>).IsSuccess);
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void Default_Map_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => default(Result<int>).Map(value => value + 1));
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void Default_BindWithNullDelegate_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => default(Result<int>).Bind<string>(null!));
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void Failure_Value_ThrowsWrongBranchException()
    {
        var result = Result<int>.Failure(new Error("bad"));
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = result.Value);
        Assert.Equal("Cannot access the value of a failed result.", exception.Message);
    }

    [Fact]
    public void Success_Error_ThrowsWrongBranchException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = Result<int>.Success(1).Error);
        Assert.Equal("Cannot access the error of a successful result.", exception.Message);
    }

    [Fact]
    public void Map_RecordPayloadWithExpression_PreservesRecordSemantics()
    {
        Result<Person> result = Result<Person>.Success(new Person("Ada", 30)).Map(person => person with { Age = 31 });
        Assert.Equal(new Person("Ada", 31), result.Value);
    }

    [Fact]
    public void MapError_RecordErrorWithExpression_PreservesRecordSemantics()
    {
        Result<int> result = Result<int>.Failure(new Error("bad")).MapError(error => error with { Message = "worse" });
        Assert.Equal(new Error("worse"), result.Error);
    }

    [Fact]
    public void Map_Failure_DoesNotInvokeMapper()
    {
        var calls = 0;
        Result<string> result = Result<int>.Failure(new Error("bad")).Map(value => { calls++; return value.ToString(); });
        Assert.Equal(0, calls);
        Assert.Equal(new Error("bad"), result.Error);
    }

    [Fact]
    public void Ensure_FalsePredicate_ChangesSuccessToFailure()
    {
        Result<int> result = Result<int>.Success(3).Ensure(value => value > 5, new Error("small"));
        Assert.True(result.IsFailure);
        Assert.Equal(new Error("small"), result.Error);
    }

    [Fact]
    public void Bind_LeftIdentity_Holds()
    {
        static Result<int> AddOne(int value) => Result<int>.Success(value + 1);
        Assert.Equal(AddOne(4), Result<int>.Success(4).Bind(AddOne));
    }

    [Fact]
    public void Bind_RightIdentity_Holds()
    {
        var source = Result<int>.Success(4);
        Assert.Equal(source, source.Bind(Result<int>.Success));
    }

    [Fact]
    public void Map_Identity_Holds()
    {
        var source = Result<int>.Failure(new Error("bad"));
        Assert.Equal(source, source.Map(value => value));
    }

    [Fact]
    public void ExplicitConversion_RoundTrip_PreservesResult()
    {
        var original = Result<int>.Success(42);
        var generic = (Result<int, Error>)original;
        var roundTrip = (Result<int>)generic;
        Assert.Equal(original, roundTrip);
    }

    [Fact]
    public void ExplicitConversion_FromUninitializedGenericResult_ThrowsConsistentException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = (Result<int>)default(Result<int, Error>));
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void ExplicitConversion_ToUninitializedGenericResult_ThrowsConsistentException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = (Result<int, Error>)default(Result<int>));
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void Bind_NullDelegate_ThrowsWithBindParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<int>.Success(1).Bind<string>(null!));
        Assert.Equal("bind", exception.ParamName);
    }

    [Fact]
    public void Equality_EquivalentSuccesses_HaveSameHashCode()
    {
        var left = Result<int>.Success(42);
        var right = Result<int>.Success(42);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Match_Action_Success_InvokesOnlySuccessAction()
    {
        var observed = 0;
        var failureCalled = false;
        var result = Result<int>.Success(42);
        Result<int> returned = result.Match(val => observed = val, _ => failureCalled = true);

        Assert.Equal(42, observed);
        Assert.False(failureCalled);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Match_Action_Failure_InvokesOnlyFailureAction()
    {
        var successCalled = false;
        var observed = default(Error);
        var expected = new Error("err");
        var result = Result<int>.Failure(expected);
        Result<int> returned = result.Match(_ => successCalled = true, err => observed = err);

        Assert.False(successCalled);
        Assert.Equal(expected, observed);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Match_Action_NullSuccessAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<int>.Success(42).Match(null!, _ => { }));
        Assert.Equal("onSuccess", exception.ParamName);
    }

    [Fact]
    public void Match_Action_NullFailureAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<int>.Success(42).Match(_ => { }, null!));
        Assert.Equal("onFailure", exception.ParamName);
    }

    [Fact]
    public async Task MatchAsync_Action_Success_InvokesOnlySuccessAsyncAction()
    {
        var observed = 0;
        var failureCalled = false;
        var result = Result<int>.Success(42);
        Result<int> returned = await result.MatchAsync(val => { observed = val; return Task.CompletedTask; }, _ => { failureCalled = true; return Task.CompletedTask; });

        Assert.Equal(42, observed);
        Assert.False(failureCalled);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task MatchAsync_Action_Failure_InvokesOnlyFailureAsyncAction()
    {
        var successCalled = false;
        var observed = default(Error);
        var expected = new Error("err");
        var result = Result<int>.Failure(expected);
        Result<int> returned = await result.MatchAsync(_ => { successCalled = true; return Task.CompletedTask; }, err => { observed = err; return Task.CompletedTask; });

        Assert.False(successCalled);
        Assert.Equal(expected, observed);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Bind_ToPayloadFreeResult_Success_ReturnsBoundResult()
    {
        var result = Result<int>.Success(42);
        Result bound = result.Bind(val => val > 0 ? Result.Success() : Result.Failure(new Error("neg")));

        Assert.True(bound.IsSuccess);
    }

    [Fact]
    public void Bind_ToPayloadFreeResult_Failure_PropagatesErrorWithoutInvokingBind()
    {
        var invoked = false;
        var result = Result<int>.Failure(new Error("bad"));
        Result bound = result.Bind(_ => { invoked = true; return Result.Success(); });

        Assert.False(invoked);
        Assert.True(bound.IsFailure);
        Assert.Equal(new Error("bad"), bound.Error);
    }

    [Fact]
    public void Bind_ToPayloadFreeResult_NullBind_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<int>.Success(42).Bind(null!));
        Assert.Equal("bind", exception.ParamName);
    }

    [Fact]
    public async Task BindAsync_ToPayloadFreeResult_Success_ReturnsBoundResult()
    {
        var result = Result<int>.Success(42);
        Result bound = await result.BindAsync(val => Task.FromResult(val > 0 ? Result.Success() : Result.Failure(new Error("neg"))));

        Assert.True(bound.IsSuccess);
    }

    [Fact]
    public async Task BindAsync_ToPayloadFreeResult_Failure_PropagatesErrorWithoutInvokingBind()
    {
        var invoked = false;
        var result = Result<int>.Failure(new Error("bad"));
        Result bound = await result.BindAsync(_ => { invoked = true; return Task.FromResult(Result.Success()); });

        Assert.False(invoked);
        Assert.True(bound.IsFailure);
        Assert.Equal(new Error("bad"), bound.Error);
    }

    [Fact]
    public async Task BindAsync_ToPayloadFreeResult_NullBind_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result<int>.Success(42).BindAsync(null!));
        Assert.Equal("bind", exception.ParamName);
    }

    [Fact]
    public void Tap_Success_InvokesActionAndReturnsSelf()
    {
        var observed = 0;
        var result = Result<int>.Success(42);
        Result<int> returned = result.Tap(val => observed = val);

        Assert.Equal(42, observed);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Tap_Failure_DoesNotInvokeAction()
    {
        var tapped = false;
        var result = Result<int>.Failure(new Error("err"));
        Result<int> returned = result.Tap(_ => tapped = true);

        Assert.False(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void Tap_NullAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<int>.Success(42).Tap(null!));
        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public async Task TapAsync_Success_InvokesAsyncActionAndReturnsSelf()
    {
        var observed = 0;
        var result = Result<int>.Success(42);
        Result<int> returned = await result.TapAsync(val => { observed = val; return Task.CompletedTask; });

        Assert.Equal(42, observed);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task TapAsync_Failure_DoesNotInvokeAsyncAction()
    {
        var tapped = false;
        var result = Result<int>.Failure(new Error("err"));
        Result<int> returned = await result.TapAsync(_ => { tapped = true; return Task.CompletedTask; });

        Assert.False(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task TapAsync_NullAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result<int>.Success(42).TapAsync(null!));
        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public void TapError_Success_DoesNotInvokeAction()
    {
        var tapped = false;
        var result = Result<int>.Success(42);
        Result<int> returned = result.TapError(_ => tapped = true);

        Assert.False(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void TapError_Failure_InvokesAction()
    {
        Error observed = default;
        Error error = new("err");
        var result = Result<int>.Failure(error);
        Result<int> returned = result.TapError(err => observed = err);

        Assert.Equal(error, observed);
        Assert.Equal(result, returned);
    }

    [Fact]
    public void TapError_NullAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<int>.Failure(new Error("err")).TapError(null!));
        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public async Task TapErrorAsync_Failure_InvokesAsyncAction()
    {
        Error observed = default;
        Error error = new("err");
        var result = Result<int>.Failure(error);
        Result<int> returned = await result.TapErrorAsync(err => { observed = err; return Task.CompletedTask; });

        Assert.Equal(error, observed);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task TapErrorAsync_Success_DoesNotInvokeAsyncAction()
    {
        var tapped = false;
        var result = Result<int>.Success(42);
        Result<int> returned = await result.TapErrorAsync(_ => { tapped = true; return Task.CompletedTask; });

        Assert.False(tapped);
        Assert.Equal(result, returned);
    }

    [Fact]
    public async Task TapErrorAsync_NullAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result<int>.Failure(new Error("err")).TapErrorAsync(null!));
        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public void ToOption_Success_ReturnsSome()
    {
        var option = Result<int>.Success(42).ToOption();
        Assert.True(option.IsSome);
        Assert.Equal(42, option.Value);
    }

    [Fact]
    public void ToOption_Failure_ReturnsNone()
    {
        var option = Result<int>.Failure(new Error("bad")).ToOption();
        Assert.True(option.IsNone);
    }

    [Fact]
    public void GetValueOrDefault_Success_ReturnsValue() => Assert.Equal(42, Result<int>.Success(42).GetValueOrDefault());

    [Fact]
    public void GetValueOrDefault_Failure_ReturnsDefault() => Assert.Equal(0, Result<int>.Failure(new Error("bad")).GetValueOrDefault());

    [Fact]
    public void GetValueOrDefault_FailureWithFallback_ReturnsFallback() => Assert.Equal("fallback", Result<string>.Failure(new Error("bad")).GetValueOrDefault("fallback"));

    [Fact]
    public void GetValueOrDefault_SuccessWithFallback_ReturnsValue() => Assert.Equal("value", Result<string>.Success("value").GetValueOrDefault("fallback"));

    [Fact]
    public void GetValueOrDefault_NullFallback_ThrowsWithFallbackParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<string>.Failure(new Error("bad")).GetValueOrDefault(null!));
        Assert.Equal("fallback", exception.ParamName);
    }

    [Fact]
    public void Default_GetValueOrDefault_ThrowsConsistentUninitializedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => default(Result<int>).GetValueOrDefault());
        Assert.Equal("The result is uninitialized. Create it with Success or Failure.", exception.Message);
    }

    [Fact]
    public void OrElse_Success_ReturnsOriginalAndIgnoresFallback()
    {
        Result<int> result = Result<int>.Success(1).OrElse(Result<int>.Success(2));
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public void OrElse_Failure_ReturnsEagerFallback()
    {
        Result<int> result = Result<int>.Failure(new Error("bad")).OrElse(Result<int>.Success(2));
        Assert.Equal(2, result.Value);
    }

    [Fact]
    public void OrElse_Func_Success_DoesNotInvokeFallback()
    {
        var calls = 0;
        Result<int> result = Result<int>.Success(1).OrElse(() => { calls++; return Result<int>.Success(2); });
        Assert.Equal(1, result.Value);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void OrElse_Func_Failure_InvokesFallback()
    {
        var calls = 0;
        Result<int> result = Result<int>.Failure(new Error("bad")).OrElse(() => { calls++; return Result<int>.Success(2); });
        Assert.Equal(2, result.Value);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void OrElse_FuncNullFallback_ThrowsWithFallbackParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Result<int>.Failure(new Error("bad")).OrElse(null!));
        Assert.Equal("fallback", exception.ParamName);
    }

    [Fact]
    public async Task OrElseAsync_Success_DoesNotInvokeFallback()
    {
        var calls = 0;
        Result<int> result = await Result<int>.Success(1).OrElseAsync(() => { calls++; return Task.FromResult(Result<int>.Success(2)); });
        Assert.Equal(1, result.Value);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task OrElseAsync_Failure_InvokesFallback()
    {
        var calls = 0;
        Result<int> result = await Result<int>.Failure(new Error("bad")).OrElseAsync(() => { calls++; return Task.FromResult(Result<int>.Success(2)); });
        Assert.Equal(2, result.Value);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task OrElseAsync_NullFallback_ThrowsWithFallbackParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Result<int>.Failure(new Error("bad")).OrElseAsync(null!));
        Assert.Equal("fallback", exception.ParamName);
    }

    [Fact]
    public void Success_ComplexPayload_PreservesExactInstanceAndNestedData()
    {
        Order order = CreateOrder();

        var result = Result<Order>.Success(order);

        Assert.Same(order, result.Value);
        Assert.Equal("London", result.Value.Customer.Address.City);
        Assert.Equal(2, result.Value.Lines.Count);
    }

    [Fact]
    public void Composition_ComplexPayload_PassesExactInstanceThroughSuccessPath()
    {
        Order order = CreateOrder();
        Order? tapped = null;

        Result<Order> result = Result<Order>.Success(order)
            .Ensure(value => value.Lines.Count > 0, new Error("empty order"))
            .Tap(value => tapped = value)
            .Bind(Result<Order>.Success);

        Assert.Same(order, tapped);
        Assert.Same(order, result.Value);
    }

    [Fact]
    public void Match_ComplexPayload_ProvidesExactInstance()
    {
        Order order = CreateOrder();

        Order matched = Result<Order>.Success(order).Match(value => value, _ => throw new Xunit.Sdk.XunitException("Failure branch invoked."));

        Assert.Same(order, matched);
    }

    [Fact]
    public void ToOption_ComplexPayload_PreservesExactInstance()
    {
        Order order = CreateOrder();

        var option = Result<Order>.Success(order).ToOption();

        Assert.Same(order, option.Value);
    }

    private static Order CreateOrder() =>
        new(
            42,
            new Customer("Ada", new Address("London", "UK")),
            [new OrderLine("BOOK", 1), new OrderLine("PEN", 3)]);
}
