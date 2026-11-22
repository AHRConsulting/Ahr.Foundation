namespace Ahr.Foundation.Tests;

public sealed class FormattingTests
{
    private static readonly Error _failure = Error.FromMessage("not found");

    private static readonly BusinessContext _valueDescriptions = BusinessContext
        .Define("value-descriptions", "Value descriptions", "Make Foundation values safe and understandable in logs and messages.")
        .Capability("describe-functional-values", "Describe result, error, and option states")
        .Outcome("readable-state", "Every state has a stable human-readable description")
        .Rule("formatting-never-throws", "Formatting an uninitialized value does not throw")
        .Build();

    public static TheoryData<string, object, string> FormattedStates => new()
    {
        { "payload-free result success", Result.Success(), "Success" },
        { "payload-free result failure", Result.Failure(_failure), "Failure(not found)" },
        { "uninitialized payload-free result", default(Result), "Uninitialized" },
        { "generic result success", Result<int>.Success(42), "Success(42)" },
        { "generic result failure", Result<int>.Failure(_failure), "Failure(not found)" },
        { "uninitialized generic result", default(Result<int>), "Uninitialized" },
        { "custom-error result success", Result<int, int>.Success(42), "Success(42)" },
        { "custom-error result failure", Result<int, int>.Failure(404), "Failure(404)" },
        { "uninitialized custom-error result", default(Result<int, int>), "Uninitialized" },
        { "option with a value", Option<int>.Some(42), "Some(42)" },
        { "empty option", Option<int>.None, "None" },
    };

    [Theory]
    [MemberData(nameof(FormattedStates))]
    public Task ToString_EveryState_ReturnsReadableText(string exampleLabel, object value, string expected)
    {
        string? description = null;

        return Scenario.Define("Foundation functional values have readable descriptions", _valueDescriptions)
            .When($"The {exampleLabel} is converted to text", () =>
            {
                description = value.ToString();
            })
            .Then("The description matches the value's state", () => Assert.Equal(expected, description))
            .VerifyAsync();
    }

    [Fact]
    public Task Interpolation_MixedStates_UsesToString()
    {
        string? text = null;

        return Scenario.Define("Interpolated results and options remain readable", _valueDescriptions)
            .When("A success, failure, and empty option are interpolated", () =>
            {
                text = $"{Result.Success()} {Result<int>.Failure(_failure)} {Option<string>.None}";
            })
            .Then("Each value contributes its readable description", () =>
                Assert.Equal("Success Failure(not found) None", text))
            .VerifyAsync();
    }

    [Fact]
    public Task Error_Default_Message_IsEmpty()
    {
        Error error = default;
        string? message = null;
        string? description = null;

        return Scenario.Define("A default error can be safely described", _valueDescriptions)
            .When("The default error is read and formatted", () =>
            {
                message = error.Message;
                description = error.ToString();
            })
            .Then("Its message and description are empty", () =>
            {
                Assert.Equal(string.Empty, message);
                Assert.Equal(string.Empty, description);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Failure_DefaultErrorOnPayloadFreeResult_ThrowsWithErrorParameter() =>
        Scenario.Define("A result failure cannot be created from a default error", _valueDescriptions)
            .WhenThrows<ArgumentNullException>("The payload-free result receives a default error", () =>
            _ = Result.Failure(default))
            .Then("The invalid error is rejected", (ArgumentNullException refusal) =>
                Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Failure_DefaultErrorOnGenericResult_ThrowsWithErrorParameter() =>
        Scenario.Define("A generic result failure cannot be created from a default error", _valueDescriptions)
            .WhenThrows<ArgumentNullException>("The generic result receives a default error", () =>
            _ = Result<int>.Failure(default))
            .Then("The invalid error is rejected", (ArgumentNullException refusal) =>
                Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task ToResult_DefaultError_ThrowsWithErrorParameter() =>
        Scenario.Define("An empty option requires a valid error when converted to a result", _valueDescriptions)
            .WhenThrows<ArgumentNullException>("The empty option is converted with a default error", () =>
            _ = Option<int>.None.ToResult(new Error()))
            .Then("The invalid error is rejected", (ArgumentNullException refusal) =>
                Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task FirstOrNone_NullFirstElement_ReturnsNone()
    {
        Option<string?>? first = null;

        return Scenario.Define("A null first item is not exposed as an option value", _valueDescriptions)
            .When("The first item is selected from a sequence containing null", () =>
            {
                first = new string?[] { null, "a" }.FirstOrNone();
            })
            .Then("The selection is empty", () => Assert.True(first?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task FirstOrNone_PredicateMatchesNull_ReturnsNone()
    {
        Option<string?>? match = null;

        return Scenario.Define("A predicate match on null does not create an option value", _valueDescriptions)
            .When("A predicate selects a null item from the sequence", () =>
            {
                match = new string?[] { "a", null }.FirstOrNone(item => item is null);
            })
            .Then("The selection is empty", () => Assert.True(match?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task ToOption_NullableValueType_ReturnsOptionOfUnderlyingType()
    {
        Option<int>? presentOption = null;
        Option<int>? missingOption = null;

        return Scenario.Define("Nullable value types map to value-type options", _valueDescriptions)
            .When("Present and missing values are converted to options", () =>
            {
                int? present = 5;
                int? missing = null;
                presentOption = present.ToOption();
                missingOption = missing.ToOption();
            })
            .Then("The present value is preserved", () => Assert.Equal(5, presentOption?.Value))
            .And("The missing value becomes an empty option", () =>
                Assert.True(missingOption?.IsNone))
            .VerifyAsync();
    }
}
