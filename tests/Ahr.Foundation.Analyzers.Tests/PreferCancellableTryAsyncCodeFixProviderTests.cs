using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class PreferCancellableTryAsyncCodeFixProviderTests
{
    [Fact]
    public async Task CodeFix_ExpressionLambda_PassesTokenToCancellableOverload()
    {
        var fixedSource = await ApplyFixAsync(
            "Task M(Client client, CancellationToken ct) => Result.TryAsync(() => client.GetAsync(\"u\", ct));");

        Assert.Contains("Result.TryAsync(token => client.GetAsync(\"u\", token), ct)", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CodeFix_AsyncBlockLambda_ReplacesEveryReference()
    {
        var fixedSource = await ApplyFixAsync("""
            Task M(Client client, CancellationToken ct) => Result.TryAsync(async () =>
            {
                await client.SaveAsync(ct);
                return await client.GetAsync("u", ct);
            });
            """);

        Assert.Contains("Result.TryAsync(async token =>", fixedSource, StringComparison.Ordinal);
        Assert.Contains("await client.SaveAsync(token);", fixedSource, StringComparison.Ordinal);
        Assert.Contains("await client.GetAsync(\"u\", token);", fixedSource, StringComparison.Ordinal);
        Assert.Contains("}, ct);", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CodeFix_ErrorFactoryOverload_KeepsFactoryAndAppendsToken()
    {
        var fixedSource = await ApplyFixAsync(
            "Task M(Client client, Context context) => Result.TryAsync(() => client.GetAsync(\"u\", context.RequestAborted), ex => ex.Message);");

        Assert.Contains("Result.TryAsync(token => client.GetAsync(\"u\", token), ex => ex.Message, context.RequestAborted)", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CodeFix_NameInScope_ChoosesFreeParameterName()
    {
        var fixedSource = await ApplyFixAsync(
            "Task M(Client client, CancellationToken token) => Result.TryAsync(() => client.GetAsync(\"u\", token));");

        Assert.Contains("Result.TryAsync(cancellationToken => client.GetAsync(\"u\", cancellationToken), token)", fixedSource, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CodeFix_TwoDistinctTokens_OffersNoFix()
    {
        (Document document, Diagnostic diagnostic) = await GetDiagnosticAsync(
            "Task M(Client client, CancellationToken a, CancellationToken b) => Result.TryAsync(async () => { await client.SaveAsync(a); return await client.GetAsync(\"u\", b); });");

        List<CodeAction> actions = await RegisterAsync(document, diagnostic);
        Assert.Empty(actions);
    }

    [Fact]
    public async Task CodeFix_FixAll_FixesEveryCallInDocument()
    {
        (Document document, _) = await GetDiagnosticAsync("""
            void M(Client client, CancellationToken ct)
            {
                _ = Result.TryAsync(() => client.GetAsync("a", ct));
                _ = Result.TryAsync(() => client.SaveAsync(ct));
            }
            """);

        PreferCancellableTryAsyncCodeFixProvider provider = new();
        FixAllContext fixAllContext = new(
            document,
            provider,
            FixAllScope.Document,
            "Pass the cancellation token to TryAsync",
            [PreferCancellableTryAsyncAnalyzer.DiagnosticId],
            new DocumentDiagnosticProvider(),
            TestContext.Current.CancellationToken);

        CodeAction? action = await provider.GetFixAllProvider().GetFixAsync(fixAllContext);
        Assert.NotNull(action);
        var fixedSource = await ApplyAsync(document, action);

        Assert.Contains("Result.TryAsync(token => client.GetAsync(\"a\", token), ct)", fixedSource, StringComparison.Ordinal);
        Assert.Contains("Result.TryAsync(token => client.SaveAsync(token), ct)", fixedSource, StringComparison.Ordinal);
    }

    private static async Task<string> ApplyFixAsync(string member)
    {
        (Document document, Diagnostic diagnostic) = await GetDiagnosticAsync(member);
        CodeAction action = Assert.Single(await RegisterAsync(document, diagnostic));
        Assert.Equal("Pass the cancellation token to TryAsync", action.Title);
        var fixedSource = await ApplyAsync(document, action);

        Compilation compilation = (await document.Project.Solution
            .WithDocumentText(document.Id, Microsoft.CodeAnalysis.Text.SourceText.From(fixedSource))
            .GetProject(document.Project.Id)!
            .GetCompilationAsync(TestContext.Current.CancellationToken))!;
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.Empty(await compilation.WithAnalyzers([new PreferCancellableTryAsyncAnalyzer()]).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken));
        return fixedSource;
    }

    private static async Task<(Document Document, Diagnostic Diagnostic)> GetDiagnosticAsync(string member)
    {
        AdhocWorkspace workspace = new();
        Project project = AnalyzerTestHarness.CreateProject(
            workspace,
            PreferCancellableTryAsyncAnalyzerTests.ResultDefinitions,
            PreferCancellableTryAsyncAnalyzerTests.Wrap(member));
        Document document = project.Documents.Single(d => d.Name == "TestClass.cs");

        Compilation compilation = (await project.GetCompilationAsync(TestContext.Current.CancellationToken))!;
        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers([new PreferCancellableTryAsyncAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        return (document, diagnostics.First());
    }

    private static async Task<List<CodeAction>> RegisterAsync(Document document, Diagnostic diagnostic)
    {
        List<CodeAction> actions = [];
        CodeFixContext context = new(document, diagnostic, (action, _) => actions.Add(action), TestContext.Current.CancellationToken);
        await new PreferCancellableTryAsyncCodeFixProvider().RegisterCodeFixesAsync(context);
        return actions;
    }

    private static async Task<string> ApplyAsync(Document document, CodeAction action)
    {
        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(TestContext.Current.CancellationToken);
        ApplyChangesOperation apply = Assert.IsType<ApplyChangesOperation>(Assert.Single(operations));
        Document changed = apply.ChangedSolution.GetDocument(document.Id)!;
        return (await changed.GetTextAsync(TestContext.Current.CancellationToken)).ToString();
    }

    private sealed class DocumentDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
        {
            Compilation compilation = (await document.Project.GetCompilationAsync(cancellationToken))!;
            ImmutableArray<Diagnostic> diagnostics = await compilation
                .WithAnalyzers([new PreferCancellableTryAsyncAnalyzer()])
                .GetAnalyzerDiagnosticsAsync(cancellationToken);
            SyntaxTree tree = (await document.GetSyntaxTreeAsync(cancellationToken))!;
            return diagnostics.Where(d => d.Location.SourceTree == tree);
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult(Enumerable.Empty<Diagnostic>());

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult(Enumerable.Empty<Diagnostic>());
    }
}
