using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

internal class Program
{
    private static void Main(string[] args)
    {
        IConfig config = DefaultConfig.Instance.WithArtifactsPath(Path.Combine("artifacts", "benchmarks"));
        _ = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
    }
}
