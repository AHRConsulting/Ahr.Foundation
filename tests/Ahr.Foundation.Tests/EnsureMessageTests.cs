namespace Ahr.Foundation.Tests;

public sealed class EnsureMessageTests
{
    private const string Message = "The value must be positive.";

    private static readonly BusinessContext _valueValidation = BusinessContext
        .Define("value-validation", "Value validation", "Keep values that satisfy a rule and explain rejected values.")
        .Capability("validate-values", "Validate values in synchronous and asynchronous workflows")
        .Outcome("invalid-values-explained", "A rejected value carries the validation message")
        .Rule("failed-results-preserved", "A prior failure is not replaced by a later validation rule")
        .Build();

    [Fact]
    public Task Ensure_PredicatePasses_KeepsValue()
    {
        Result<int>? result = null;

        return Scenario.Define("A positive value passes the validation rule", _valueValidation)
            .When("The value is checked against the positive rule", () =>
            {
                result = Result<int>.Success(1).Ensure(value => value > 0, Message);
            })
            .Then("The validated value remains available", () =>
                Assert.Equal(1, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task Ensure_PredicateFails_UsesMessage()
    {
        Result<int>? result = null;

        return Scenario.Define("A non-positive value is rejected with the validation message", _valueValidation)
            .When("The value is checked against the positive rule", () =>
            {
                result = Result<int>.Success(-1).Ensure(value => value > 0, Message);
            })
            .Then("The failure explains the violated rule", () =>
                Assert.Equal(Message, result?.Error.Message))
            .VerifyAsync();
    }

    [Fact]
    public Task Ensure_AlreadyFailed_KeepsOriginalError()
    {
        var original = Error.FromMessage("original");
        Result<int>? result = null;

        return Scenario.Define("An existing failure survives later validation", _valueValidation)
            .When("The validation rule is applied to the failed result", () =>
            {
                result = Result<int>.Failure(original).Ensure(_ => false, Message);
            })
            .Then("The original error is preserved", () =>
                Assert.Equal(original, result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task Ensure_NullMessage_Throws() =>
        Scenario.Define("A validation rule requires an error message", _valueValidation)
            .WhenThrows<ArgumentNullException>("A valid value is checked with a null rule message", () =>
            _ = Result<int>.Success(1).Ensure(_ => true, null!))
            .Then("The invalid message is rejected", (ArgumentNullException refusal) =>
                Assert.Equal("message", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task EnsureAsync_PredicateFails_UsesMessage()
    {
        Result<int>? result = null;

        return Scenario.Define("An asynchronous validation failure explains the rejected value", _valueValidation)
            .When("The value is checked by an asynchronous rule", async () =>
            {
                result = await Result<int>.Success(-1).EnsureAsync(value => Task.FromResult(value > 0), Message);
            })
            .Then("The failure carries the rule message", () =>
                Assert.Equal(Message, result?.Error.Message))
            .VerifyAsync();
    }

    [Fact]
    public Task EnsureAsync_PredicatePasses_KeepsValue()
    {
        Result<int>? result = null;

        return Scenario.Define("An asynchronously validated value remains available", _valueValidation)
            .When("A positive value passes the asynchronous rule", async () =>
            {
                result = await Result<int>.Success(1).EnsureAsync(value => Task.FromResult(value > 0), Message);
            })
            .Then("The successful value is preserved", () =>
                Assert.Equal(1, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskEnsureAsync_SyncPredicateFails_UsesMessage()
    {
        Result<int>? result = null;

        return Scenario.Define("A task result supports synchronous validation", _valueValidation)
            .When("A task result is checked against a synchronous rule", async () =>
            {
                result = await Task.FromResult(Result<int>.Success(-1)).EnsureAsync(value => value > 0, Message);
            })
            .Then("The failed task result carries the rule message", () =>
                Assert.Equal(Message, result?.Error.Message))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskEnsureAsync_AsyncPredicateFails_UsesMessage()
    {
        Result<int>? result = null;

        return Scenario.Define("A task result supports asynchronous validation", _valueValidation)
            .When("A task result is checked against an asynchronous rule", async () =>
            {
                result = await Task.FromResult(Result<int>.Success(-1))
                    .EnsureAsync(value => Task.FromResult(value > 0), Message);
            })
            .Then("The failed task result carries the rule message", () =>
                Assert.Equal(Message, result?.Error.Message))
            .VerifyAsync();
    }

    [Fact]
    public Task TaskEnsureAsync_NullTask_ThrowsWithResultTaskParameter()
    {
        Task<Result<int>> resultTask = null!;

        return Scenario.Define("Asynchronous validation requires a result task", _valueValidation)
            .WhenThrows<ArgumentNullException>("The validation rule receives no result task", async () =>
                await resultTask.EnsureAsync(_ => true, Message))
            .Then("The refusal identifies the missing task", (ArgumentNullException refusal) =>
                Assert.Equal("resultTask", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task ToResultAsync_Some_ReturnsSuccess()
    {
        Result<int>? result = null;

        return Scenario.Define("A present option continues as a successful result", _valueValidation)
            .When("The present option is converted with a validation error", async () =>
            {
                result = await Task.FromResult(Option<int>.Some(1)).ToResultAsync(Error.FromMessage(Message));
            })
            .Then("The contained value continues in the workflow", () =>
                Assert.Equal(1, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task ToResultAsync_None_ReturnsFailure()
    {
        var error = Error.FromMessage(Message);
        Result<int>? result = null;

        return Scenario.Define("An empty option becomes the provided failure", _valueValidation)
            .When("The empty option is converted to a result", async () =>
            {
                result = await Task.FromResult(Option<int>.None).ToResultAsync(error);
            })
            .Then("The result contains the supplied error", () =>
                Assert.Equal(error, result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task ToResultAsync_CustomError_ReturnsCustomFailure()
    {
        Result<int, int>? result = null;

        return Scenario.Define("An empty option can become a custom-error failure", _valueValidation)
            .When("The empty option is converted with a custom error", async () =>
            {
                result = await Task.FromResult(Option<int>.None).ToResultAsync(404);
            })
            .Then("The result contains the custom error", () =>
                Assert.Equal(404, result?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task ToResultAsync_ContinuesPipeline()
    {
        Result<int>? result = null;

        return Scenario.Define("A present option continues through result composition", _valueValidation)
            .When("The option is converted and its value is incremented", async () =>
            {
                result = await Task.FromResult(Option<int>.Some(1))
                    .ToResultAsync(Error.FromMessage(Message))
                    .BindAsync(value => Task.FromResult(Result<int>.Success(value + 1)));
            })
            .Then("The composed result contains the incremented value", () =>
                Assert.Equal(2, result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task ToResultAsync_NullTask_ThrowsWithOptionTaskParameter()
    {
        Task<Option<int>> optionTask = null!;

        return Scenario.Define("Option conversion requires an option task", _valueValidation)
            .WhenThrows<ArgumentNullException>("The conversion receives no option task", async () =>
                await optionTask.ToResultAsync(Error.FromMessage(Message)))
            .Then("The refusal identifies the missing option task", (ArgumentNullException refusal) =>
                Assert.Equal("optionTask", refusal.ParamName))
            .VerifyAsync();
    }
}
