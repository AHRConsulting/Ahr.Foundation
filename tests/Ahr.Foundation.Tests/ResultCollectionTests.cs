namespace Ahr.Foundation.Tests;

public sealed class ResultCollectionTests
{
    private static readonly int[] _items = [1, 2, 3, 4];

    private static readonly BusinessContext _resultCollections = BusinessContext
        .Define("result-collection-processing", "Result collection processing", "Transform result sequences without losing order or error information.")
        .Capability("traverse-sequences", "Transform every item or stop at the first failure")
        .Capability("partition-sequences", "Keep every successful value and every error")
        .Outcome("ordered-success-values", "Successful values remain in their original order")
        .Outcome("first-failure-stops-traversal", "Traversal stops at its first failure")
        .Outcome("all-partition-errors-retained", "Partitioning retains every failure")
        .Rule("sequential-async-processing", "Asynchronous collection operations process one item at a time")
        .Build();

    private static Result<int> Parse(int item) =>
        item % 2 == 0 ? Result<int>.Failure(Error.FromMessage($"bad {item}")) : Result<int>.Success(item * 10);

    private static Result Check(int item) =>
        item % 2 == 0 ? Result.Failure(Error.FromMessage($"bad {item}")) : Result.Success();

    private static Result<int, string> ParseCustom(int item) =>
        item % 2 == 0 ? Result<int, string>.Failure($"bad {item}") : Result<int, string>.Success(item * 10);

    private static Result<int> Ok(int item) => Result<int>.Success(item * 10);

    [Fact]
    public Task Traverse_AllSucceed_ReturnsValuesInOrder()
    {
        Result<IReadOnlyList<int>>? result = null;

        return Scenario.Define("Traversing successful items preserves their order", _resultCollections)
            .When("Each item is transformed into a successful result", () =>
            {
                result = _items.Traverse(Ok);
            })
            .Then("The transformed values are returned in source order", () =>
                Assert.Equal([10, 20, 30, 40], result?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task Traverse_Empty_ReturnsEmptySuccess()
    {
        Result<IReadOnlyList<int>>? values = null;
        Result? payloadFree = null;

        return Scenario.Define("Traversing an empty sequence returns success", _resultCollections)
            .When("The empty sequence is traversed with and without a payload", () =>
            {
                values = Array.Empty<int>().Traverse(Ok);
                payloadFree = Array.Empty<int>().Traverse(Check);
            })
            .Then("The value-producing traversal returns an empty collection", () =>
                Assert.Equal(0, values?.Value.Count))
            .And("The payload-free traversal succeeds", () =>
                Assert.True(payloadFree?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task Traverse_StopsAtFirstFailure()
    {
        List<int> seen = [];
        Result<IReadOnlyList<int>>? result = null;

        return Scenario.Define("Traversal stops at the first failed item", _resultCollections)
            .When("Items are parsed until one fails", () =>
            {
                result = _items.Traverse(item =>
                {
                    seen.Add(item);
                    return Parse(item);
                });
            })
            .Then("The first failure is returned", () =>
                Assert.Equal("bad 2", result?.Error.Message))
            .And("Items after the failure are not visited", () =>
                Assert.Equal([1, 2], seen))
            .VerifyAsync();
    }

    [Fact]
    public Task Traverse_PayloadFree_ReturnsFirstFailure()
    {
        Result? failed = null;
        Result? passed = null;

        return Scenario.Define("Payload-free traversal preserves the first failure", _resultCollections)
            .When("A failing and a successful sequence are traversed", () =>
            {
                failed = _items.Traverse(Check);
                passed = new[] { 1, 3 }.Traverse(Check);
            })
            .Then("The failing sequence returns its first error", () =>
                Assert.Equal("bad 2", failed?.Error.Message))
            .And("The successful sequence returns success", () =>
                Assert.True(passed?.IsSuccess))
            .VerifyAsync();
    }

    [Fact]
    public Task Traverse_CustomError_ReturnsValuesOrFirstFailure()
    {
        Result<IReadOnlyList<int>, string>? failed = null;
        Result<IReadOnlyList<int>, string>? passed = null;

        return Scenario.Define("Custom-error traversal returns values or the first error", _resultCollections)
            .When("A failing and a successful sequence are traversed", () =>
            {
                failed = _items.Traverse(ParseCustom);
                passed = new[] { 1, 3 }.Traverse(ParseCustom);
            })
            .Then("The first custom error is returned", () =>
                Assert.Equal("bad 2", failed?.Error))
            .And("Successful custom-error results keep their values", () =>
                Assert.Equal([10, 30], passed?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task TraverseAsync_RunsOneAtATimeAndStopsAtFirstFailure()
    {
        List<int> seen = [];
        var running = 0;
        var maxRunning = 0;
        Result<IReadOnlyList<int>>? result = null;

        return Scenario.Define("Asynchronous traversal is sequential and stops at the first failure", _resultCollections)
            .When("Items are parsed asynchronously", async () =>
            {
                result = await _items.TraverseAsync(async item =>
                {
                    maxRunning = Math.Max(maxRunning, ++running);
                    seen.Add(item);
                    await Task.Yield();
                    running--;
                    return Parse(item);
                });
            })
            .Then("The first failure is returned", () =>
                Assert.Equal("bad 2", result?.Error.Message))
            .And("Items after the failure are not visited", () =>
                Assert.Equal([1, 2], seen))
            .And("Only one item is processed at a time", () =>
                Assert.Equal(1, maxRunning))
            .VerifyAsync();
    }

    [Fact]
    public Task TraverseAsync_AllForms_ReturnValuesOrFirstFailure()
    {
        Result<IReadOnlyList<int>>? values = null;
        Result? payloadFree = null;
        Result<IReadOnlyList<int>, string>? custom = null;

        return Scenario.Define("Asynchronous traversal preserves each result shape", _resultCollections)
            .When("Value, payload-free, and custom-error sequences are traversed", async () =>
            {
                values = await new[] { 1, 3 }.TraverseAsync(item => Task.FromResult(Parse(item)));
                payloadFree = await _items.TraverseAsync(item => Task.FromResult(Check(item)));
                custom = await _items.TraverseAsync(item => Task.FromResult(ParseCustom(item)));
            })
            .Then("Successful values are returned in order", () =>
                Assert.Equal([10, 30], values?.Value))
            .And("Payload-free traversal returns its first failure", () =>
                Assert.Equal("bad 2", payloadFree?.Error.Message))
            .And("Custom-error traversal returns its first failure", () =>
                Assert.Equal("bad 2", custom?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task Sequence_AllSucceed_ReturnsValues()
    {
        Result<IReadOnlyList<int>>? values = null;
        Result? payloadFree = null;
        Result<IReadOnlyList<int>, string>? custom = null;

        return Scenario.Define("Sequencing successful results preserves their values", _resultCollections)
            .When("Value, payload-free, and custom-error results are sequenced", () =>
            {
                values = new[] { 1, 3 }.Select(Parse).Sequence();
                payloadFree = new[] { 1, 3 }.Select(Check).Sequence();
                custom = new[] { 1, 3 }.Select(ParseCustom).Sequence();
            })
            .Then("Value results retain their values", () =>
                Assert.Equal([10, 30], values?.Value))
            .And("Payload-free results remain successful", () =>
                Assert.True(payloadFree?.IsSuccess))
            .And("Custom-error results retain their values", () =>
                Assert.Equal([10, 30], custom?.Value))
            .VerifyAsync();
    }

    [Fact]
    public Task Sequence_StopsReadingAtFirstFailure()
    {
        var read = 0;
        Result<IReadOnlyList<int>>? values = null;
        Result? payloadFree = null;
        Result<IReadOnlyList<int>, string>? custom = null;

        return Scenario.Define("Sequencing stops reading after its first failure", _resultCollections)
            .When("Each result shape is sequenced", () =>
            {
                IEnumerable<Result<int>> results = _items.Select(item =>
                {
                    read++;
                    return Parse(item);
                });

                values = results.Sequence();
                payloadFree = _items.Select(Check).Sequence();
                custom = _items.Select(ParseCustom).Sequence();
            })
            .Then("The first value-result failure is returned", () =>
                Assert.Equal("bad 2", values?.Error.Message))
            .And("The sequence is read only through the first failure", () =>
                Assert.Equal(2, read))
            .And("Payload-free sequencing returns its first failure", () =>
                Assert.Equal("bad 2", payloadFree?.Error.Message))
            .And("Custom-error sequencing returns its first failure", () =>
                Assert.Equal("bad 2", custom?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task SequenceAsync_StopsAwaitingAtFirstFailure()
    {
        var started = 0;
        Result<IReadOnlyList<int>>? result = null;

        return Scenario.Define("Asynchronous sequencing stops awaiting after its first failure", _resultCollections)
            .When("Results are created and sequenced one at a time", async () =>
            {
                IEnumerable<Task<Result<int>>> tasks = _items.Select(item =>
                {
                    started++;
                    return Task.FromResult(Parse(item));
                });

                result = await tasks.SequenceAsync();
            })
            .Then("The first failure is returned", () =>
                Assert.Equal("bad 2", result?.Error.Message))
            .And("Only results through the first failure are started", () =>
                Assert.Equal(2, started))
            .VerifyAsync();
    }

    [Fact]
    public Task SequenceAsync_AllForms_ReturnValuesOrFirstFailure()
    {
        Result<IReadOnlyList<int>>? values = null;
        Result? failedPayloadFree = null;
        Result? passedPayloadFree = null;
        Result<IReadOnlyList<int>, string>? custom = null;

        return Scenario.Define("Asynchronous sequencing preserves each result shape", _resultCollections)
            .When("Value, payload-free, and custom-error tasks are sequenced", async () =>
            {
                values = await new[] { 1, 3 }.Select(item => Task.FromResult(Parse(item))).SequenceAsync();
                failedPayloadFree = await _items.Select(item => Task.FromResult(Check(item))).SequenceAsync();
                passedPayloadFree = await new[] { 1, 3 }.Select(item => Task.FromResult(Check(item))).SequenceAsync();
                custom = await _items.Select(item => Task.FromResult(ParseCustom(item))).SequenceAsync();
            })
            .Then("Successful value results retain their values", () =>
                Assert.Equal([10, 30], values?.Value))
            .And("Payload-free sequencing retains its failure", () =>
                Assert.Equal("bad 2", failedPayloadFree?.Error.Message))
            .And("Successful payload-free results remain successful", () =>
                Assert.True(passedPayloadFree?.IsSuccess))
            .And("Custom-error sequencing retains its failure", () =>
                Assert.Equal("bad 2", custom?.Error))
            .VerifyAsync();
    }

    [Fact]
    public Task Partition_KeepsEveryValueAndEveryError()
    {
        (IReadOnlyList<int> Values, IReadOnlyList<Error> Errors)? partition = null;

        return Scenario.Define("Partitioning results keeps every value and every error", _resultCollections)
            .When("The parsed results are partitioned", () =>
            {
                partition = _items.Select(Parse).Partition();
            })
            .Then("Successful values are retained in order", () =>
                Assert.Equal([10, 30], partition?.Values))
            .And("Every failure is retained in order", () =>
                Assert.Equal(["bad 2", "bad 4"], partition?.Errors.Select(error => error.Message)))
            .VerifyAsync();
    }

    [Fact]
    public Task Partition_WithStep_RunsEveryItem()
    {
        (IReadOnlyList<int> Values, IReadOnlyList<Error> Errors)? values = null;
        IReadOnlyList<Error>? payloadFree = null;
        (IReadOnlyList<int> Values, IReadOnlyList<string> Errors)? custom = null;

        return Scenario.Define("Partitioning with a step retains every result", _resultCollections)
            .When("Each item is parsed with built-in and custom errors", () =>
            {
                values = _items.Partition(Parse);
                payloadFree = _items.Partition(Check);
                custom = _items.Partition(ParseCustom);
            })
            .Then("Built-in results keep all values and errors", () =>
            {
                Assert.Equal([10, 30], values?.Values);
                Assert.Equal(2, values?.Errors.Count);
            })
            .And("Payload-free results keep every error", () =>
                Assert.Equal(["bad 2", "bad 4"], payloadFree?.Select(error => error.Message)))
            .And("Custom-error results keep all values and errors", () =>
            {
                Assert.Equal([10, 30], custom?.Values);
                Assert.Equal(["bad 2", "bad 4"], custom?.Errors);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Partition_ResultLists_KeepEveryError()
    {
        IReadOnlyList<Error>? errors = null;
        IReadOnlyList<Error>? noErrors = null;
        IReadOnlyList<string>? customErrors = null;

        return Scenario.Define("Partitioning result lists keeps every error", _resultCollections)
            .When("Failing and successful result lists are partitioned", () =>
            {
                errors = _items.Select(Check).Partition();
                noErrors = new[] { 1, 3 }.Select(Check).Partition();
                customErrors = _items.Select(ParseCustom).Partition().Errors;
            })
            .Then("Every payload-free error is retained", () =>
                Assert.Equal(2, errors?.Count))
            .And("Successful results produce no errors", () =>
                Assert.Equal(0, noErrors?.Count))
            .And("Custom errors are retained in order", () =>
                Assert.Equal(["bad 2", "bad 4"], customErrors))
            .VerifyAsync();
    }

    [Fact]
    public Task PartitionAsync_WithStep_RunsEveryItemOneAtATime()
    {
        var running = 0;
        var maxRunning = 0;
        (IReadOnlyList<int> Values, IReadOnlyList<Error> Errors)? partition = null;

        return Scenario.Define("Asynchronous partitioning retains all results sequentially", _resultCollections)
            .When("Each item is parsed asynchronously", async () =>
            {
                partition = await _items.PartitionAsync(async item =>
                {
                    maxRunning = Math.Max(maxRunning, ++running);
                    await Task.Yield();
                    running--;
                    return Parse(item);
                });
            })
            .Then("Every successful value is retained", () =>
                Assert.Equal([10, 30], partition?.Values))
            .And("Every failure is retained", () =>
                Assert.Equal(2, partition?.Errors.Count))
            .And("Only one item is processed at a time", () =>
                Assert.Equal(1, maxRunning))
            .VerifyAsync();
    }

    [Fact]
    public Task PartitionAsync_AllForms_KeepEveryValueAndError()
    {
        IReadOnlyList<Error>? payloadFree = null;
        (IReadOnlyList<int> Values, IReadOnlyList<string> Errors)? custom = null;
        (IReadOnlyList<int> Values, IReadOnlyList<Error> Errors)? taskValues = null;
        IReadOnlyList<Error>? taskPayloadFree = null;
        (IReadOnlyList<int> Values, IReadOnlyList<string> Errors)? taskCustom = null;

        return Scenario.Define("Asynchronous partitioning retains every result shape", _resultCollections)
            .When("Delegate and task sequences are partitioned", async () =>
            {
                payloadFree = await _items.PartitionAsync(item => Task.FromResult(Check(item)));
                custom = await _items.PartitionAsync(item => Task.FromResult(ParseCustom(item)));
                taskValues = await _items.Select(item => Task.FromResult(Parse(item))).PartitionAsync();
                taskPayloadFree = await _items.Select(item => Task.FromResult(Check(item))).PartitionAsync();
                taskCustom = await _items.Select(item => Task.FromResult(ParseCustom(item))).PartitionAsync();
            })
            .Then("Payload-free delegate results retain every error", () =>
                Assert.Equal(2, payloadFree?.Count))
            .And("Custom-error delegate results retain values and errors", () =>
            {
                Assert.Equal([10, 30], custom?.Values);
                Assert.Equal(["bad 2", "bad 4"], custom?.Errors);
            })
            .And("Value task results retain values and errors", () =>
            {
                Assert.Equal([10, 30], taskValues?.Values);
                Assert.Equal(2, taskValues?.Errors.Count);
            })
            .And("Payload-free task results retain every error", () =>
                Assert.Equal(2, taskPayloadFree?.Count))
            .And("Custom-error task results retain values and errors", () =>
            {
                Assert.Equal([10, 30], taskCustom?.Values);
                Assert.Equal(["bad 2", "bad 4"], taskCustom?.Errors);
            })
            .VerifyAsync();
    }

    [Fact]
    public Task Traverse_NullSource_ThrowsWithExactParameter()
    {
        IEnumerable<int> source = null!;

        return Scenario.Define("Traverse rejects a null source", _resultCollections)
            .WhenThrows<ArgumentNullException>("A caller traverses a null source", () =>
                _ = source.Traverse(Ok))
            .Then("Traverse names its missing source", (ArgumentNullException refusal) =>
                Assert.Equal("source", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task Traverse_NullStep_ThrowsWithExactParameter() =>
        Scenario.Define("Traverse rejects a null step", _resultCollections)
            .WhenThrows<ArgumentNullException>("A caller traverses items without a step", () =>
                _ = _items.Traverse(null!))
            .Then("Traverse names its missing step", (ArgumentNullException refusal) =>
                Assert.Equal("step", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Partition_NullStep_ThrowsWithExactParameter() =>
        Scenario.Define("Partition rejects a null step", _resultCollections)
            .WhenThrows<ArgumentNullException>("A caller partitions items without a step", () =>
                _ = _items.Partition(null!))
            .Then("Partition names its missing step", (ArgumentNullException refusal) =>
                Assert.Equal("step", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task Sequence_NullSource_ThrowsWithExactParameter()
    {
        IEnumerable<Result<int>> results = null!;

        return Scenario.Define("Sequence rejects a null source", _resultCollections)
            .WhenThrows<ArgumentNullException>("A caller sequences a null result source", () =>
                _ = results.Sequence())
            .Then("Sequence names its missing results", (ArgumentNullException refusal) =>
                Assert.Equal("results", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task Partition_NullSource_ThrowsWithExactParameter()
    {
        IEnumerable<Result<int>> results = null!;

        return Scenario.Define("Partition rejects a null source", _resultCollections)
            .WhenThrows<ArgumentNullException>("A caller partitions a null result source", () =>
                _ = results.Partition())
            .Then("Partition names its missing results", (ArgumentNullException refusal) =>
                Assert.Equal("results", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task SequenceAsync_NullTasks_ThrowsWithExactParameter()
    {
        IEnumerable<Task<Result>> tasks = null!;

        return Scenario.Define("SequenceAsync rejects a null task source", _resultCollections)
            .WhenThrows<ArgumentNullException>("A caller sequences a null task source", tasks.SequenceAsync)
            .Then("SequenceAsync names its missing tasks", (ArgumentNullException refusal) =>
                Assert.Equal("tasks", refusal.ParamName))
            .VerifyAsync();
    }

    [Fact]
    public Task PartitionAsync_NullTaskEntry_ThrowsWithExactParameter() =>
        Scenario.Define("PartitionAsync rejects a null task entry", _resultCollections)
            .WhenThrows<ArgumentNullException>("A caller partitions a task list containing a null entry", async () =>
                await new Task<Result>[] { null! }.PartitionAsync())
            .Then("PartitionAsync identifies the tasks parameter", (ArgumentNullException refusal) =>
                Assert.Equal("tasks", refusal.ParamName))
            .VerifyAsync();

    [Fact]
    public Task TraverseAsync_NullStep_ThrowsWithExactParameter() =>
        Scenario.Define("TraverseAsync rejects a null step", _resultCollections)
            .WhenThrows<ArgumentNullException>("A caller traverses items asynchronously without a step", async () =>
                await _items.TraverseAsync(null!))
            .Then("TraverseAsync names its missing step", (ArgumentNullException refusal) =>
                Assert.Equal("step", refusal.ParamName))
            .VerifyAsync();
}
