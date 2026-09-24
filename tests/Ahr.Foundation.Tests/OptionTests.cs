namespace Ahr.Foundation.Tests;

public sealed class OptionTests
{
    private sealed record Catalog(string Name, CatalogMetadata Metadata, IReadOnlyList<Product> Products);
    private sealed record CatalogMetadata(string Region, IReadOnlyDictionary<string, string> Tags);
    private sealed record Product(string Sku, decimal Price);

    [Fact]
    public void None_DefaultValue_IsEmpty()
    {
        Option<int> option = default;
        Assert.True(option.IsNone);
        Assert.False(option.IsSome);
        Assert.Equal(0, option.GetValueOrDefault());
    }

    [Fact]
    public void Some_NullValue_ThrowsWithValueParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Option<string>.Some(null!));
        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void None_Value_ThrowsExpectedException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = Option<int>.None.Value);
        Assert.Equal("Cannot access the value of an empty option.", exception.Message);
    }

    [Fact]
    public void Map_Some_TransformsValue()
    {
        Option<string> result = Option<int>.Some(21).Map(value => (value * 2).ToString());
        Assert.Equal("42", result.Value);
    }

    [Fact]
    public void Map_None_DoesNotInvokeMapper()
    {
        var calls = 0;
        Option<string> result = Option<int>.None.Map(value => { calls++; return value.ToString(); });
        Assert.Equal(0, calls);
        Assert.True(result.IsNone);
    }

    [Fact]
    public void Bind_Some_UsesReturnedOption()
    {
        Option<int> result = Option<int>.Some(2).Bind(value => Option<int>.Some(value * 3));
        Assert.Equal(6, result.Value);
    }

    [Fact]
    public void Where_FalsePredicate_ReturnsNone() => Assert.True(Option<int>.Some(2).Where(value => value > 5).IsNone);

    [Fact]
    public void Match_NullNoneDelegate_ThrowsWithOnNoneParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Option<int>.Some(1).Match(_ => 1, null!));
        Assert.Equal("onNone", exception.ParamName);
    }

    [Fact]
    public void GetValueOrDefault_None_ReturnsFallback() => Assert.Equal("fallback", Option<string>.None.GetValueOrDefault("fallback"));

    [Fact]
    public void GetValueOrDefault_NullFallback_ThrowsWithFallbackParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Option<string>.None.GetValueOrDefault(null!));
        Assert.Equal("fallback", exception.ParamName);
    }

    [Fact]
    public void ToResult_None_ReturnsProvidedFailure()
    {
        var result = Option<int>.None.ToResult("missing");
        Assert.Equal("missing", result.Error);
    }

    [Fact]
    public void ToResult_NullError_ThrowsWithErrorParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Option<int>.None.ToResult<string>(null!));
        Assert.Equal("error", exception.ParamName);
    }

    [Fact]
    public void ToOption_NullReference_ReturnsNone()
    {
        string? value = null;
        Assert.True(value.ToOption().IsNone);
    }

    [Fact]
    public void FirstOrNone_Sequence_ReturnsFirstValue()
    {
        Option<int> option = new[] { 4, 5 }.FirstOrNone();
        Assert.Equal(4, option.Value);
    }

    [Fact]
    public void FirstOrNone_NullSource_ThrowsWithSourceParameter()
    {
        IEnumerable<int> source = null!;
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => source.FirstOrNone());
        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void FirstOrNone_NullPredicate_ThrowsWithPredicateParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new[] { 1 }.FirstOrNone(null!));
        Assert.Equal("predicate", exception.ParamName);
    }

    [Fact]
    public void Equality_EquivalentSomeValues_HaveSameHashCode()
    {
        var left = Option<int>.Some(9);
        var right = Option<int>.Some(9);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Some_ComplexPayload_PreservesExactInstanceAndNestedData()
    {
        Catalog catalog = CreateCatalog();

        var option = Option<Catalog>.Some(catalog);

        Assert.Same(catalog, option.Value);
        Assert.Equal("eu-west", option.Value.Metadata.Region);
        Assert.Equal(2, option.Value.Products.Count);
    }

    [Fact]
    public void Composition_ComplexPayload_PassesExactInstanceThroughSomePath()
    {
        Catalog catalog = CreateCatalog();

        Option<Catalog> option = Option<Catalog>.Some(catalog)
            .Where(value => value.Products.Count > 0)
            .Bind(Option<Catalog>.Some);
        Catalog matched = option.Match(value => value, () => throw new Xunit.Sdk.XunitException("None branch invoked."));

        Assert.Same(catalog, matched);
    }

    [Fact]
    public void Map_ComplexPayload_UsesNestedData()
    {
        Catalog catalog = CreateCatalog();

        Option<decimal> option = Option<Catalog>.Some(catalog)
            .Map(value => value.Products.Sum(product => product.Price));

        Assert.Equal(15.50m, option.Value);
    }

    [Fact]
    public void ToResult_ComplexPayload_PreservesExactInstanceForBothErrorForms()
    {
        Catalog catalog = CreateCatalog();
        var option = Option<Catalog>.Some(catalog);

        var standard = option.ToResult(new Error("missing"));
        var custom = option.ToResult("missing");

        Assert.Same(catalog, standard.Value);
        Assert.Same(catalog, custom.Value);
    }

    private static Catalog CreateCatalog() =>
        new(
            "Autumn",
            new CatalogMetadata("eu-west", new Dictionary<string, string> { ["channel"] = "online" }),
            [new Product("BOOK", 12.50m), new Product("PEN", 3m)]);

    [Fact]
    public void Match_Action_Some_InvokesOnlySomeAction()
    {
        var observed = 0;
        var noneCalled = false;
        var option = Option<int>.Some(42);
        Option<int> returned = option.Match(v => observed = v, () => noneCalled = true);

        Assert.Equal(42, observed);
        Assert.False(noneCalled);
        Assert.Equal(option, returned);
    }

    [Fact]
    public void Match_Action_None_InvokesOnlyNoneAction()
    {
        var someCalled = false;
        var noneCalled = false;
        Option<int> option = Option<int>.None;
        Option<int> returned = option.Match(_ => { someCalled = true; }, () => noneCalled = true);

        Assert.False(someCalled);
        Assert.True(noneCalled);
        Assert.Equal(option, returned);
    }

    [Fact]
    public void Match_Action_NullSomeAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Option<int>.Some(1).Match(null!, () => { }));
        Assert.Equal("onSome", exception.ParamName);
    }

    [Fact]
    public void Match_Action_NullNoneAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Option<int>.Some(1).Match(_ => { }, null!));
        Assert.Equal("onNone", exception.ParamName);
    }

    [Fact]
    public async Task MatchAsync_Value_Some_InvokesOnlySomeBranchAndReturnsItsValue()
    {
        var noneCalled = false;

        var value = await Option<int>.Some(21).MatchAsync(
            some => Task.FromResult((some * 2).ToString()),
            () =>
            {
                noneCalled = true;
                return Task.FromResult("none");
            });

        Assert.Equal("42", value);
        Assert.False(noneCalled);
    }

    [Fact]
    public async Task MatchAsync_Value_None_InvokesOnlyNoneBranchAndReturnsItsValue()
    {
        var someCalled = false;

        var value = await Option<int>.None.MatchAsync(
            some =>
            {
                someCalled = true;
                return Task.FromResult(some.ToString());
            },
            () => Task.FromResult("none"));

        Assert.Equal("none", value);
        Assert.False(someCalled);
    }

    [Fact]
    public async Task MatchAsync_Value_NullSomeDelegate_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Option<int>.Some(1).MatchAsync(null!, () => Task.FromResult("none")));

        Assert.Equal("onSome", exception.ParamName);
    }

    [Fact]
    public async Task MatchAsync_Value_NullNoneDelegate_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Option<int>.Some(1).MatchAsync(value => Task.FromResult(value.ToString()), null!));

        Assert.Equal("onNone", exception.ParamName);
    }

    [Fact]
    public async Task MatchAsync_Value_SomeBranchReturnsNullTask_ThrowsWithOnSomeParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Option<int>.Some(1).MatchAsync(_ => null!, () => Task.FromResult("none")));

        Assert.Equal("onSome", exception.ParamName);
    }

    [Fact]
    public async Task MatchAsync_Value_NoneBranchReturnsNullTask_ThrowsWithOnNoneParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => Option<int>.None.MatchAsync(_ => Task.FromResult("some"), () => null!));

        Assert.Equal("onNone", exception.ParamName);
    }

    [Fact]
    public async Task MatchAsync_Value_ActiveBranchThrows_PropagatesException()
    {
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Option<int>.Some(1).MatchAsync(
                _ => Task.FromException<string>(new InvalidOperationException("failed")),
                () => Task.FromResult("none")));

        Assert.Equal("failed", exception.Message);
    }

    [Fact]
    public async Task MatchAsync_Action_Some_InvokesOnlySomeAsyncAction()
    {
        var observed = 0;
        var noneCalled = false;
        var option = Option<int>.Some(42);
        Option<int> returned = await option.MatchAsync(v => { observed = v; return Task.CompletedTask; }, () => { noneCalled = true; return Task.CompletedTask; });

        Assert.Equal(42, observed);
        Assert.False(noneCalled);
        Assert.Equal(option, returned);
    }

    [Fact]
    public async Task MatchAsync_Action_None_InvokesOnlyNoneAsyncAction()
    {
        var someCalled = false;
        var noneCalled = false;
        Option<int> option = Option<int>.None;
        Option<int> returned = await option.MatchAsync(_ => { someCalled = true; return Task.CompletedTask; }, () => { noneCalled = true; return Task.CompletedTask; });

        Assert.False(someCalled);
        Assert.True(noneCalled);
        Assert.Equal(option, returned);
    }

    [Fact]
    public void Tap_Some_InvokesActionAndReturnsSelf()
    {
        var observed = 0;
        var option = Option<int>.Some(42);
        Option<int> returned = option.Tap(v => observed = v);

        Assert.Equal(42, observed);
        Assert.Equal(option, returned);
    }

    [Fact]
    public void Tap_None_DoesNotInvokeAction()
    {
        var tapped = false;
        Option<int> option = Option<int>.None;
        Option<int> returned = option.Tap(_ => tapped = true);

        Assert.False(tapped);
        Assert.Equal(option, returned);
    }

    [Fact]
    public void Tap_NullAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Option<int>.Some(1).Tap(null!));
        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public async Task TapAsync_Some_InvokesAsyncActionAndReturnsSelf()
    {
        var observed = 0;
        var option = Option<int>.Some(42);
        Option<int> returned = await option.TapAsync(v => { observed = v; return Task.CompletedTask; });

        Assert.Equal(42, observed);
        Assert.Equal(option, returned);
    }

    [Fact]
    public async Task TapAsync_None_DoesNotInvokeAsyncAction()
    {
        var tapped = false;
        Option<int> option = Option<int>.None;
        Option<int> returned = await option.TapAsync(_ => { tapped = true; return Task.CompletedTask; });

        Assert.False(tapped);
        Assert.Equal(option, returned);
    }

    [Fact]
    public async Task TapAsync_NullAction_ThrowsWithExactParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Option<int>.Some(1).TapAsync(null!));
        Assert.Equal("action", exception.ParamName);
    }

    [Fact]
    public void ToResult_WithStandardError_Some_ReturnsSuccess()
    {
        Error error = new("not found");
        var result = Option<int>.Some(42).ToResult(error);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void ToResult_WithStandardError_None_ReturnsFailure()
    {
        Error error = new("not found");
        var result = Option<int>.None.ToResult(error);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void ToResult_WithDefaultStandardError_ThrowsWithErrorParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Option<int>.None.ToResult(default));
        Assert.Equal("error", exception.ParamName);
    }

    [Fact]
    public void ToResult_WithCustomError_Some_ReturnsSuccess()
    {
        var result = Option<int>.Some(42).ToResult("error");
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void OrElse_Some_ReturnsOriginalAndIgnoresFallback() => Assert.Equal(1, Option<int>.Some(1).OrElse(Option<int>.Some(2)).Value);

    [Fact]
    public void OrElse_None_ReturnsEagerFallback() => Assert.Equal(2, Option<int>.None.OrElse(Option<int>.Some(2)).Value);

    [Fact]
    public void OrElse_None_ReturnsFallbackNone() => Assert.True(Option<int>.None.OrElse(Option<int>.None).IsNone);

    [Fact]
    public void OrElse_Func_Some_DoesNotInvokeFallback()
    {
        var calls = 0;
        Option<int> result = Option<int>.Some(1).OrElse(() => { calls++; return Option<int>.Some(2); });
        Assert.Equal(1, result.Value);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void OrElse_Func_None_InvokesFallback()
    {
        var calls = 0;
        Option<int> result = Option<int>.None.OrElse(() => { calls++; return Option<int>.Some(2); });
        Assert.Equal(2, result.Value);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void OrElse_FuncNullFallback_ThrowsWithFallbackParameter()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => Option<int>.None.OrElse(null!));
        Assert.Equal("fallback", exception.ParamName);
    }

    [Fact]
    public async Task OrElseAsync_Some_DoesNotInvokeFallback()
    {
        var calls = 0;
        Option<int> result = await Option<int>.Some(1).OrElseAsync(() => { calls++; return Task.FromResult(Option<int>.Some(2)); });
        Assert.Equal(1, result.Value);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task OrElseAsync_None_InvokesFallback()
    {
        var calls = 0;
        Option<int> result = await Option<int>.None.OrElseAsync(() => { calls++; return Task.FromResult(Option<int>.Some(2)); });
        Assert.Equal(2, result.Value);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task OrElseAsync_NullFallback_ThrowsWithFallbackParameter()
    {
        ArgumentNullException exception = await Assert.ThrowsAsync<ArgumentNullException>(() => Option<int>.None.OrElseAsync(null!));
        Assert.Equal("fallback", exception.ParamName);
    }
}
