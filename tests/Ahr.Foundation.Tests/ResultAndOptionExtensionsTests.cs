namespace Ahr.Foundation.Tests;

public sealed class ResultAndOptionExtensionsTests
{
    private static readonly BusinessContext _resultConversions = BusinessContext
        .Define("result-conversions", "Result conversions", "Represent successful operations and failures without losing their useful details.")
        .Capability("record-successful-values", "Represent a completed operation with its value")
        .Capability("record-failure-details", "Represent failures from messages and exceptions")
        .Outcome("success-values-preserved", "A successful result retains the supplied value")
        .Outcome("failure-details-preserved", "A failure retains its message and originating exception")
        .Rule("required-inputs-are-explicit", "Required values, messages, and exceptions cannot be missing")
        .Build();

    private static readonly BusinessContext _optionalValues = BusinessContext
        .Define("optional-value-extensions", "Optional value extensions", "Represent available values explicitly while keeping absent values empty.")
        .Capability("convert-optional-values", "Convert nullable values into optional values")
        .Capability("select-first-match", "Select the first available value from a sequence")
        .Outcome("present-values-preserved", "Available values are returned without being changed")
        .Outcome("absent-values-stay-empty", "Missing or unmatched values remain empty")
        .Rule("sequence-inputs-are-required", "Sequence operations require a source and any supplied predicate")
        .Build();

    private static readonly BusinessContext _documentPayloads = BusinessContext
        .Define("document-payloads", "Document payloads", "Move rich document data through success and optional-value boundaries without copying it.")
        .Capability("preserve-document-payloads", "Retain a document payload when converting it")
        .Outcome("document-identity-preserved", "The exact document instance remains available")
        .Rule("first-match-is-returned", "A sequence selection returns the first matching document")
        .Build();

    private sealed record Document(string Id, DocumentMetadata Metadata, IReadOnlyList<string> Sections);
    private sealed record DocumentMetadata(string Author, IReadOnlyDictionary<string, string> Attributes);

    [Fact]
    public Task ToSuccess_NonNullValue_ReturnsSuccess()
    {
        Result<int>? result = null;

        return Scenario.Define("A completed operation records its successful value", _resultConversions)
            .When("The value is converted to a result", () => result = 42.ToSuccess())
            .Then("The result contains the supplied value", () => Assert.Equal(42, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task ToSuccess_NullValue_ThrowsWithValueParameter()
    {
        string value = null!;

        return Scenario.Define("A missing value cannot be recorded as a success", _resultConversions)
            .WhenThrows<ArgumentNullException>("The null value is converted to a result", () => _ = value.ToSuccess())
            .Then("The refusal identifies the value parameter", (ArgumentNullException refusal) => Assert.Equal("value", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task ToSuccess_CustomError_NonNullValue_ReturnsSuccess()
    {
        Result<int, string>? result = null;

        return Scenario.Define("A successful result can use a domain-specific error type", _resultConversions)
            .When("The value is converted to a result with a custom error type", () => result = 42.ToSuccess<int, string>())
            .Then("The result is successful", () => Assert.True(result?.IsSuccess))
            .And("The supplied value remains available", () => Assert.Equal(42, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task ToSuccess_CustomError_NullValue_ThrowsWithValueParameter()
    {
        string value = null!;

        return Scenario.Define("A custom-error success still requires a value", _resultConversions)
            .WhenThrows<ArgumentNullException>("The null value is converted to a custom-error result", () => _ = value.ToSuccess<string, int>())
            .Then("The refusal identifies the value parameter", (ArgumentNullException refusal) => Assert.Equal("value", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_ResultFromError_ReturnsFailure()
    {
        Error error = new("failure");
        Result? result = null;

        return Scenario.Define("An existing error can be represented as a result failure", _resultConversions)
            .When("The error is converted to a failure result", () => result = error.ToFailure())
            .Then("The result represents a failure", () => Assert.True(result?.IsFailure))
            .And("The original error is retained", () => Assert.Equal(error, result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_ResultOfTFromError_ReturnsFailure()
    {
        Error error = new("failure");
        Result<int>? result = null;

        return Scenario.Define("An existing error can fail a value-bearing result", _resultConversions)
            .When("The error is converted to a value-bearing failure result", () => result = error.ToFailure<int>())
            .Then("The result represents a failure", () => Assert.True(result?.IsFailure))
            .And("The original error is retained", () => Assert.Equal(error, result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_Message_ReturnsFailure()
    {
        Result? result = null;

        return Scenario.Define("A failure message can be converted to an error result", _resultConversions)
            .When("The message is converted to a failure result", () => result = "bad".ToFailure())
            .Then("The result contains an error with the supplied message", () => Assert.Equal(new Error("bad"), result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_NullMessage_ThrowsWithMessageParameter()
    {
        string message = null!;

        return Scenario.Define("A failure cannot be created without its message", _resultConversions)
            .WhenThrows<ArgumentNullException>("The null message is converted to a failure", () => _ = message.ToFailure())
            .Then("The refusal identifies the message parameter", (ArgumentNullException refusal) => Assert.Equal("message", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_ResultOfTFromMessage_ReturnsFailure()
    {
        Result<int>? result = null;

        return Scenario.Define("A message can fail a value-bearing result", _resultConversions)
            .When("The message is converted to a value-bearing failure result", () => result = "bad".ToFailure<int>())
            .Then("The result represents a failure", () => Assert.True(result?.IsFailure))
            .And("The failure contains the supplied message", () => Assert.Equal(new Error("bad"), result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_ResultOfTFromNullMessage_ThrowsWithMessageParameter()
    {
        string message = null!;

        return Scenario.Define("A value-bearing failure also requires its message", _resultConversions)
            .WhenThrows<ArgumentNullException>("The null message is converted to a value-bearing failure", () => _ = message.ToFailure<int>())
            .Then("The refusal identifies the message parameter", (ArgumentNullException refusal) => Assert.Equal("message", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_NullException_ThrowsWithExceptionParameter()
    {
        Exception exception = null!;

        return Scenario.Define("A missing exception cannot be converted to a failure", _resultConversions)
            .WhenThrows<ArgumentNullException>("The null exception is converted to a failure", () => _ = exception.ToFailure())
            .Then("The refusal identifies the exception parameter", (ArgumentNullException refusal) => Assert.Equal("exception", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_Exception_ReturnsFailure()
    {
        InvalidOperationException source = new("boom");
        Result? result = null;

        return Scenario.Define("A failed operation retains its exception in the result", _resultConversions)
            .When("The exception is converted to a failure result", () => result = source.ToFailure())
            .Then("The result represents a failure", () => Assert.True(result?.IsFailure))
            .And("The exception message becomes the error message", () => Assert.Equal("boom", result?.Error.Message))
            .And("The original exception is preserved", () => Assert.Same(source, result?.Error.Exception))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_ExceptionAndMessage_ReturnsFailure()
    {
        InvalidOperationException source = new("inner");
        Result? result = null;

        return Scenario.Define("A failure can use a custom message and retain its exception", _resultConversions)
            .When("The exception and custom message are converted to a failure result", () => result = source.ToFailure("custom error message"))
            .Then("The result represents a failure", () => Assert.True(result?.IsFailure))
            .And("The custom message becomes the error message", () => Assert.Equal("custom error message", result?.Error.Message))
            .And("The original exception is preserved", () => Assert.Same(source, result?.Error.Exception))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_NullExceptionAndMessage_ThrowsWithExceptionParameter()
    {
        Exception exception = null!;

        return Scenario.Define("A custom-message failure still requires its exception", _resultConversions)
            .WhenThrows<ArgumentNullException>("A missing exception is converted with a custom message", () => _ = exception.ToFailure("msg"))
            .Then("The refusal identifies the exception parameter", (ArgumentNullException refusal) => Assert.Equal("exception", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_ExceptionAndNullMessage_ThrowsWithMessageParameter()
    {
        InvalidOperationException source = new("inner");

        return Scenario.Define("A custom-message failure requires a non-null message", _resultConversions)
            .WhenThrows<ArgumentNullException>("The exception is converted with a null message", () => _ = source.ToFailure(null!))
            .Then("The refusal identifies the message parameter", (ArgumentNullException refusal) => Assert.Equal("message", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_ResultOfTFromException_ReturnsFailure()
    {
        InvalidOperationException source = new("boom");
        Result<int>? result = null;

        return Scenario.Define("A value-bearing failure retains its originating exception", _resultConversions)
            .When("The exception is converted to a value-bearing failure result", () => result = source.ToFailure<int>())
            .Then("The result represents a failure", () => Assert.True(result?.IsFailure))
            .And("The exception message becomes the error message", () => Assert.Equal("boom", result?.Error.Message))
            .And("The original exception is preserved", () => Assert.Same(source, result?.Error.Exception))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_ResultOfTFromNullException_ThrowsWithExceptionParameter()
    {
        Exception exception = null!;

        return Scenario.Define("A value-bearing failure requires an exception", _resultConversions)
            .WhenThrows<ArgumentNullException>("The missing exception is converted to a value-bearing failure", () => _ = exception.ToFailure<int>())
            .Then("The refusal identifies the exception parameter", (ArgumentNullException refusal) => Assert.Equal("exception", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_ResultOfTFromExceptionAndMessage_ReturnsFailure()
    {
        InvalidOperationException source = new("inner");
        Result<int>? result = null;

        return Scenario.Define("A value-bearing failure keeps a custom message and exception", _resultConversions)
            .When("The exception and custom message are converted to a value-bearing failure", () => result = source.ToFailure<int>("custom message"))
            .Then("The result represents a failure", () => Assert.True(result?.IsFailure))
            .And("The custom message becomes the error message", () => Assert.Equal("custom message", result?.Error.Message))
            .And("The original exception is preserved", () => Assert.Same(source, result?.Error.Exception))
            .VerifyAsync();
    }

    [Fact]
    public Task ToFailure_ResultOfTFromNullExceptionAndMessage_ThrowsWithExceptionParameter()
    {
        Exception exception = null!;

        return Scenario.Define("A custom-message value-bearing failure requires an exception", _resultConversions)
            .WhenThrows<ArgumentNullException>("A missing exception is converted with a custom message", () => _ = exception.ToFailure<int>("msg"))
            .Then("The refusal identifies the exception parameter", (ArgumentNullException refusal) => Assert.Equal("exception", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task ToOption_NonNullValue_ReturnsSome()
    {
        var value = "hello";
        Option<string>? option = null;

        return Scenario.Define("A present value becomes an available option", _optionalValues)
            .When("The value is converted to an option", () => option = value.ToOption())
            .Then("The option is present", () => Assert.True(option?.IsSome))
            .And("The original value is available", () => Assert.Equal("hello", option?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task ToOption_NullValue_ReturnsNone()
    {
        string? value = null;
        Option<string>? option = null;

        return Scenario.Define("A missing value becomes an empty option", _optionalValues)
            .When("The null value is converted to an option", () => option = value.ToOption())
            .Then("The option is empty", () => Assert.True(option?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task FirstOrNone_EmptySequence_ReturnsNone()
    {
        Option<int>? option = null;

        return Scenario.Define("An empty sequence has no first optional value", _optionalValues)
            .When("The first value is selected from the empty sequence", () => option = Array.Empty<int>().FirstOrNone())
            .Then("The selection is empty", () => Assert.True(option?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task FirstOrNone_NonEmptySequence_ReturnsFirstElementAsSome()
    {
        Option<int>? option = null;

        return Scenario.Define("A non-empty sequence returns its first value", _optionalValues)
            .When("The first value is selected", () => option = new[] { 10, 20, 30 }.FirstOrNone())
            .Then("The selection is present", () => Assert.True(option?.IsSome))
            .And("The first item is returned", () => Assert.Equal(10, option?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task FirstOrNone_NullSequence_ThrowsWithSourceParameter()
    {
        IEnumerable<int> source = null!;

        return Scenario.Define("Selecting a first value requires a sequence", _optionalValues)
            .WhenThrows<ArgumentNullException>("The first value is selected from a missing sequence", () => _ = source.FirstOrNone())
            .Then("The refusal identifies the source parameter", (ArgumentNullException refusal) => Assert.Equal("source", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task FirstOrNone_WithPredicate_MatchingElement_ReturnsSome()
    {
        Option<int>? option = null;

        return Scenario.Define("First-value selection returns the earliest item that matches", _optionalValues)
            .When("The first item matching the selection rule is chosen", () => option = new[] { 1, 2, 3, 4, 5 }.FirstOrNone(x => x > 3))
            .Then("The selection is present", () => Assert.True(option?.IsSome))
            .And("The earliest matching item is returned", () => Assert.Equal(4, option?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task FirstOrNone_WithPredicate_NoMatch_ReturnsNone()
    {
        Option<int>? option = null;

        return Scenario.Define("First-value selection is empty when nothing matches", _optionalValues)
            .When("No item matches the selection rule", () => option = new[] { 1, 2, 3 }.FirstOrNone(x => x > 10))
            .Then("The selection is empty", () => Assert.True(option?.IsNone))
            .VerifyAsync();
    }

    [Fact]
    public Task FirstOrNone_WithPredicate_NullSequence_ThrowsWithSourceParameter()
    {
        IEnumerable<int> source = null!;

        return Scenario.Define("Predicate selection requires a sequence", _optionalValues)
            .WhenThrows<ArgumentNullException>("A matching item is selected from a missing sequence", () => _ = source.FirstOrNone(x => x > 0))
            .Then("The refusal identifies the source parameter", (ArgumentNullException refusal) => Assert.Equal("source", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task FirstOrNone_WithPredicate_NullPredicate_ThrowsWithPredicateParameter()
    {
        int[] values = [1, 2, 3];

        return Scenario.Define("Predicate selection requires a selection rule", _optionalValues)
            .WhenThrows<ArgumentNullException>("The sequence is searched with no predicate", () => _ = values.FirstOrNone(null!))
            .Then("The refusal identifies the predicate parameter", (ArgumentNullException refusal) => Assert.Equal("predicate", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task ToSuccess_ComplexPayload_PreservesExactInstance()
    {
        Document document = CreateDocument();
        Result<Document>? result = null;

        return Scenario.Define("A successful document conversion retains the complete payload", _documentPayloads)
            .When("The document is converted to a success result", () => result = document.ToSuccess())
            .Then("The exact document instance is preserved", () => Assert.Same(document, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task ToOption_ComplexPayload_PreservesExactInstance()
    {
        Document document = CreateDocument();
        Option<Document>? option = null;

        return Scenario.Define("An available document option retains the complete payload", _documentPayloads)
            .When("The document is converted to an option", () => option = document.ToOption())
            .Then("The exact document instance is preserved", () => Assert.Same(document, option?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task FirstOrNone_ComplexPayload_PreservesSequenceInstance()
    {
        Document first = CreateDocument();
        Document second = first with
        {
            Id = "doc-2"
        };
        Option<Document>? option = null;

        return Scenario.Define("First-match selection retains the selected document instance", _documentPayloads)
            .When("The first document with multiple sections is selected", () =>
                option = new[] { first, second }.FirstOrNone(document => document.Sections.Count > 1))
            .Then("The exact first matching document is preserved", () => Assert.Same(first, option?.Value))
            .VerifyAsync();
    }

    private static Document CreateDocument() =>
        new(
            "doc-1",
            new DocumentMetadata("Ada", new Dictionary<string, string> { ["format"] = "markdown" }),
            ["Introduction", "Details"]);
}
