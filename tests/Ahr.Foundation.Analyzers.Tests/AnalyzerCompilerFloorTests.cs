using System.Reflection;

namespace Ahr.Foundation.Analyzers.Tests;

/// <summary>
/// A compiler only loads an analyzer whose referenced Roslyn and host assemblies are no newer than
/// its own; otherwise it skips the analyzer with CS9057. These tests keep the bundled analyzers
/// loadable from the .NET 6 SDK and Visual Studio 2022 17.0 onward, whatever SDK builds them.
/// </summary>
public sealed class AnalyzerCompilerFloorTests
{
    private static readonly Version _roslynFloor = new(4, 0, 0, 0);
    private static readonly Version _immutableFloor = new(5, 0, 0, 0);
    private static readonly Version _compositionFloor = new(1, 0, 31, 0);

    private static AssemblyName[] AnalyzerReferences() =>
        typeof(DiscardedResultAnalyzer).Assembly.GetReferencedAssemblies();

    [Fact]
    public void AnalyzerAssembly_RoslynReferences_DoNotExceedTheCompilerFloor()
    {
        AssemblyName[] roslyn = [.. AnalyzerReferences().Where(reference => reference.Name!.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal))];

        Assert.NotEmpty(roslyn);
        Assert.All(roslyn, reference => Assert.True(
            reference.Version <= _roslynFloor,
            $"{reference.Name} {reference.Version} is newer than the analyzer floor {_roslynFloor}."));
    }

    [Theory]
    [InlineData("System.Collections.Immutable")]
    [InlineData("System.Composition.AttributedModel")]
    public void AnalyzerAssembly_HostReferences_DoNotExceedTheCompilerFloor(string assemblyName)
    {
        Version floor = assemblyName == "System.Collections.Immutable" ? _immutableFloor : _compositionFloor;

        AssemblyName? reference = AnalyzerReferences().SingleOrDefault(candidate => candidate.Name == assemblyName);

        Assert.NotNull(reference);
        Assert.True(
            reference.Version <= floor,
            $"{assemblyName} {reference.Version} is newer than the analyzer floor {floor}.");
    }
}
