using BenchmarkDotNet.Attributes;

namespace Ahr.Foundation.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
public class ResultAsyncBenchmarks
{
    private static readonly Error _error = new("failure");
    private static readonly Result _payloadFreeSuccess = Result.Success();
    private static readonly Result<int> _standardSuccess = Result<int>.Success(42);
    private static readonly Result<int> _standardFailure = Result<int>.Failure(_error);
    private static readonly Result<Order, DomainError> _customSuccess =
        Result<Order, DomainError>.Success(BenchmarkModels._order);
    private static readonly Result<Order, DomainError> _customFailure =
        Result<Order, DomainError>.Failure(BenchmarkModels._error);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Async", "Baseline")]
    public Task<int> CompletedTaskBaseline() => Task.FromResult(43);

    [Benchmark]
    [BenchmarkCategory("Async", "PayloadFree", "Success", "Bind")]
    public Task<Result> PayloadFreeBindSuccess() =>
        _payloadFreeSuccess.BindAsync(static () => Task.FromResult(Result.Success()));

    [Benchmark]
    [BenchmarkCategory("Async", "StandardError", "Success", "Map")]
    public Task<Result<int>> StandardMapSuccess() =>
        _standardSuccess.MapAsync(static value => Task.FromResult(value + 1));

    [Benchmark]
    [BenchmarkCategory("Async", "StandardError", "Failure", "Bind")]
    public Task<Result<int>> StandardBindFailure() =>
        _standardFailure.BindAsync(static value => Task.FromResult(Result<int>.Success(value + 1)));

    [Benchmark]
    [BenchmarkCategory("Async", "CustomError", "Complex", "Success", "Map")]
    public Task<Result<int, DomainError>> CustomComplexMapSuccess() =>
        _customSuccess.MapAsync(static order => Task.FromResult(order.Lines.Count));

    [Benchmark]
    [BenchmarkCategory("Async", "CustomError", "Complex", "Failure", "Bind")]
    public Task<Result<Order, DomainError>> CustomComplexBindFailure() =>
        _customFailure.BindAsync(static order => Task.FromResult(Result<Order, DomainError>.Success(order)));

    [Benchmark]
    [BenchmarkCategory("Async", "TryAsync", "PayloadFree", "Success")]
    public Task<Result> TryAsyncPayloadFreeSuccess() => Result.TryAsync(static () => Task.CompletedTask);

    [Benchmark]
    [BenchmarkCategory("Async", "TryAsync", "PayloadFree", "Failure")]
    public Task<Result> TryAsyncPayloadFreeFailure() =>
        Result.TryAsync(static () => Task.FromException(new InvalidOperationException("failure")));

    [Benchmark]
    [BenchmarkCategory("Async", "TryAsync", "StandardError", "Success")]
    public Task<Result<int>> TryAsyncStandardSuccess() => Result.TryAsync(static () => Task.FromResult(42));

    [Benchmark]
    [BenchmarkCategory("Async", "TryAsync", "StandardError", "Failure")]
    public Task<Result<int>> TryAsyncStandardFailure() =>
        Result.TryAsync(static () => Task.FromException<int>(new InvalidOperationException("failure")));
}
