using BenchmarkDotNet.Attributes;

namespace Ahr.Foundation.Benchmarks;

[MemoryDiagnoser]
[CategoriesColumn]
public class OptionConstructionBenchmarks
{
    [Benchmark]
    [BenchmarkCategory("Option", "Primitive", "Some")]
    public Option<int> PrimitiveSome() => Option<int>.Some(42);

    [Benchmark]
    [BenchmarkCategory("Option", "Primitive", "None")]
    public Option<int> PrimitiveNone() => Option<int>.None;

    [Benchmark]
    [BenchmarkCategory("Option", "Complex", "Some")]
    public Option<Order> ComplexSome() => Option<Order>.Some(BenchmarkModels._order);

    [Benchmark]
    [BenchmarkCategory("Option", "Complex", "None")]
    public Option<Order> ComplexNone() => Option<Order>.None;
}
