using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Ahr.Foundation.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
public class ResultCompositionBenchmarks
{
    private static readonly Error _error = new("failure");
    private static readonly Result _payloadFreeSuccess = Result.Success();
    private static readonly Result _payloadFreeFailure = Result.Failure(_error);
    private static readonly Result<int> _standardSuccess = Result<int>.Success(42);
    private static readonly Result<int> _standardFailure = Result<int>.Failure(_error);
    private static readonly Result<Order, DomainError> _customSuccess =
        Result<Order, DomainError>.Success(BenchmarkModels._order);
    private static readonly Result<Order, DomainError> _customFailure =
        Result<Order, DomainError>.Failure(BenchmarkModels._error);

    private readonly Consumer _consumer = new();

    [Benchmark]
    [BenchmarkCategory("PayloadFree", "Success", "Match")]
    public int PayloadFreeMatchSuccess() => _payloadFreeSuccess.Match(static () => 1, static _ => 0);

    [Benchmark]
    [BenchmarkCategory("PayloadFree", "Failure", "Bind")]
    public Result PayloadFreeBindFailure() => _payloadFreeFailure.Bind(static () => Result.Success());

    [Benchmark]
    [BenchmarkCategory("StandardError", "Success", "Map")]
    public Result<int> StandardMapSuccess() => _standardSuccess.Map(static value => value + 1);

    [Benchmark]
    [BenchmarkCategory("StandardError", "Success", "Bind")]
    public Result<int> StandardBindSuccess() =>
        _standardSuccess.Bind(static value => Result<int>.Success(value + 1));

    [Benchmark]
    [BenchmarkCategory("StandardError", "Success", "Ensure")]
    public Result<int> StandardEnsureSuccess() =>
        _standardSuccess.Ensure(static value => value > 0, _error);

    [Benchmark]
    [BenchmarkCategory("StandardError", "Success", "Tap")]
    public Result<int> StandardTapSuccess() =>
        _standardSuccess.Tap(_consumer.Consume);

    [Benchmark]
    [BenchmarkCategory("StandardError", "Failure", "Map")]
    public Result<int> StandardMapFailure() => _standardFailure.Map(static value => value + 1);

    [Benchmark]
    [BenchmarkCategory("StandardError", "Failure", "Bind")]
    public Result<int> StandardBindFailure() =>
        _standardFailure.Bind(static value => Result<int>.Success(value + 1));

    [Benchmark]
    [BenchmarkCategory("CustomError", "Complex", "Success", "Match")]
    public int CustomComplexMatchSuccess() =>
        _customSuccess.Match(static order => order.Lines.Count, static _ => 0);

    [Benchmark]
    [BenchmarkCategory("CustomError", "Complex", "Success", "Map")]
    public Result<int, DomainError> CustomComplexMapSuccess() =>
        _customSuccess.Map(static order => order.Lines.Count);

    [Benchmark]
    [BenchmarkCategory("CustomError", "Complex", "Failure", "Bind")]
    public Result<Order, DomainError> CustomComplexBindFailure() =>
        _customFailure.Bind(static order => Result<Order, DomainError>.Success(order));
}
