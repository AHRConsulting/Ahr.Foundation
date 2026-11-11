using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Ahr.Foundation.Benchmarks;

internal static class Program
{
    private static void Main(string[] args)
    {
        IConfig config = DefaultConfig.Instance.WithArtifactsPath(Path.Combine("artifacts", "benchmarks"));
        _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}
