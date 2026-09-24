using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class DefaultResultConstructionCodeFixProviderTests
{
    private const string ResultDefinitions = """
        namespace Ahr.Foundation
        {
            public readonly struct Result
            {
                public static Result Success() => new Result(1);
                private Result(int _) { }
            }
        }
        """;

    [Fact]
    public async Task CodeFix_ResultDefault_ReplacesWithResultSuccess()
    {
        const string initialSource = """
            namespace Ahr.Foundation;
            class TestClass
            {
                Result GetResult() => default(Result);
            }
            """;

        AdhocWorkspace workspace = new();
        Project project = AnalyzerTestHarness.CreateProject(workspace, ResultDefinitions, initialSource);
        Document testDoc = project.Documents.Single(document => document.Name == "TestClass.cs");

        Compilation compilation = (await testDoc.Project.GetCompilationAsync(TestContext.Current.CancellationToken))!;
        CompilationWithAnalyzers compilationWithAnalyzers = compilation.WithAnalyzers([new DefaultResultConstructionAnalyzer()]);
        ImmutableArray<Diagnostic> diagnostics = await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        Diagnostic diagnostic = Assert.Single(diagnostics, d => d.Location.SourceTree?.FilePath == "TestClass.cs");

        List<CodeAction> registeredActions = [];
        CodeFixContext context = new(
            testDoc,
            diagnostic,
            (action, _) => registeredActions.Add(action),
            TestContext.Current.CancellationToken);

        DefaultResultConstructionCodeFixProvider provider = new();
        await provider.RegisterCodeFixesAsync(context);

        CodeAction action = Assert.Single(registeredActions);
        Assert.Equal("Use 'Result.Success()'", action.Title);

        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(TestContext.Current.CancellationToken);
        ApplyChangesOperation applyOperation = Assert.IsType<ApplyChangesOperation>(Assert.Single(operations));
        Document newDoc = applyOperation.ChangedSolution.GetDocument(testDoc.Id)!;
        var newText = (await newDoc.GetTextAsync(TestContext.Current.CancellationToken)).ToString();

        Assert.Contains("Result.Success()", newText);
    }
}
