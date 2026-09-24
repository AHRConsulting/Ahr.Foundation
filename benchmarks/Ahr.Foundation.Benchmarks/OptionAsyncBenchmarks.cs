using BenchmarkDotNet.Attributes;

namespace Ahr.Foundation.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
public class OptionAsyncBenchmarks
{
    private static readonly Option<int> _primitiveSome = Option<int>.Some(42);
    private static readonly Option<int> _primitiveNone = Option<int>.None;
    private static readonly Option<Order> _complexSome = Option<Order>.Some(BenchmarkModels._order);
    private static readonly Option<Order> _complexNone = Option<Order>.None;

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Option", "Async", "Baseline")]
    public Task<int> CompletedTaskBaseline() => Task.FromResult(43);

    [Benchmark]
    [BenchmarkCategory("Option", "Async", "Primitive", "Some", "Map")]
    public Task<Option<int>> PrimitiveMapSome() =>
        _primitiveSome.MapAsync(static value => Task.FromResult(value + 1));

    [Benchmark]
    [BenchmarkCategory("Option", "Async", "Primitive", "None", "Map")]
    public Task<Option<int>> PrimitiveMapNone() =>
        _primitiveNone.MapAsync(static value => Task.FromResult(value + 1));

    [Benchmark]
    [BenchmarkCategory("Option", "Async", "Primitive", "Some", "Bind")]
    public Task<Option<int>> PrimitiveBindSome() =>
        _primitiveSome.BindAsync(static value => Task.FromResult(Option<int>.Some(value + 1)));

    [Benchmark]
    [BenchmarkCategory("Option", "Async", "Complex", "Some", "Map")]
    public Task<Option<int>> ComplexMapSome() =>
        _complexSome.MapAsync(static order => Task.FromResult(order.Lines.Count));

    [Benchmark]
    [BenchmarkCategory("Option", "Async", "Complex", "None", "Bind")]
    public Task<Option<Order>> ComplexBindNone() =>
        _complexNone.BindAsync(static order => Task.FromResult(Option<Order>.Some(order)));
}
