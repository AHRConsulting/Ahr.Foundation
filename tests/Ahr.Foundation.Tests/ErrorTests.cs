namespace Ahr.Foundation.Tests;

public sealed class ErrorTests
{
    private static readonly BusinessContext _errorValues = BusinessContext
        .Define("error-values", "Error values", "Preserve useful failure details at the point an error is created.")
        .Capability("create-errors", "Create errors from messages and exceptions")
        .Outcome("failure-details-preserved", "Error messages and associated exceptions remain available")
        .Rule("error-message-required", "An error always has a non-null message")
        .Build();

    [Fact]
    public Task Constructor_MessageAndException_PreservesBoth()
    {
        const string message = "outer";
        InvalidOperationException innerException = new("inner");
        Error? error = null;

        return Scenario.Define("An error keeps its message and inner exception", _errorValues)
            .When("The error is created from both details", () =>
            {
                error = new Error(message, innerException);
            })
            .Then("The message remains available", () => Assert.Equal(message, error?.Message))
            .And("The originating exception is preserved", () => Assert.Same(innerException, error?.Exception))
            .VerifyAsync();
    }

    [Fact]
    public Task Constructor_NullMessage_ThrowsWithMessageParameter() =>
        Scenario.Define("Creating an error without a message is rejected", _errorValues)
            .WhenThrows<ArgumentNullException>("An error is created without a message", () => _ = new Error(null!))
            .Then("The refusal identifies the message parameter", (ArgumentNullException refusal) => Assert.Equal("message", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task FromException_NullException_ThrowsWithExceptionParameter() =>
        Scenario.Define("Creating an error from a missing exception is rejected", _errorValues)
            .WhenThrows<ArgumentNullException>("An error is created from no exception", () => _ = Error.FromException(null!))
            .Then("The refusal identifies the exception parameter", (ArgumentNullException refusal) => Assert.Equal("exception", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task FromException_ValidException_PreservesIdentityAndMessage()
    {
        InvalidOperationException source = new("broken");
        Error? error = null;

        return Scenario.Define("An error created from an exception keeps its details", _errorValues)
            .When("The exception is converted to an error", () => { error = Error.FromException(source); })
            .Then("The exception message becomes the error message", () => Assert.Equal("broken", error?.Message))
            .And("The original exception remains attached", () => Assert.Same(source, error?.Exception))
            .VerifyAsync();
    }

    [Fact]
    public Task Equality_EquivalentErrors_AreEqualAndHashCodesMatch()
    {
        Error left = new("same");
        Error right = new("same");
        bool? areEqual = null;
        bool? hashesMatch = null;

        return Scenario.Define("Errors with the same message compare as equal", _errorValues)
            .When("The errors are compared", () =>
            {
                areEqual = left.Equals(right);
                hashesMatch = left.GetHashCode() == right.GetHashCode();
            })
            .Then("They have equal values", () => Assert.True(areEqual))
            .And("They have matching hash codes", () => Assert.True(hashesMatch))
            .VerifyAsync();
    }

    [Fact]
    public Task WithExpression_NullMessage_ThrowsWithValueParameter()
    {
        Error error = new("valid");

        return Scenario.Define("An error cannot be updated with a null message", _errorValues)
            .WhenThrows<ArgumentNullException>("A valid error's message is replaced with null", () => _ = error with { Message = null! })
            .Then("The refusal identifies the replacement value", (ArgumentNullException refusal) => Assert.Equal("value", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task FromMessage_ReturnsErrorWithMessage()
    {
        const string message = "custom error";

        return Scenario.Define("An error created from a message has no exception", _errorValues)
            .When("The message is converted to an error", () => Error.FromMessage(message))
            .Then("The error keeps the supplied message", (Error error) => Assert.Equal(message, error.Message))
            .And("No exception is attached", (Error error) => Assert.Null(error.Exception))
            .VerifyAsync();
    }

    [Fact]
    public Task FromMessage_NullMessage_ThrowsWithMessageParameter() =>
        Scenario.Define("Creating an error from a missing message is rejected", _errorValues)
            .WhenThrows<ArgumentNullException>("The absent message is converted to an error", () => _ = Error.FromMessage(null!))
            .Then("The refusal identifies the message parameter", (ArgumentNullException refusal) => Assert.Equal("message", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task ToString_ReturnsMessage()
    {
        const string message = "failed to connect";
        Error error = new(message);
        string? description = null;

        return Scenario.Define("An error description is its human-readable message", _errorValues)
            .When("The error is converted to text", () => { description = error.ToString(); })
            .Then("The description contains the error message", () => Assert.Equal(message, description))
            .VerifyAsync();
    }
}
