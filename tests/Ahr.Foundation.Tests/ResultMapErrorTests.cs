namespace Ahr.Foundation.Tests;

public sealed class ResultMapErrorTests
{
    private static readonly BusinessContext _failureHandling = BusinessContext
        .Define("result-failure-handling", "Result failure handling", "Preserve or translate failures as operations move through a workflow.")
        .Capability("translate-failures", "Translate errors while preserving successful results")
        .Outcome("failure-details-preserved", "Mapped failures carry the intended error")
        .Rule("success-is-unchanged", "Error mapping never changes or invokes the mapper for success")
        .Build();

    private static Error Wrap(Error error) => Error.FromMessage($"save failed: {error.Message}");

    [Fact]
    public Task MapError_Failure_ReplacesError()
    {
        Result? result = null;

        return Scenario.Define("A failed result carries the translated error", _failureHandling)
            .When("A storage failure is translated for the caller", () =>
            {
                result = Result.Failure(Error.FromMessage("disk")).MapError(Wrap);
            })
            .Then("The caller receives the translated message", () =>
                Assert.Equal("save failed: disk", result?.Error.Message))
            .VerifyAsync();
    }

    [Fact]
    public Task MapError_Success_ReturnsSuccessWithoutCallingMap()
    {
        var mapperCalled = false;
        Result? result = null;

        return Scenario.Define("A successful result bypasses error translation", _failureHandling)
            .When("A successful result passes through the error mapper", () =>
            {
                result = Result.Success().MapError(error =>
                {
                    mapperCalled = true;
                    return error;
                });
            })
            .Then("The result remains successful", () => Assert.True(result?.IsSuccess))
            .And("The error mapper is not called", () => Assert.False(mapperCalled))
            .VerifyAsync();
    }

    [Fact]
    public Task MapError_DefaultErrorFromMap_Throws() =>
        Scenario.Define("An error mapper must return a valid error", _failureHandling)
            .WhenThrows<ArgumentNullException>("A failed result is mapped to a default error", () =>
            _ = Result.Failure(Error.FromMessage("disk")).MapError(_ => default))
            .Then("The invalid mapped error is rejected", (ArgumentNullException refusal) =>
                Assert.Equal("error", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task MapErrorAsync_AllForms_ReplaceError()
    {
        Result? direct = null;
        Result? fromTaskAsync = null;
        Result? fromTaskSync = null;
        Result? successful = null;

        return Scenario.Define("Asynchronous error mapping preserves the workflow outcome", _failureHandling)
            .When("The failure and success paths use each asynchronous overload", async () =>
            {
                var failed = Result.Failure(Error.FromMessage("disk"));
                direct = await failed.MapErrorAsync(error => Task.FromResult(Wrap(error)));
                fromTaskAsync = await Task.FromResult(failed).MapErrorAsync(error => Task.FromResult(Wrap(error)));
                fromTaskSync = await Task.FromResult(failed).MapErrorAsync(Wrap);
                successful = await Task.FromResult(Result.Success()).MapErrorAsync(Wrap);
            })
            .Then("Each failed path carries the translated message", () =>
            {
                Assert.Equal("save failed: disk", direct?.Error.Message);
                Assert.Equal("save failed: disk", fromTaskAsync?.Error.Message);
                Assert.Equal("save failed: disk", fromTaskSync?.Error.Message);
            })
            .And("A successful path remains successful", () => Assert.True(successful?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task MapError_NullMap_ThrowsWithMapParameter()
    {
        var failed = Result.Failure(Error.FromMessage("disk"));

        return Scenario.Define("Error mapping requires a mapper", _failureHandling)
            .WhenThrows<ArgumentNullException>("A failed result is mapped with no mapper", () =>
            _ = failed.MapError(null!))
            .Then("The refusal identifies the mapper parameter", (ArgumentNullException refusal) =>
                Assert.Equal("map", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task MapErrorAsync_NullMap_ThrowsWithMapParameter()
    {
        var failed = Result.Failure(Error.FromMessage("disk"));

        return Scenario.Define("Asynchronous error mapping requires a mapper", _failureHandling)
            .WhenThrows<ArgumentNullException>("A failed result is mapped asynchronously with no mapper", async () =>
                await failed.MapErrorAsync(null!))
            .Then("The refusal identifies the mapper parameter", (ArgumentNullException refusal) =>
                Assert.Equal("map", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task MapErrorAsync_MapReturnsNullTask_ThrowsWithMapParameter()
    {
        var failed = Result.Failure(Error.FromMessage("disk"));

        return Scenario.Define("Asynchronous error mapping requires a mapper task", _failureHandling)
            .WhenThrows<ArgumentNullException>("The error mapper returns no task", async () =>
                await failed.MapErrorAsync(_ => null!))
            .Then("The refusal identifies the mapper parameter", (ArgumentNullException refusal) =>
                Assert.Equal("map", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task MapErrorAsync_NullResultTask_ThrowsWithResultTaskParameter()
    {
        Task<Result> resultTask = null!;

        return Scenario.Define("Asynchronous error mapping requires a result task", _failureHandling)
            .WhenThrows<ArgumentNullException>("Error mapping is applied to no result task", async () =>
                await resultTask.MapErrorAsync(Wrap))
            .Then("The refusal identifies the result task parameter", (ArgumentNullException refusal) =>
                Assert.Equal("resultTask", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task MapError_Uninitialized_Throws() =>
        Scenario.Define("An uninitialized result cannot enter an error mapping workflow", _failureHandling)
            .WhenThrows<InvalidOperationException>("An uninitialized result is passed to the mapper", () =>
            _ = default(Result).MapError(Wrap))
            .Then("The result explains how it must be initialized", (InvalidOperationException exception) =>
                Assert.Equal(ResultTestMessages.Uninitialized, exception.Message))
            .VerifyAsync();
}
