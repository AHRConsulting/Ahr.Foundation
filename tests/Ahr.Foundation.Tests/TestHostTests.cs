using System.Text.Json;

namespace Ahr.Foundation.Tests;

public sealed class TestHostTests
{
    private static readonly BusinessContext _technical = BusinessContext
        .Define("technical", "Technical checks", "Verify test-host wiring that is not part of Foundation's documented behavior.")
        .Capability("verify-test-host", "Verify the test host runs Foundation from the local project")
        .Outcome("local-project-loaded", "The test host resolves Foundation from the project under test")
        .Rule("foundation-project-reference", "The test dependency graph must resolve Foundation as a project")
        .Build();

    [Fact]
    public Task DependencyGraph_FoundationReference_ResolvesToLocalProject()
    {
        string? libraryType = null;

        return Scenario.Define("Functional tests load Foundation from the local project", _technical)
            .When("The test host dependency manifest is inspected", () =>
            {
                libraryType = ReadFoundationLibraryType();
            })
            .Then("Foundation resolves as the project under test", () =>
                Assert.Equal("project", libraryType))
            .VerifyAsync();
    }

    private static string? ReadFoundationLibraryType()
    {
        var manifestPath = Path.Combine(
            AppContext.BaseDirectory,
            $"{typeof(TestHostTests).Assembly.GetName().Name}.deps.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        return manifest.RootElement
            .GetProperty("libraries")
            .EnumerateObject()
            .Single(library => library.Name.StartsWith("Ahr.Foundation/", StringComparison.Ordinal))
            .Value
            .GetProperty("type")
            .GetString();
    }
}
