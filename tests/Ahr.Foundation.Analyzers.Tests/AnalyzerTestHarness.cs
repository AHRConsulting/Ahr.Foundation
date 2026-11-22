using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Ahr.Foundation.Analyzers.Tests;

internal static class AnalyzerTestHarness
{
    internal const string TestDocumentName = "TestClass.cs";

    private static readonly string _runtimeDirectory =
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(RuntimeEnvironment.GetRuntimeDirectory()));

    private static readonly ImmutableArray<MetadataReference> _platformReferences =
    [
        ..
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator)
        .Where(IsSharedFrameworkAssembly)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
    ];

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
        return definitionsDocument.Project.AddDocument(TestDocumentName, source).Project;
    }

    internal static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        DiagnosticAnalyzer analyzer,
        string definitions,
        string source) =>
        AnalyzeCompilationAsync(CreateCompilation(definitions, source), analyzer, TestContext.Current.CancellationToken);

    internal static async Task<ImmutableArray<Diagnostic>> AnalyzeProjectAsync(
        Project project,
        DiagnosticAnalyzer analyzer,
        CancellationToken cancellationToken)
    {
        Compilation compilation = await project.GetCompilationAsync(cancellationToken)
            ?? throw new InvalidOperationException("The test project could not be compiled.");
        return await AnalyzeCompilationAsync(compilation, analyzer, cancellationToken);
    }

    internal static Document GetTestDocument(Project project) =>
        project.Documents.Single(candidate => candidate.Name == TestDocumentName);

    /// <summary>
    /// Analyzes the test document, registers fixes when exactly one diagnostic is reported, and applies the fix
    /// when exactly one is offered. Nothing here asserts: each later field is empty or null when an earlier stage
    /// did not produce exactly one item, so the scenario's Then and And steps report what went wrong.
    /// </summary>
    internal static async Task<CodeFixOutcome> RunCodeFixAsync(
        Project project,
        DiagnosticAnalyzer analyzer,
        CodeFixProvider provider,
        CancellationToken cancellationToken)
    {
        Document document = GetTestDocument(project);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeProjectAsync(project, analyzer, cancellationToken);
        IReadOnlyList<CodeAction> actions = diagnostics is [Diagnostic diagnostic]
            ? await RegisterCodeFixesAsync(provider, document, diagnostic, cancellationToken)
            : [];
        string[] actionTitles = [.. actions.Select(action => action.Title)];
        if (actions is not [CodeAction fix])
        {
            return new(diagnostics, actionTitles, [], null, []);
        }

        ImmutableArray<CodeActionOperation> operations = await fix.GetOperationsAsync(cancellationToken);
        var fixedSource = await ApplyAsync(document, operations, cancellationToken);
        if (fixedSource is null)
        {
            return new(diagnostics, actionTitles, GetOperationNames(operations), null, []);
        }

        Project fixedProject = project.Solution
            .WithDocumentText(document.Id, SourceText.From(fixedSource))
            .GetProject(project.Id)!;
        ImmutableArray<Diagnostic> diagnosticsAfterFix = await AnalyzeProjectAsync(fixedProject, analyzer, cancellationToken);
        return new(diagnostics, actionTitles, GetOperationNames(operations), fixedSource, diagnosticsAfterFix);
    }

    internal static async Task<IReadOnlyList<CodeAction>> RegisterCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        List<CodeAction> actions = [];
        CodeFixContext context = new(document, diagnostic, (action, _) => actions.Add(action), cancellationToken);
        await provider.RegisterCodeFixesAsync(context);
        return actions;
    }

    internal static string[] GetOperationNames(ImmutableArray<CodeActionOperation> operations) =>
        [.. operations.Select(operation => operation.GetType().Name)];

    /// <summary>
    /// Returns the test document's text after the operations, or null unless they are exactly one
    /// <see cref="ApplyChangesOperation"/> that keeps the test document.
    /// </summary>
    internal static async Task<string?> ApplyAsync(
        Document document,
        ImmutableArray<CodeActionOperation> operations,
        CancellationToken cancellationToken)
    {
        if (operations is not [ApplyChangesOperation apply]
            || apply.ChangedSolution.GetDocument(document.Id) is not { } changed)
        {
            return null;
        }

        SourceText text = await changed.GetTextAsync(cancellationToken);
        return text.ToString();
    }

    internal static Diagnostic ReportsSingle(ImmutableArray<Diagnostic> diagnostics, string id)
    {
        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal(id, diagnostic.Id);
        return diagnostic;
    }

    internal static Diagnostic ReportsSingle(ImmutableArray<Diagnostic> diagnostics, string id, DiagnosticSeverity severity)
    {
        Diagnostic diagnostic = ReportsSingle(diagnostics, id);
        Assert.Equal(severity, diagnostic.Severity);
        return diagnostic;
    }

    private static bool IsSharedFrameworkAssembly(string path) =>
        string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), _runtimeDirectory, StringComparison.Ordinal);

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeCompilationAsync(
        Compilation compilation,
        DiagnosticAnalyzer analyzer,
        CancellationToken cancellationToken)
    {
        Diagnostic[] compileErrors = [.. compilation.GetDiagnostics(cancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
        if (compileErrors.Length > 0)
        {
            throw new InvalidOperationException(
                $"The test snippet must compile before it is analyzed, but it has {compileErrors.Length} error(s):{Environment.NewLine}{string.Join(Environment.NewLine, compileErrors.Select(error => error.ToString()))}");
        }

        return await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync(cancellationToken);
    }

    private static CSharpCompilation CreateCompilation(string definitions, string source) =>
        CSharpCompilation.Create(
            "AnalyzerTests",
            [CSharpSyntaxTree.ParseText(definitions), CSharpSyntaxTree.ParseText(source)],
            _platformReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
}
