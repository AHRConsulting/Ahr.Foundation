using System.Reflection;

namespace Ahr.Foundation.Analyzers.Tests;

/// <summary>
/// A compiler only loads an analyzer whose referenced Roslyn and host assemblies are no newer than
/// its own; otherwise it skips the analyzer with CS9057. These tests keep the bundled analyzers
/// loadable from the .NET 6 SDK and Visual Studio 2022 17.0 onward, whatever SDK builds them.
/// </summary>
public sealed class AnalyzerCompilerFloorTests
{
    private static readonly BusinessContext _analyzerCompatibility = BusinessContext
        .Define("analyzer-compiler-compatibility", "Analyzer compiler compatibility", "Keep analyzer dependencies loadable by supported compiler hosts.")
        .Capability("load-on-supported-compilers", "Load the analyzer on supported compiler versions")
        .Outcome("compiler-host-loads-analyzer", "Supported compiler hosts can load the analyzer assembly")
        .Rule("dependency-versions-within-host-floor", "Analyzer dependencies do not exceed the supported compiler floor")
        .Build();

    private static readonly Version _roslynFloor = new(4, 0, 0, 0);

    private static AssemblyName[] AnalyzerReferences() =>
        typeof(DiscardedResultAnalyzer).Assembly.GetReferencedAssemblies();

    [Fact]
    public Task AnalyzerAssembly_RoslynReferences_DoNotExceedTheCompilerFloor() =>
        Scenario.Define("Analyzer Roslyn references stay within the supported compiler floor", _analyzerCompatibility)
            .When("The analyzer's Roslyn references are read", () =>
                AnalyzerReferences()
                    .Where(reference => reference.Name!.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))
                    .ToArray())
            .Then("The analyzer has compiler references", (AssemblyName[] roslyn) =>
                Assert.NotEmpty(roslyn))
            .And($"None is newer than {_roslynFloor}", (AssemblyName[] roslyn) =>
                Assert.DoesNotContain(roslyn, reference => reference.Version is null || reference.Version > _roslynFloor))
            .VerifyAsync();

    [Theory]
    [InlineData("Immutable collection reference", "System.Collections.Immutable", "5.0.0.0")]
    [InlineData("Composition host reference", "System.Composition.AttributedModel", "1.0.31.0")]
    public Task AnalyzerAssembly_HostReferences_DoNotExceedTheCompilerFloor(string example, string assemblyName, string floorVersion)
    {
        var floor = Version.Parse(floorVersion);

        return Scenario.Define("Analyzer host references stay within supported compiler floors", _analyzerCompatibility)
            .When($"The analyzer's {example} is inspected", () =>
                AnalyzerReferences().SingleOrDefault(candidate => candidate.Name == assemblyName))
            .Then($"The host dependency is present and no newer than {floor}", (AssemblyName? reference) =>
                Assert.True(
                    reference?.Version is { } version && version <= floor,
                    $"{assemblyName} {reference?.Version} is missing or newer than the analyzer floor {floor}."))
            .VerifyAsync();
    }
}
