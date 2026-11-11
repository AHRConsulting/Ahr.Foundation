namespace Ahr.Foundation.Tests;

public sealed class ResultCollectionTests
{
    private static readonly int[] _items = [1, 2, 3, 4];

    private static Result<int> Parse(int item) =>
        item % 2 == 0 ? Result<int>.Failure(Error.FromMessage($"bad {item}")) : Result<int>.Success(item * 10);

    private static Result Check(int item) =>
        item % 2 == 0 ? Result.Failure(Error.FromMessage($"bad {item}")) : Result.Success();

    private static Result<int, string> ParseCustom(int item) =>
        item % 2 == 0 ? Result<int, string>.Failure($"bad {item}") : Result<int, string>.Success(item * 10);

    private static Result<int> Ok(int item) => Result<int>.Success(item * 10);

    // -- Traverse --

    [Fact]
    public void Traverse_AllSucceed_ReturnsValuesInOrder()
    {
        Result<IReadOnlyList<int>> result = _items.Traverse(Ok);

        Assert.Equal([10, 20, 30, 40], result.Value);
    }

    [Fact]
    public void Traverse_Empty_ReturnsEmptySuccess()
    {
        Assert.Empty(Array.Empty<int>().Traverse(Ok).Value);
        Assert.True(Array.Empty<int>().Traverse(Check).IsSuccess);
    }

    [Fact]
    public void Traverse_StopsAtFirstFailure()
    {
        List<int> seen = [];
        Result<IReadOnlyList<int>> result = _items.Traverse(item =>
        {
            seen.Add(item);
            return Parse(item);
        });

        Assert.Equal("bad 2", result.Error.Message);
        Assert.Equal([1, 2], seen);
    }

    [Fact]
    public void Traverse_PayloadFree_ReturnsFirstFailure()
    {
        Assert.Equal("bad 2", _items.Traverse(Check).Error.Message);
        Assert.True(new[] { 1, 3 }.Traverse(Check).IsSuccess);
    }

    [Fact]
    public void Traverse_CustomError_ReturnsValuesOrFirstFailure()
    {
        Result<IReadOnlyList<int>, string> failed = _items.Traverse(ParseCustom);
        Result<IReadOnlyList<int>, string> passed = new[] { 1, 3 }.Traverse(ParseCustom);

        Assert.Equal("bad 2", failed.Error);
        Assert.Equal([10, 30], passed.Value);
    }

    [Fact]
    public async Task TraverseAsync_RunsOneAtATimeAndStopsAtFirstFailure()
    {
        List<int> seen = [];
        var running = 0;
        var maxRunning = 0;

        Result<IReadOnlyList<int>> result = await _items.TraverseAsync(async item =>
        {
            maxRunning = Math.Max(maxRunning, ++running);
            seen.Add(item);
            await Task.Yield();
            running--;
            return Parse(item);
        });

        Assert.Equal("bad 2", result.Error.Message);
        Assert.Equal([1, 2], seen);
        Assert.Equal(1, maxRunning);
    }

    [Fact]
    public async Task TraverseAsync_AllForms_ReturnValuesOrFirstFailure()
    {
        Result<IReadOnlyList<int>> values = await new[] { 1, 3 }.TraverseAsync(item => Task.FromResult(Parse(item)));
        Result payloadFree = await _items.TraverseAsync(item => Task.FromResult(Check(item)));
        Result<IReadOnlyList<int>, string> custom = await _items.TraverseAsync(item => Task.FromResult(ParseCustom(item)));

        Assert.Equal([10, 30], values.Value);
        Assert.Equal("bad 2", payloadFree.Error.Message);
        Assert.Equal("bad 2", custom.Error);
    }

    // -- Sequence --

    [Fact]
    public void Sequence_AllSucceed_ReturnsValues()
    {
        Assert.Equal([10, 30], new[] { 1, 3 }.Select(Parse).Sequence().Value);
        Assert.True(new[] { 1, 3 }.Select(Check).Sequence().IsSuccess);
        Assert.Equal([10, 30], new[] { 1, 3 }.Select(ParseCustom).Sequence().Value);
    }

    [Fact]
    public void Sequence_StopsReadingAtFirstFailure()
    {
        var read = 0;
        IEnumerable<Result<int>> results = _items.Select(item =>
        {
            read++;
            return Parse(item);
        });

        Assert.Equal("bad 2", results.Sequence().Error.Message);
        Assert.Equal(2, read);
        Assert.Equal("bad 2", _items.Select(Check).Sequence().Error.Message);
        Assert.Equal("bad 2", _items.Select(ParseCustom).Sequence().Error);
    }

    [Fact]
    public async Task SequenceAsync_StopsAwaitingAtFirstFailure()
    {
        var started = 0;
        IEnumerable<Task<Result<int>>> tasks = _items.Select(item =>
        {
            started++;
            return Task.FromResult(Parse(item));
        });

        Result<IReadOnlyList<int>> result = await tasks.SequenceAsync();

        Assert.Equal("bad 2", result.Error.Message);
        Assert.Equal(2, started);
    }

    [Fact]
    public async Task SequenceAsync_AllForms_ReturnValuesOrFirstFailure()
    {
        Result<IReadOnlyList<int>> values = await new[] { 1, 3 }.Select(item => Task.FromResult(Parse(item))).SequenceAsync();
        Result payloadFree = await _items.Select(item => Task.FromResult(Check(item))).SequenceAsync();
        Result passed = await new[] { 1, 3 }.Select(item => Task.FromResult(Check(item))).SequenceAsync();
        Result<IReadOnlyList<int>, string> custom = await _items.Select(item => Task.FromResult(ParseCustom(item))).SequenceAsync();

        Assert.Equal([10, 30], values.Value);
        Assert.Equal("bad 2", payloadFree.Error.Message);
        Assert.True(passed.IsSuccess);
        Assert.Equal("bad 2", custom.Error);
    }

    // -- Partition --

    [Fact]
    public void Partition_KeepsEveryValueAndEveryError()
    {
        (IReadOnlyList<int> values, IReadOnlyList<Error> errors) = _items.Select(Parse).Partition();

        Assert.Equal([10, 30], values);
        Assert.Equal(["bad 2", "bad 4"], errors.Select(error => error.Message));
    }

    [Fact]
    public void Partition_WithStep_RunsEveryItem()
    {
        (IReadOnlyList<int> values, IReadOnlyList<Error> errors) = _items.Partition(Parse);
        IReadOnlyList<Error> payloadFree = _items.Partition(Check);
        (IReadOnlyList<int> customValues, IReadOnlyList<string> customErrors) = _items.Partition(ParseCustom);

        Assert.Equal([10, 30], values);
        Assert.Equal(2, errors.Count);
        Assert.Equal(["bad 2", "bad 4"], payloadFree.Select(error => error.Message));
        Assert.Equal([10, 30], customValues);
        Assert.Equal(["bad 2", "bad 4"], customErrors);
    }

    [Fact]
    public void Partition_ResultLists_KeepEveryError()
    {
        Assert.Equal(2, _items.Select(Check).Partition().Count);
        Assert.Empty(new[] { 1, 3 }.Select(Check).Partition());
        Assert.Equal(["bad 2", "bad 4"], _items.Select(ParseCustom).Partition().Errors);
    }

    [Fact]
    public async Task PartitionAsync_WithStep_RunsEveryItemOneAtATime()
    {
        var running = 0;
        var maxRunning = 0;

        (IReadOnlyList<int> values, IReadOnlyList<Error> errors) = await _items.PartitionAsync(async item =>
        {
            maxRunning = Math.Max(maxRunning, ++running);
            await Task.Yield();
            running--;
            return Parse(item);
        });

        Assert.Equal([10, 30], values);
        Assert.Equal(2, errors.Count);
        Assert.Equal(1, maxRunning);
    }

    [Fact]
    public async Task PartitionAsync_AllForms_KeepEveryValueAndError()
    {
        IReadOnlyList<Error> payloadFree = await _items.PartitionAsync(item => Task.FromResult(Check(item)));
        (IReadOnlyList<int> customValues, IReadOnlyList<string> customErrors) = await _items.PartitionAsync(item => Task.FromResult(ParseCustom(item)));
        (IReadOnlyList<int> taskValues, IReadOnlyList<Error> taskErrors) = await _items.Select(item => Task.FromResult(Parse(item))).PartitionAsync();
        IReadOnlyList<Error> taskPayloadFree = await _items.Select(item => Task.FromResult(Check(item))).PartitionAsync();
        (IReadOnlyList<int> taskCustomValues, IReadOnlyList<string> taskCustomErrors) = await _items.Select(item => Task.FromResult(ParseCustom(item))).PartitionAsync();

        Assert.Equal(2, payloadFree.Count);
        Assert.Equal([10, 30], customValues);
        Assert.Equal(["bad 2", "bad 4"], customErrors);
        Assert.Equal([10, 30], taskValues);
        Assert.Equal(2, taskErrors.Count);
        Assert.Equal(2, taskPayloadFree.Count);
        Assert.Equal([10, 30], taskCustomValues);
        Assert.Equal(["bad 2", "bad 4"], taskCustomErrors);
    }

    // -- Guards --

    [Fact]
    public async Task NullArguments_ThrowWithExactParameter()
    {
        IEnumerable<int> noItems = null!;
        IEnumerable<Result<int>> noResults = null!;
        IEnumerable<Task<Result>> noTasks = null!;

        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => noItems.Traverse(Ok)).ParamName);
        Assert.Equal("step", Assert.Throws<ArgumentNullException>(() => _items.Traverse(null!)).ParamName);
        Assert.Equal("step", Assert.Throws<ArgumentNullException>(() => _items.Partition(null!)).ParamName);
        Assert.Equal("results", Assert.Throws<ArgumentNullException>(() => noResults.Sequence()).ParamName);
        Assert.Equal("results", Assert.Throws<ArgumentNullException>(() => noResults.Partition()).ParamName);
        Assert.Equal("tasks", (await Assert.ThrowsAsync<ArgumentNullException>(noTasks.SequenceAsync)).ParamName);
        Assert.Equal("tasks", (await Assert.ThrowsAsync<ArgumentNullException>(() => new Task<Result>[] { null! }.PartitionAsync())).ParamName);
        Assert.Equal("step", (await Assert.ThrowsAsync<ArgumentNullException>(() => _items.TraverseAsync(null!))).ParamName);
    }
}
