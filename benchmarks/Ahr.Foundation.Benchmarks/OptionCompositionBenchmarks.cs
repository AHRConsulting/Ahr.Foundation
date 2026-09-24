using BenchmarkDotNet.Attributes;

namespace Ahr.Foundation.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
public class OptionCompositionBenchmarks
{
    private static readonly Error _error = new("missing");
    private static readonly Option<int> _primitiveSome = Option<int>.Some(42);
    private static readonly Option<int> _primitiveNone = Option<int>.None;
    private static readonly Option<Order> _complexSome = Option<Order>.Some(BenchmarkModels._order);
    private static readonly Option<Order> _complexNone = Option<Order>.None;

    [Benchmark]
    [BenchmarkCategory("Option", "Primitive", "Some", "Match")]
    public int PrimitiveMatchSome() => _primitiveSome.Match(static value => value + 1, static () => 0);

    [Benchmark]
    [BenchmarkCategory("Option", "Primitive", "None", "Match")]
    public int PrimitiveMatchNone() => _primitiveNone.Match(static value => value + 1, static () => 0);

    [Benchmark]
    [BenchmarkCategory("Option", "Primitive", "Some", "Map")]
    public Option<int> PrimitiveMapSome() => _primitiveSome.Map(static value => value + 1);

    [Benchmark]
    [BenchmarkCategory("Option", "Primitive", "None", "Map")]
    public Option<int> PrimitiveMapNone() => _primitiveNone.Map(static value => value + 1);

    [Benchmark]
    [BenchmarkCategory("Option", "Primitive", "Some", "Bind")]
    public Option<int> PrimitiveBindSome() =>
        _primitiveSome.Bind(static value => Option<int>.Some(value + 1));

    [Benchmark]
    [BenchmarkCategory("Option", "Primitive", "None", "Bind")]
    public Option<int> PrimitiveBindNone() =>
        _primitiveNone.Bind(static value => Option<int>.Some(value + 1));

    [Benchmark]
    [BenchmarkCategory("Option", "Complex", "Some", "Map")]
    public Option<int> ComplexMapSome() =>
        _complexSome.Map(static order => order.Lines.Count);

    [Benchmark]
    [BenchmarkCategory("Option", "Complex", "None", "Bind")]
    public Option<Order> ComplexBindNone() =>
        _complexNone.Bind(static order => Option<Order>.Some(order));

    [Benchmark]
    [BenchmarkCategory("Option", "Primitive", "Some", "Where")]
    public Option<int> PrimitiveWhereSome() =>
        _primitiveSome.Where(static value => value > 0);

    [Benchmark]
    [BenchmarkCategory("Option", "Complex", "None", "ToResult")]
    public Result<Order> ComplexToResultNone() => _complexNone.ToResult(_error);
}
