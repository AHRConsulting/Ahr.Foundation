namespace Ahr.Foundation.Tests;

public sealed class OptionSequenceTests
{
    private static readonly BusinessContext _optionCollections = BusinessContext
        .Define("option-collections", "Option collections", "Select and retrieve values without representing missing data as a value.")
        .Capability("select-optional-values", "Select an optional value from a sequence or dictionary")
        .Outcome("missing-values-stay-empty", "Missing or null values produce an empty option")
        .Rule("single-selection-is-unambiguous", "Single-value selection rejects more than one match")
        .Build();

    [Fact]
    public Task SingleOrNone_Empty_ReturnsNone()
    {
        Option<int>? selected = null;

        return Scenario.Define("An empty sequence has no single option value", _optionCollections)
            .When("A single value is selected from the empty sequence", () =>
            {
                selected = Array.Empty<int>().SingleOrNone();
            })
            .Then("The selection is empty", () => Assert.True(selected?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task SingleOrNone_OneElement_ReturnsSome()
    {
        Option<int>? selected = null;

        return Scenario.Define("A sequence with one item returns that item as an option", _optionCollections)
            .When("The only item is selected", () =>
            {
                selected = new[] { 7 }.SingleOrNone();
            })
            .Then("The selected value is returned", () => Assert.Equal(7, selected?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task SingleOrNone_NullElement_ReturnsNone()
    {
        Option<string?>? selected = null;

        return Scenario.Define("A null item is not returned as a single option value", _optionCollections)
            .When("The only item is selected", () =>
            {
                selected = new string?[] { null }.SingleOrNone();
            })
            .Then("The selection is empty", () => Assert.True(selected?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task SingleOrNone_MoreThanOneElement_Throws() =>
        Scenario.Define("A sequence with multiple items cannot produce a single option", _optionCollections)
            .WhenThrows<InvalidOperationException>("Single-value selection is attempted on multiple items", () =>
            _ = new[] { 1, 2 }.SingleOrNone())
            .VerifyAsync();

    [Fact]
    public Task SingleOrNone_Predicate_OneMatch_ReturnsSome()
    {
        Option<int>? selected = null;

        return Scenario.Define("A unique predicate match returns its item as an option", _optionCollections)
            .When("An item matching the predicate is selected", () =>
            {
                selected = new[] { 1, 2, 3 }.SingleOrNone(value => value == 2);
            })
            .Then("The matching value is returned", () => Assert.Equal(2, selected?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task SingleOrNone_Predicate_NoMatch_ReturnsNone()
    {
        Option<int>? selected = null;

        return Scenario.Define("A sequence without a predicate match has no single option", _optionCollections)
            .When("No item matches the selection rule", () =>
            {
                selected = new[] { 1, 2 }.SingleOrNone(value => value > 5);
            })
            .Then("The selection is empty", () => Assert.True(selected?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task SingleOrNone_Predicate_ManyMatches_Throws() =>
        Scenario.Define("Multiple predicate matches cannot produce a single option", _optionCollections)
            .WhenThrows<InvalidOperationException>("Single-value selection finds multiple matching items", () =>
            _ = new[] { 1, 2, 3 }.SingleOrNone(value => value > 1))
            .VerifyAsync();

    [Fact]
    public Task SingleOrNone_NullSource_ThrowsWithSourceParameter()
    {
        IEnumerable<int> source = null!;

        return Scenario.Define("Single-value selection requires a source sequence", _optionCollections)
            .WhenThrows<ArgumentNullException>("The source sequence is missing", () =>
            _ = source.SingleOrNone())
            .Then("The refusal identifies the source parameter", (ArgumentNullException refusal) =>
                Assert.Equal("source", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task SingleOrNone_NullPredicate_ThrowsWithPredicateParameter() =>
        Scenario.Define("Predicate selection requires a predicate", _optionCollections)
            .WhenThrows<ArgumentNullException>("The selection predicate is missing", () =>
            _ = new[] { 1 }.SingleOrNone(null!))
            .Then("The refusal identifies the predicate parameter", (ArgumentNullException refusal) =>
                Assert.Equal("predicate", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task LastOrNone_Empty_ReturnsNone()
    {
        Option<int>? selected = null;

        return Scenario.Define("An empty sequence has no last option value", _optionCollections)
            .When("The last value is selected from the empty sequence", () =>
            {
                selected = new List<int>().LastOrNone();
            })
            .Then("The selection is empty", () => Assert.True(selected?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task LastOrNone_List_ReturnsLast()
    {
        Option<int>? selected = null;

        return Scenario.Define("A list returns its last item as an option", _optionCollections)
            .When("The last item is selected", () =>
            {
                selected = new List<int> { 1, 2, 3 }.LastOrNone();
            })
            .Then("The last value is returned", () => Assert.Equal(3, selected?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task LastOrNone_LazySequence_ReturnsLast()
    {
        Option<int>? selected = null;

        return Scenario.Define("A deferred sequence returns its last item as an option", _optionCollections)
            .When("The last item is selected from the deferred sequence", () =>
            {
                selected = Enumerable.Range(1, 3).Select(value => value).LastOrNone();
            })
            .Then("The last value is returned", () => Assert.Equal(3, selected?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task LastOrNone_EmptyLazySequence_ReturnsNone()
    {
        Option<int>? selected = null;

        return Scenario.Define("An empty deferred sequence has no last option value", _optionCollections)
            .When("The last item is selected from the empty sequence", () =>
            {
                selected = Enumerable.Empty<int>().Select(value => value).LastOrNone();
            })
            .Then("The selection is empty", () => Assert.True(selected?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task LastOrNone_NullLastElement_ReturnsNone()
    {
        Option<string?>? selected = null;

        return Scenario.Define("A null last item is not returned as an option value", _optionCollections)
            .When("The last item is selected", () =>
            {
                selected = new string?[] { "a", null }.LastOrNone();
            })
            .Then("The selection is empty", () => Assert.True(selected?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task LastOrNone_Predicate_List_ReturnsLastMatch()
    {
        Option<int>? selected = null;

        return Scenario.Define("A list returns the last item matching the predicate", _optionCollections)
            .When("The last even item is selected", () =>
            {
                selected = new[] { 1, 2, 3, 4, 5 }.LastOrNone(value => value % 2 == 0);
            })
            .Then("The last matching value is returned", () => Assert.Equal(4, selected?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task LastOrNone_Predicate_LazySequence_ReturnsLastMatch()
    {
        Option<int>? selected = null;

        return Scenario.Define("A deferred sequence returns its last predicate match", _optionCollections)
            .When("The last even item is selected from the deferred sequence", () =>
            {
                selected = Enumerable.Range(1, 5).Select(value => value).LastOrNone(value => value % 2 == 0);
            })
            .Then("The last matching value is returned", () => Assert.Equal(4, selected?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task LastOrNone_Predicate_NoMatch_ReturnsNone()
    {
        Option<int>? selected = null;

        return Scenario.Define("A sequence without a last predicate match has no option value", _optionCollections)
            .When("The last even item is selected from a sequence of odd values", () =>
            {
                selected = new[] { 1, 3 }.LastOrNone(value => value % 2 == 0);
            })
            .Then("The selection is empty", () => Assert.True(selected?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task LastOrNone_NullSource_ThrowsWithSourceParameter()
    {
        IEnumerable<int> source = null!;

        return Scenario.Define("Last-value selection requires a source sequence", _optionCollections)
            .WhenThrows<ArgumentNullException>("The source sequence is missing", () =>
            _ = source.LastOrNone())
            .Then("The refusal identifies the source parameter", (ArgumentNullException refusal) =>
                Assert.Equal("source", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task LastOrNone_NullPredicate_ThrowsWithPredicateParameter() =>
        Scenario.Define("Last-value predicate selection requires a predicate", _optionCollections)
            .WhenThrows<ArgumentNullException>("The selection predicate is missing", () =>
            _ = new[] { 1 }.LastOrNone(null!))
            .Then("The refusal identifies the predicate parameter", (ArgumentNullException refusal) =>
                Assert.Equal("predicate", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task GetValueOrNone_KeyPresent_ReturnsSome()
    {
        Option<decimal>? price = null;

        return Scenario.Define("A dictionary lookup returns a present value as an option", _optionCollections)
            .When("The catalog price is looked up", () =>
            {
                var prices = new Dictionary<string, decimal> { ["apple"] = 1.5m };
                price = prices.GetValueOrNone("apple");
            })
            .Then("The price is available", () => Assert.Equal(1.5m, price?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrNone_KeyMissing_ReturnsNone()
    {
        Option<decimal>? price = null;

        return Scenario.Define("A dictionary lookup for a missing key is empty", _optionCollections)
            .When("A price is looked up for an unknown item", () =>
            {
                price = new Dictionary<string, decimal>().GetValueOrNone("pear");
            })
            .Then("No value is returned", () => Assert.True(price?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrNone_NullValue_ReturnsNone()
    {
        Option<string?>? name = null;

        return Scenario.Define("A dictionary lookup with a null value is empty", _optionCollections)
            .When("The stored null name is looked up", () =>
            {
                name = new Dictionary<int, string?> { [1] = null }.GetValueOrNone(1);
            })
            .Then("No value is returned", () => Assert.True(name?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrNone_ReadOnlyDictionary_ReturnsSome()
    {
        Option<string>? name = null;

        return Scenario.Define("A read-only dictionary lookup returns its present value", _optionCollections)
            .When("A name is looked up in the read-only dictionary", () =>
            {
                IReadOnlyDictionary<int, string> names = new System.Collections.ObjectModel.ReadOnlyDictionary<int, string>(
                    new Dictionary<int, string> { [1] = "one" });
                name = names.GetValueOrNone(1);
            })
            .Then("The name is available", () => Assert.Equal("one", name?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task GetValueOrNone_NullDictionary_Throws()
    {
        IReadOnlyDictionary<int, int> dictionary = null!;

        return Scenario.Define("A dictionary lookup requires a dictionary", _optionCollections)
            .WhenThrows<ArgumentNullException>("A key is looked up in a missing dictionary", () =>
            _ = dictionary.GetValueOrNone(1))
            .Then("The refusal identifies the dictionary parameter", (ArgumentNullException refusal) =>
                Assert.Equal("dictionary", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task Values_MixedOptions_ReturnsSomeValuesInOrder()
    {
        IEnumerable<int> expected = [1, 3];
        int[]? values = null;

        return Scenario.Define("Reading option values preserves present-item order", _optionCollections)
            .When("Values are read from a mixed sequence of present and empty options", () =>
            {
                Option<int>[] options = [Option<int>.Some(1), Option<int>.None, Option<int>.Some(3)];
                values = [.. options.Values()];
            })
            .Then("Only present values are returned in order", () => Assert.Equal(expected, values))
            .VerifyAsync();
    }

    [Fact]
    public Task Values_IsDeferred()
    {
        IEnumerable<int> expected = [1, 2, 3];
        var evaluated = 0;
        var evaluatedBeforeEnumeration = -1;
        int[]? values = null;

        return Scenario.Define("Reading option values remains deferred", _optionCollections)
            .When("A deferred values sequence is created and enumerated", () =>
            {
                IEnumerable<int> deferred = Enumerable.Range(1, 3)
                    .Select(value =>
                    {
                        evaluated++;
                        return Option<int>.Some(value);
                    })
                    .Values();
                evaluatedBeforeEnumeration = evaluated;
                values = [.. deferred];
            })
            .Then("Creating the sequence does not evaluate its source", () => Assert.Equal(0, evaluatedBeforeEnumeration))
            .And("Enumeration returns each value and evaluates each item once", () =>
            {
                Assert.Equal(expected, values);
                Assert.Equal(3, evaluated);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Values_NullSource_ThrowsImmediately()
    {
        IEnumerable<Option<int>> source = null!;

        return Scenario.Define("Reading option values requires a source sequence", _optionCollections)
            .WhenThrows<ArgumentNullException>("Option values are read from a missing sequence", () =>
            _ = source.Values())
            .Then("The refusal identifies the source parameter", (ArgumentNullException refusal) =>
                Assert.Equal("source", refusal.ParamName))
            .VerifyAsync();
    }
}
