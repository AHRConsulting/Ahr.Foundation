using BenchmarkDotNet.Attributes;

namespace Ahr.Foundation.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
public class ResultConstructionBenchmarks
{
    private static readonly Error _error = new("failure");

    [Benchmark]
    [BenchmarkCategory("PayloadFree", "Success")]
    public Result PayloadFreeSuccess() => Result.Success();

    [Benchmark]
    [BenchmarkCategory("PayloadFree", "Failure")]
    public Result PayloadFreeFailure() => Result.Failure(_error);

    [Benchmark]
    [BenchmarkCategory("StandardError", "Primitive", "Success")]
    public Result<int> StandardPrimitiveSuccess() => Result<int>.Success(42);

    [Benchmark]
    [BenchmarkCategory("StandardError", "Primitive", "Failure")]
    public Result<int> StandardPrimitiveFailure() => Result<int>.Failure(_error);

    [Benchmark]
    [BenchmarkCategory("StandardError", "Complex", "Success")]
    public Result<Order> StandardComplexSuccess() => Result<Order>.Success(BenchmarkModels._order);

    [Benchmark]
    [BenchmarkCategory("StandardError", "Complex", "Failure")]
    public Result<Order> StandardComplexFailure() => Result<Order>.Failure(_error);

    [Benchmark]
    [BenchmarkCategory("CustomError", "Primitive", "Success")]
    public Result<int, string> CustomPrimitiveSuccess() => Result<int, string>.Success(42);

    [Benchmark]
    [BenchmarkCategory("CustomError", "Primitive", "Failure")]
    public Result<int, string> CustomPrimitiveFailure() => Result<int, string>.Failure("failure");

    [Benchmark]
    [BenchmarkCategory("CustomError", "Complex", "Success")]
    public Result<Order, DomainError> CustomComplexSuccess() =>
        Result<Order, DomainError>.Success(BenchmarkModels._order);

    [Benchmark]
    [BenchmarkCategory("CustomError", "Complex", "Failure")]
    public Result<Order, DomainError> CustomComplexFailure() =>
        Result<Order, DomainError>.Failure(BenchmarkModels._error);

    [Benchmark]
    [BenchmarkCategory("Try", "PayloadFree", "Success")]
    public Result TryPayloadFreeSuccess() => Result.Try(static () => { });

    [Benchmark]
    [BenchmarkCategory("Try", "PayloadFree", "Failure")]
    public Result TryPayloadFreeFailure() => Result.Try(static () => throw new InvalidOperationException("failure"));

    [Benchmark]
    [BenchmarkCategory("Try", "StandardError", "Success")]
    public Result<int> TryStandardSuccess() => Result.Try(static () => 42);

    [Benchmark]
    [BenchmarkCategory("Try", "StandardError", "Failure")]
    public Result<int> TryStandardFailure() => Result.Try<int>(static () => throw new InvalidOperationException("failure"));

    [Benchmark]
    [BenchmarkCategory("Try", "CustomError", "Success")]
    public Result<int, DomainError> TryCustomSuccess() => Result.Try(static () => 42, static ex => new DomainError(ex.Message, new ErrorContext("Try", [])));

    [Benchmark]
    [BenchmarkCategory("Try", "CustomError", "Failure")]
    public Result<int, DomainError> TryCustomFailure() =>
        Result.Try<int, DomainError>(static () => throw new InvalidOperationException("failure"), static ex => new DomainError(ex.Message, new ErrorContext("Try", [])));
}
