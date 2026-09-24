using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ahr.Foundation.Analyzers.Tests;

internal static class AnalyzerTestHarness
{
    private static readonly ImmutableArray<MetadataReference> _platformReferences =
    [
        ..
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
    ];

    internal static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        DiagnosticAnalyzer analyzer,
        string definitions,
        string source)
    {
        CSharpCompilation compilation = CreateCompilation(definitions, source);
        return await compilation
            .WithAnalyzers([analyzer])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    internal static Project CreateProject(
        AdhocWorkspace workspace,
        string definitions,
        string source)
    {
        Project project = workspace
            .AddProject("TestProject", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReferences(_platformReferences);

        Document definitionsDocument = project.AddDocument("Definitions.cs", definitions);
        return definitionsDocument.Project.AddDocument("TestClass.cs", source).Project;
    }

    private static CSharpCompilation CreateCompilation(string definitions, string source) =>
        CSharpCompilation.Create(
            "AnalyzerTests",
            [CSharpSyntaxTree.ParseText(definitions), CSharpSyntaxTree.ParseText(source)],
            _platformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}
