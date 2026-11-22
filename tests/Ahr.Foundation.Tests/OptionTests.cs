namespace Ahr.Foundation.Tests;

public sealed class OptionTests
{
    private static readonly BusinessContext _optionalValues = BusinessContext
        .Define("optional-values", "Optional values", "Represent values that may be absent and safely compose operations over them.")
        .Capability("handle-optional-data", "Transform, select, and recover optional values")
        .Outcome("absence-is-explicit", "An absent value remains distinct from a present value")
        .Rule("invalid-operations-are-rejected", "Invalid inputs are rejected with the relevant parameter")
        .Build();

    private sealed record Catalog(string Name, CatalogMetadata Metadata, IReadOnlyList<Product> Products);
    private sealed record CatalogMetadata(string Region, IReadOnlyDictionary<string, string> Tags);
    private sealed record Product(string Sku, decimal Price);

    [Fact]
    public Task None_DefaultValue_IsEmpty()
    {
        Option<int>? option = null;

        return Scenario.Define("A default option is empty", _optionalValues)
            .When("The default Option value is inspected for its empty state", () =>
            {
                option = default(Option<int>);
            })
            .Then("The default option represents absence", () => Assert.True(option?.IsNone))
            .And("The default option is not present", () => Assert.False(option?.IsSome))
            .And("The default option returns the default value", () => Assert.Equal(0, option?.GetValueOrDefault()))
            .VerifyAsync();
    }

    [Fact]
    public Task Some_NullValue_ThrowsWithValueParameter() =>
        Scenario.Define("A present option rejects a null reference", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller creates an option from a null value", () => _ = Option<string>.Some(null!))
            .Then("The refusal identifies the value argument", (ArgumentNullException refusal) => Assert.Equal("value", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task None_Value_ThrowsExpectedException() =>
        Scenario.Define("Reading an empty option value is rejected", _optionalValues)
            .WhenThrows<InvalidOperationException>("A caller reads the value of an empty option", () => _ = Option<int>.None.Value)
            .Then("The option explains that it is empty", (InvalidOperationException exception) => Assert.Equal("Cannot access the value of an empty option.", exception.Message))
            .VerifyAsync();

    [Fact]
    public Task Map_Some_TransformsValue()
    {
        Option<string>? result = null;

        return Scenario.Define("Mapping a present option transforms its value", _optionalValues)
            .When("Map transforms the value 21 in a present option", () =>
            {
                result = Option<int>.Some(21).Map(value => (value * 2).ToString());
            })
            .Then("The present value is transformed", () => Assert.Equal("42", result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task Map_None_DoesNotInvokeMapper()
    {
        var calls = 0;
        Option<string>? result = null;

        return Scenario.Define("Mapping an empty option skips the mapper", _optionalValues)
            .When("Map receives an empty option and a mapper with a call counter", () =>
            {
                result = Option<int>.None.Map(value => { calls++; return value.ToString(); });
            })
            .Then("The mapper is skipped", () => Assert.Equal(0, calls))
            .And("The option remains empty", () => Assert.True(result?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task Bind_Some_UsesReturnedOption()
    {
        Option<int>? result = null;

        return Scenario.Define("Binding a present option returns the binder's option", _optionalValues)
            .When("Bind continues the value 2 with a continuation that returns Some(6)", () =>
            {
                result = Option<int>.Some(2).Bind(value => Option<int>.Some(value * 3));
            })
            .Then("Binding returns the option produced by the continuation", () => Assert.Equal(6, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task Where_FalsePredicate_ReturnsNone()
    {
        Option<int>? result = null;

        return Scenario.Define("Filtering out a value returns an empty option", _optionalValues)
            .When("Where applies a predicate that rejects the present value 2", () =>
            {
                result = Option<int>.Some(2).Where(value => value > 5);
            })
            .Then("The rejected value becomes None", () => Assert.True(result?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task Match_NullNoneDelegate_ThrowsWithOnNoneParameter() =>
        Scenario.Define("Option value matching requires an absence handler", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller matches an option without an absence handler", () => _ = Option<int>.Some(1).Match(_ => 1, null!))
            .Then("The refusal identifies the on-none handler", (ArgumentNullException refusal) => Assert.Equal("onNone", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task GetValueOrDefault_None_ReturnsFallback()
    {
        string? value = null;

        return Scenario.Define("An empty option returns the supplied default value", _optionalValues)
            .When("GetValueOrDefault reads the explicit fallback from an empty option", () =>
            {
                value = Option<string>.None.GetValueOrDefault("fallback");
            })
            .Then("The empty option returns the supplied fallback", () => Assert.Equal("fallback", value))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrDefault_NullFallback_ThrowsWithFallbackParameter() =>
        Scenario.Define("An empty option requires a valid fallback value", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller supplies a null fallback", () => _ = Option<string>.None.GetValueOrDefault(null!))
            .Then("The refusal identifies the fallback argument", (ArgumentNullException refusal) => Assert.Equal("fallback", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task ToResult_None_ReturnsProvidedFailure()
    {
        Result<int, string>? result = null;

        return Scenario.Define("Converting an empty option returns the supplied failure", _optionalValues)
            .When("ToResult converts an empty option using the supplied missing-value error", () =>
            {
                result = Option<int>.None.ToResult("missing");
            })
            .Then("An empty option becomes a failure with the supplied error", () => Assert.Equal("missing", result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task ToResult_NullError_ThrowsWithErrorParameter() =>
        Scenario.Define("Converting an empty option requires a failure value", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller converts an empty option with a null error", () => _ = Option<int>.None.ToResult<string>(null!))
            .Then("The refusal identifies the error argument", (ArgumentNullException refusal) => Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task ToOption_NullReference_ReturnsNone()
    {
        Option<string>? result = null;

        return Scenario.Define("A null reference converts to an empty option", _optionalValues)
            .When("ToOption receives a null reference", () =>
            {
                string? value = null;
                result = value.ToOption();
            })
            .Then("The null reference becomes None", () => Assert.True(result?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task FirstOrNone_Sequence_ReturnsFirstValue()
    {
        Option<int>? option = null;

        return Scenario.Define("FirstOrNone returns the first sequence item", _optionalValues)
            .When("FirstOrNone selects the first item from a two-item sequence", () =>
            {
                option = new[] { 4, 5 }.FirstOrNone();
            })
            .Then("The first sequence item is returned", () => Assert.Equal(4, option?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task FirstOrNone_NullSource_ThrowsWithSourceParameter()
    {
        IEnumerable<int> source = null!;

        return Scenario.Define("Selecting an option requires a source sequence", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller selects the first option from a null sequence", () => _ = source.FirstOrNone())
            .Then("The refusal identifies the source sequence", (ArgumentNullException refusal) => Assert.Equal("source", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task FirstOrNone_NullPredicate_ThrowsWithPredicateParameter() =>
        Scenario.Define("Predicate-based selection requires a predicate", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller selects from a sequence without a predicate", () => _ = new[] { 1 }.FirstOrNone(null!))
            .Then("The refusal identifies the predicate", (ArgumentNullException refusal) => Assert.Equal("predicate", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Equality_EquivalentSomeValues_HaveSameHashCode()
    {
        var left = Option<int>.Some(9);
        var right = Option<int>.Some(9);
        bool? areEqual = null;
        bool? hashesMatch = null;

        return Scenario.Define("Equivalent present options have equal hash codes", _optionalValues)
            .When("The options holding the same value are compared", () =>
            {
                areEqual = left.Equals(right);
                hashesMatch = left.GetHashCode() == right.GetHashCode();
            })
            .Then("Equivalent present options are equal", () => Assert.True(areEqual))
            .And("Equivalent present options have equal hash codes", () => Assert.True(hashesMatch))
            .VerifyAsync();
    }

    [Fact]
    public Task Some_ComplexPayload_PreservesExactInstanceAndNestedData()
    {
        Catalog catalog = CreateCatalog();
        Option<Catalog>? option = null;

        return Scenario.Define("A present option preserves its payload instance and nested data", _optionalValues)
            .When("Some receives a Catalog with nested product data", () =>
            {
                option = Option<Catalog>.Some(catalog);
            })
            .Then("The same catalog instance is preserved", () => Assert.Same(catalog, option?.Value))
            .And("The catalog region is preserved", () => Assert.Equal("eu-west", option?.Value.Metadata.Region))
            .And("The nested product collection is preserved", () => Assert.Equal(2, option?.Value.Products.Count))
            .VerifyAsync();
    }

    [Fact]
    public Task Composition_ComplexPayload_PassesExactInstanceThroughSomePath()
    {
        Catalog catalog = CreateCatalog();
        Catalog? matched = null;

        return Scenario.Define("Composition preserves a present payload instance", _optionalValues)
            .When("A Catalog passes through a successful Some composition", () =>
            {
                Option<Catalog> option = Option<Catalog>.Some(catalog)
                    .Where(value => value.Products.Count > 0)
                    .Bind(Option<Catalog>.Some);
                matched = option.Match(value => value, () => throw new Xunit.Sdk.XunitException("None branch invoked."));
            })
            .Then("The Some branch passes the exact catalog instance through composition", () => Assert.Same(catalog, matched))
            .VerifyAsync();
    }

    [Fact]
    public Task Map_ComplexPayload_UsesNestedData()
    {
        Option<decimal>? option = null;

        return Scenario.Define("Mapping a present payload reads nested data", _optionalValues)
            .When("Map reads product data nested in a Catalog option", () =>
            {
                Catalog catalog = CreateCatalog();
                option = Option<Catalog>.Some(catalog)
                    .Map(value => value.Products.Sum(product => product.Price));
            })
            .Then("Mapping reads the nested product data", () => Assert.Equal(15.50m, option?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task ToResult_ComplexPayload_PreservesExactInstanceForBothErrorForms()
    {
        Catalog catalog = CreateCatalog();
        Result<Catalog>? standard = null;
        Result<Catalog, string>? custom = null;

        return Scenario.Define("Converting a present option preserves its payload for both error types", _optionalValues)
            .When("ToResult converts a Catalog option using each supported error form", () =>
            {
                var option = Option<Catalog>.Some(catalog);

                standard = option.ToResult(new Error("missing"));
                custom = option.ToResult("missing");
            })
            .Then("The standard error form preserves the exact catalog instance", () => Assert.Same(catalog, standard?.Value))
            .And("The custom error form preserves the exact catalog instance", () => Assert.Same(catalog, custom?.Value))
            .VerifyAsync();
    }

    private static Catalog CreateCatalog() =>
        new(
            "Autumn",
            new CatalogMetadata("eu-west", new Dictionary<string, string> { ["channel"] = "online" }),
            [new Product("BOOK", 12.50m), new Product("PEN", 3m)]);

    [Fact]
    public Task Match_Action_Some_InvokesOnlySomeAction()
    {
        var observed = 0;
        var noneCalled = false;
        var option = Option<int>.Some(42);
        Option<int>? returned = null;

        return Scenario.Define("Matching a present option invokes only the present-value action", _optionalValues)
            .When("Match receives a present option and present/absence action handlers", () =>
            {
                returned = option.Match(v => observed = v, () => noneCalled = true);
            })
            .Then("The present-value action receives the option value", () => Assert.Equal(42, observed))
            .And("The absence action does not run", () => Assert.False(noneCalled))
            .And("Match returns the original option", () => Assert.Equal(option, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task Match_Action_None_InvokesOnlyNoneAction()
    {
        var someCalled = false;
        var noneCalled = false;
        Option<int> option = Option<int>.None;
        Option<int>? returned = null;

        return Scenario.Define("Matching an empty option invokes only the absence action", _optionalValues)
            .When("Match receives an empty option and present/absence action handlers", () =>
            {
                returned = option.Match(_ => { someCalled = true; }, () => noneCalled = true);
            })
            .Then("The present-value action does not run", () => Assert.False(someCalled))
            .And("The absence action runs", () => Assert.True(noneCalled))
            .And("Match returns the original empty option", () => Assert.Equal(option, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task Match_Action_NullSomeAction_ThrowsWithExactParameter() =>
        Scenario.Define("Option matching requires a handler for present values", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller matches an option without a present-value action", () => _ = Option<int>.Some(1).Match(null!, () => { }))
            .Then("The refusal identifies the on-some handler", (ArgumentNullException refusal) => Assert.Equal("onSome", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Match_Action_NullNoneAction_ThrowsWithExactParameter() =>
        Scenario.Define("Option action matching requires an absence action", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller matches an option without an absence action", () => _ = Option<int>.Some(1).Match(_ => { }, null!))
            .Then("The refusal identifies the on-none handler", (ArgumentNullException refusal) => Assert.Equal("onNone", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_Value_Some_InvokesOnlySomeBranchAndReturnsItsValue()
    {
        var noneCalled = false;
        string? value = null;

        return Scenario.Define("Asynchronous matching returns the present-value handler's result", _optionalValues)
            .When("MatchAsync receives a present option and value-returning asynchronous handlers", async () =>
            {
                value = await Option<int>.Some(21).MatchAsync(
                    some => Task.FromResult((some * 2).ToString()),
                    () =>
                    {
                        noneCalled = true;
                        return Task.FromResult("none");
                    });
            })
            .Then("The present-value handler returns its value", () => Assert.Equal("42", value))
            .And("The absence handler does not run", () => Assert.False(noneCalled))
            .VerifyAsync();
    }

    [Fact]
    public Task MatchAsync_Value_None_InvokesOnlyNoneBranchAndReturnsItsValue()
    {
        var someCalled = false;
        string? value = null;

        return Scenario.Define("Asynchronous matching returns the absence handler's result", _optionalValues)
            .When("MatchAsync receives an empty option and value-returning asynchronous handlers", async () =>
            {
                value = await Option<int>.None.MatchAsync(
                    some =>
                    {
                        someCalled = true;
                        return Task.FromResult(some.ToString());
                    },
                    () => Task.FromResult("none"));
            })
            .Then("The absence handler returns its value", () => Assert.Equal("none", value))
            .And("The present-value handler does not run", () => Assert.False(someCalled))
            .VerifyAsync();
    }

    [Fact]
    public Task MatchAsync_Value_NullSomeDelegate_ThrowsWithExactParameter() =>
        Scenario.Define("Asynchronous option matching requires a present-value handler", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller matches an option without an asynchronous present-value handler", async () => await Option<int>.Some(1).MatchAsync(null!, () => Task.FromResult("none")))
            .Then("The refusal identifies the on-some handler", (ArgumentNullException refusal) => Assert.Equal("onSome", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_Value_NullNoneDelegate_ThrowsWithExactParameter() =>
        Scenario.Define("Asynchronous option matching requires an absence handler", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller matches an option without an asynchronous absence handler", async () => await Option<int>.Some(1).MatchAsync(value => Task.FromResult(value.ToString()), null!))
            .Then("The refusal identifies the on-none handler", (ArgumentNullException refusal) => Assert.Equal("onNone", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_Value_SomeBranchReturnsNullTask_ThrowsWithOnSomeParameter() =>
        Scenario.Define("A present-value handler must return a task", _optionalValues)
            .WhenThrows<ArgumentNullException>("A present-value handler returns no task", async () => await Option<int>.Some(1).MatchAsync(_ => null!, () => Task.FromResult("none")))
            .Then("The refusal identifies the on-some handler", (ArgumentNullException refusal) => Assert.Equal("onSome", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_Value_NoneBranchReturnsNullTask_ThrowsWithOnNoneParameter() =>
        Scenario.Define("An absence handler must return a task", _optionalValues)
            .WhenThrows<ArgumentNullException>("An absence handler returns no task", async () => await Option<int>.None.MatchAsync(_ => Task.FromResult("some"), () => null!))
            .Then("The refusal identifies the on-none handler", (ArgumentNullException refusal) => Assert.Equal("onNone", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_Value_ActiveBranchThrows_PropagatesException() =>
        Scenario.Define("Asynchronous matching propagates failure from the active branch", _optionalValues)
            .WhenThrows<InvalidOperationException>("The active present-value handler fails", async () => await Option<int>.Some(1).MatchAsync(_ => Task.FromException<string>(new InvalidOperationException("failed")), () => Task.FromResult("none")))
            .Then("The original branch failure is preserved", (InvalidOperationException exception) => Assert.Equal("failed", exception.Message))
            .VerifyAsync();

    [Fact]
    public Task MatchAsync_Action_Some_InvokesOnlySomeAsyncAction()
    {
        var observed = 0;
        var noneCalled = false;
        var option = Option<int>.Some(42);
        Option<int>? returned = null;

        return Scenario.Define("Asynchronous matching invokes only the present-value action", _optionalValues)
            .When("MatchAsync receives a present option and asynchronous action handlers", async () =>
            {
                returned = await option.MatchAsync(v => { observed = v; return Task.CompletedTask; }, () => { noneCalled = true; return Task.CompletedTask; });
            })
            .Then("The present-value action receives the option value", () => Assert.Equal(42, observed))
            .And("The absence action does not run", () => Assert.False(noneCalled))
            .And("MatchAsync returns the original option", () => Assert.Equal(option, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task MatchAsync_Action_None_InvokesOnlyNoneAsyncAction()
    {
        var someCalled = false;
        var noneCalled = false;
        Option<int> option = Option<int>.None;
        Option<int>? returned = null;

        return Scenario.Define("Asynchronous matching invokes only the absence action", _optionalValues)
            .When("MatchAsync receives an empty option and asynchronous action handlers", async () =>
            {
                returned = await option.MatchAsync(_ => { someCalled = true; return Task.CompletedTask; }, () => { noneCalled = true; return Task.CompletedTask; });
            })
            .Then("The present-value action does not run", () => Assert.False(someCalled))
            .And("The absence action runs", () => Assert.True(noneCalled))
            .And("MatchAsync returns the original empty option", () => Assert.Equal(option, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_Some_InvokesActionAndReturnsSelf()
    {
        var observed = 0;
        var option = Option<int>.Some(42);
        Option<int>? returned = null;

        return Scenario.Define("Tap observes a present option and returns it unchanged", _optionalValues)
            .When("Tap observes a present option value", () =>
            {
                returned = option.Tap(v => observed = v);
            })
            .Then("The action observes the present value", () => Assert.Equal(42, observed))
            .And("Tap returns the original option", () => Assert.Equal(option, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_None_DoesNotInvokeAction()
    {
        var tapped = false;
        Option<int> option = Option<int>.None;
        Option<int>? returned = null;

        return Scenario.Define("Tap skips its action for an empty option", _optionalValues)
            .When("Tap receives an empty option and an action", () =>
            {
                returned = option.Tap(_ => tapped = true);
            })
            .Then("The action is skipped", () => Assert.False(tapped))
            .And("Tap returns the original empty option", () => Assert.Equal(option, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task Tap_NullAction_ThrowsWithExactParameter() =>
        Scenario.Define("Observing an option requires an action", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller observes a present option without an action", () => _ = Option<int>.Some(1).Tap(null!))
            .Then("The refusal identifies the action", (ArgumentNullException refusal) => Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TapAsync_Some_InvokesAsyncActionAndReturnsSelf()
    {
        var observed = 0;
        var option = Option<int>.Some(42);
        Option<int>? returned = null;

        return Scenario.Define("Asynchronous Tap observes a present option and returns it unchanged", _optionalValues)
            .When("TapAsync observes a present option value", async () =>
            {
                returned = await option.TapAsync(v => { observed = v; return Task.CompletedTask; });
            })
            .Then("The asynchronous action observes the present value", () => Assert.Equal(42, observed))
            .And("TapAsync returns the original option", () => Assert.Equal(option, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task TapAsync_None_DoesNotInvokeAsyncAction()
    {
        var tapped = false;
        Option<int> option = Option<int>.None;
        Option<int>? returned = null;

        return Scenario.Define("Asynchronous Tap skips its action for an empty option", _optionalValues)
            .When("TapAsync receives an empty option and an asynchronous action", async () =>
            {
                returned = await option.TapAsync(_ => { tapped = true; return Task.CompletedTask; });
            })
            .Then("The asynchronous action is skipped", () => Assert.False(tapped))
            .And("TapAsync returns the original empty option", () => Assert.Equal(option, returned))
            .VerifyAsync();
    }

    [Fact]
    public Task TapAsync_NullAction_ThrowsWithExactParameter() =>
        Scenario.Define("Asynchronous option observation requires an action", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller asynchronously observes an option without an action", async () => await Option<int>.Some(1).TapAsync(null!))
            .Then("The refusal identifies the action", (ArgumentNullException refusal) => Assert.Equal("action", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task ToResult_WithStandardError_Some_ReturnsSuccess()
    {
        Result<int>? result = null;

        return Scenario.Define("Converting a present option produces a successful result with the standard error type", _optionalValues)
            .When("ToResult converts a present option using the standard Error type", () =>
            {
                Error error = new("not found");
                result = Option<int>.Some(42).ToResult(error);
            })
            .Then("The present value becomes a successful result", () => Assert.True(result?.IsSuccess))
            .And("The successful result contains the present value", () => Assert.Equal(42, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task ToResult_WithStandardError_None_ReturnsFailure()
    {
        Error error = new("not found");
        Result<int>? result = null;

        return Scenario.Define("Converting an empty option produces a failed result with the standard error type", _optionalValues)
            .When("ToResult converts an empty option using the standard Error type", () =>
            {
                result = Option<int>.None.ToResult(error);
            })
            .Then("The empty option becomes a failure", () => Assert.True(result?.IsFailure))
            .And("The failure contains the supplied standard error", () => Assert.Equal(error, result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task ToResult_WithDefaultStandardError_ThrowsWithErrorParameter() =>
        Scenario.Define("Converting an empty option requires a valid standard error", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller converts an empty option using a default error", () => _ = Option<int>.None.ToResult(default))
            .Then("The refusal identifies the error argument", (ArgumentNullException refusal) => Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task ToResult_WithCustomError_Some_ReturnsSuccess()
    {
        Result<int, string>? result = null;

        return Scenario.Define("Converting a present option produces a successful result with the custom error type", _optionalValues)
            .When("ToResult converts a present option using a custom error type", () =>
            {
                result = Option<int>.Some(42).ToResult("error");
            })
            .Then("The present value becomes a successful result", () => Assert.True(result?.IsSuccess))
            .And("The successful result contains the present value", () => Assert.Equal(42, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_Some_ReturnsOriginalAndIgnoresFallback()
    {
        Option<int>? result = null;

        return Scenario.Define("OrElse keeps a present option and skips the fallback", _optionalValues)
            .When("OrElse receives a present option and an eager fallback", () =>
            {
                result = Option<int>.Some(1).OrElse(Option<int>.Some(2));
            })
            .Then("The original present value is returned", () => Assert.Equal(1, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_None_ReturnsEagerFallback()
    {
        Option<int>? result = null;

        return Scenario.Define("OrElse returns the eager fallback for an empty option", _optionalValues)
            .When("OrElse receives an empty option and an eager fallback", () =>
            {
                result = Option<int>.None.OrElse(Option<int>.Some(2));
            })
            .Then("The eager fallback becomes the value of the empty option", () => Assert.Equal(2, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_None_ReturnsFallbackNone()
    {
        Option<int>? result = null;

        return Scenario.Define("OrElse can return an empty fallback option", _optionalValues)
            .When("OrElse receives an empty option and an empty fallback", () =>
            {
                result = Option<int>.None.OrElse(Option<int>.None);
            })
            .Then("The empty option returns the supplied fallback", () => Assert.True(result?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_Func_Some_DoesNotInvokeFallback()
    {
        var calls = 0;
        Option<int>? result = null;

        return Scenario.Define("OrElse skips the fallback function for a present option", _optionalValues)
            .When("OrElse receives a present option and a lazy fallback", () =>
            {
                result = Option<int>.Some(1).OrElse(() => { calls++; return Option<int>.Some(2); });
            })
            .Then("The original present value is returned", () => Assert.Equal(1, result?.Value))
            .And("The fallback function is not invoked", () => Assert.Equal(0, calls))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_Func_None_InvokesFallback()
    {
        var calls = 0;
        Option<int>? result = null;

        return Scenario.Define("OrElse invokes the fallback function for an empty option", _optionalValues)
            .When("OrElse receives an empty option and a lazy fallback", () =>
            {
                result = Option<int>.None.OrElse(() => { calls++; return Option<int>.Some(2); });
            })
            .Then("The fallback function provides the missing value", () => Assert.Equal(2, result?.Value))
            .And("The fallback function runs once", () => Assert.Equal(1, calls))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElse_FuncNullFallback_ThrowsWithFallbackParameter() =>
        Scenario.Define("An empty option requires a fallback function", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller supplies no fallback function", () => _ = Option<int>.None.OrElse(null!))
            .Then("The refusal identifies the fallback", (ArgumentNullException refusal) => Assert.Equal("fallback", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task OrElseAsync_Some_DoesNotInvokeFallback()
    {
        var calls = 0;
        Option<int>? result = null;

        return Scenario.Define("Asynchronous OrElse skips the fallback for a present option", _optionalValues)
            .When("OrElseAsync receives a present option and an asynchronous fallback", async () =>
            {
                result = await Option<int>.Some(1).OrElseAsync(() => { calls++; return Task.FromResult(Option<int>.Some(2)); });
            })
            .Then("The original present value is returned", () => Assert.Equal(1, result?.Value))
            .And("The asynchronous fallback is not invoked", () => Assert.Equal(0, calls))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElseAsync_None_InvokesFallback()
    {
        var calls = 0;
        Option<int>? result = null;

        return Scenario.Define("Asynchronous OrElse invokes the fallback for an empty option", _optionalValues)
            .When("OrElseAsync receives an empty option and an asynchronous fallback", async () =>
            {
                result = await Option<int>.None.OrElseAsync(() => { calls++; return Task.FromResult(Option<int>.Some(2)); });
            })
            .Then("The asynchronous fallback provides the missing value", () => Assert.Equal(2, result?.Value))
            .And("The asynchronous fallback runs once", () => Assert.Equal(1, calls))
            .VerifyAsync();
    }

    [Fact]
    public Task OrElseAsync_NullFallback_ThrowsWithFallbackParameter() =>
        Scenario.Define("Asynchronous option recovery requires a fallback function", _optionalValues)
            .WhenThrows<ArgumentNullException>("A caller asynchronously recovers without a fallback function", async () => await Option<int>.None.OrElseAsync(null!))
            .Then("The refusal identifies the fallback", (ArgumentNullException refusal) => Assert.Equal("fallback", refusal.ParamName))
            .VerifyAsync();
}
