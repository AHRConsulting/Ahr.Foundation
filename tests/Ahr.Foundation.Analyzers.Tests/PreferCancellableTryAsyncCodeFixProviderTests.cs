using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class PreferCancellableTryAsyncCodeFixProviderTests
{
    private static readonly BusinessContext _cancellableTryFix = BusinessContext
        .Define("cancellable-try-async-fixes", "Cancellable TryAsync fixes", "Forward a caller's cancellation token to the cancellable TryAsync overload without changing operation behavior.")
        .Capability("forward-caller-token", "Forward the caller token to TryAsync")
        .Outcome("operation-references-rewritten", "Captured caller-token references use the new operation parameter")
        .Outcome("ambiguous-token-fix-withheld", "A code fix is withheld when one token cannot safely represent the operation")
        .Rule("preserve-call-semantics", "Preserve error factories and choose a non-conflicting operation parameter")
        .Build();

    private const string FixTitle = "Pass the cancellation token to TryAsync";

    [Fact]
    public Task CodeFix_ExpressionLambda_PassesTokenToCancellableOverload() =>
        Scenario.Define("The expression-lambda fix forwards its captured token", _cancellableTryFix)
            .When("The code fix updates a TryAsync expression lambda", _ =>
                ApplyFixAsync("Task M(Client client, CancellationToken ct) => Result.TryAsync(() => client.GetAsync(\"u\", ct));"))
            .Then("AHRF007 is reported for the TryAsync call", (CodeFixOutcome outcome) =>
                AnalyzerTestHarness.ReportsSingle(outcome.Diagnostics, PreferCancellableTryAsyncAnalyzer.DiagnosticId))
            .And("A single 'Pass the cancellation token to TryAsync' fix is offered", (CodeFixOutcome outcome) =>
                Assert.Equal([FixTitle], outcome.ActionTitles))
            .And("The fix is a single change to the solution", (CodeFixOutcome outcome) =>
                Assert.Equal([nameof(ApplyChangesOperation)], outcome.Operations))
            .And("The operation and outer call receive the expected tokens", (CodeFixOutcome outcome) =>
                Assert.Contains("Result.TryAsync(token => client.GetAsync(\"u\", token), ct)", outcome.FixedSource, StringComparison.Ordinal))
            .And("No AHRF007 diagnostic remains after the fix", (CodeFixOutcome outcome) =>
                Assert.Empty(outcome.DiagnosticsAfterFix))
            .VerifyAsync();

    [Fact]
    public Task CodeFix_AsyncBlockLambda_ReplacesEveryReference() =>
        Scenario.Define("The async block fix replaces every captured token reference", _cancellableTryFix)
            .When("The code fix updates an async block that uses the captured token twice", _ =>
                ApplyFixAsync("""
                    Task M(Client client, CancellationToken ct) => Result.TryAsync(async () =>
                    {
                        await client.SaveAsync(ct);
                        return await client.GetAsync("u", ct);
                    });
                    """))
            .Then("AHRF007 is reported for the TryAsync call", (CodeFixOutcome outcome) =>
                AnalyzerTestHarness.ReportsSingle(outcome.Diagnostics, PreferCancellableTryAsyncAnalyzer.DiagnosticId))
            .And("A single 'Pass the cancellation token to TryAsync' fix is offered", (CodeFixOutcome outcome) =>
                Assert.Equal([FixTitle], outcome.ActionTitles))
            .And("The fix is a single change to the solution", (CodeFixOutcome outcome) =>
                Assert.Equal([nameof(ApplyChangesOperation)], outcome.Operations))
            .And("Both operation calls use the new token and the call keeps the caller token", (CodeFixOutcome outcome) =>
            {
                Assert.Contains("Result.TryAsync(async token =>", outcome.FixedSource, StringComparison.Ordinal);
                Assert.Contains("await client.SaveAsync(token);", outcome.FixedSource, StringComparison.Ordinal);
                Assert.Contains("await client.GetAsync(\"u\", token);", outcome.FixedSource, StringComparison.Ordinal);
                Assert.Contains("}, ct);", outcome.FixedSource, StringComparison.Ordinal);
            })
            .And("No AHRF007 diagnostic remains after the fix", (CodeFixOutcome outcome) =>
                Assert.Empty(outcome.DiagnosticsAfterFix))
            .VerifyAsync();

    [Fact]
    public Task CodeFix_ErrorFactoryOverload_KeepsFactoryAndAppendsToken() =>
        Scenario.Define("The error-factory fix preserves the factory and appends the caller token", _cancellableTryFix)
            .When("The code fix updates a custom-error TryAsync call", _ =>
                ApplyFixAsync("Task M(Client client, Context context) => Result.TryAsync(() => client.GetAsync(\"u\", context.RequestAborted), ex => ex.Message);"))
            .Then("AHRF007 is reported for the TryAsync call", (CodeFixOutcome outcome) =>
                AnalyzerTestHarness.ReportsSingle(outcome.Diagnostics, PreferCancellableTryAsyncAnalyzer.DiagnosticId))
            .And("A single 'Pass the cancellation token to TryAsync' fix is offered", (CodeFixOutcome outcome) =>
                Assert.Equal([FixTitle], outcome.ActionTitles))
            .And("The fix is a single change to the solution", (CodeFixOutcome outcome) =>
                Assert.Equal([nameof(ApplyChangesOperation)], outcome.Operations))
            .And("The factory remains in place and the caller token is forwarded", (CodeFixOutcome outcome) =>
                Assert.Contains("Result.TryAsync(token => client.GetAsync(\"u\", token), ex => ex.Message, context.RequestAborted)", outcome.FixedSource, StringComparison.Ordinal))
            .And("No AHRF007 diagnostic remains after the fix", (CodeFixOutcome outcome) =>
                Assert.Empty(outcome.DiagnosticsAfterFix))
            .VerifyAsync();

    [Fact]
    public Task CodeFix_NameInScope_ChoosesFreeParameterName() =>
        Scenario.Define("The code fix chooses a non-conflicting operation parameter", _cancellableTryFix)
            .When("The code fix updates a call where token is already in scope", _ =>
                ApplyFixAsync("Task M(Client client, CancellationToken token) => Result.TryAsync(() => client.GetAsync(\"u\", token));"))
            .Then("AHRF007 is reported for the TryAsync call", (CodeFixOutcome outcome) =>
                AnalyzerTestHarness.ReportsSingle(outcome.Diagnostics, PreferCancellableTryAsyncAnalyzer.DiagnosticId))
            .And("A single 'Pass the cancellation token to TryAsync' fix is offered", (CodeFixOutcome outcome) =>
                Assert.Equal([FixTitle], outcome.ActionTitles))
            .And("The fix is a single change to the solution", (CodeFixOutcome outcome) =>
                Assert.Equal([nameof(ApplyChangesOperation)], outcome.Operations))
            .And("The operation parameter uses an available name and the caller token is preserved", (CodeFixOutcome outcome) =>
                Assert.Contains("Result.TryAsync(cancellationToken => client.GetAsync(\"u\", cancellationToken), token)", outcome.FixedSource, StringComparison.Ordinal))
            .And("No AHRF007 diagnostic remains after the fix", (CodeFixOutcome outcome) =>
                Assert.Empty(outcome.DiagnosticsAfterFix))
            .VerifyAsync();

    [Fact]
    public Task CodeFix_TwoDistinctTokens_OffersNoFix() =>
        Scenario.Define("The code fix is withheld when the operation uses two distinct tokens", _cancellableTryFix)
            .When("The provider is queried for a call that captures two cancellation tokens", _ =>
                ApplyFixAsync("Task M(Client client, CancellationToken a, CancellationToken b) => Result.TryAsync(async () => { await client.SaveAsync(a); return await client.GetAsync(\"u\", b); });"))
            .Then("AHRF007 is reported for the TryAsync call", (CodeFixOutcome outcome) =>
                AnalyzerTestHarness.ReportsSingle(outcome.Diagnostics, PreferCancellableTryAsyncAnalyzer.DiagnosticId))
            .And("No unsafe code fix is offered", (CodeFixOutcome outcome) =>
                Assert.Empty(outcome.ActionTitles))
            .VerifyAsync();

    [Fact]
    public Task CodeFix_FixAll_FixesEveryCallInDocument() =>
        Scenario.Define("Fix All forwards cancellation tokens for every call in the document", _cancellableTryFix)
            .When("The fix-all provider updates two calls that capture the same token", _ =>
                FixAllAsync("""
                    void M(Client client, CancellationToken ct)
                    {
                        _ = Result.TryAsync(() => client.GetAsync("a", ct));
                        _ = Result.TryAsync(() => client.SaveAsync(ct));
                    }
                    """))
            .Then("AHRF007 is reported for both calls before the fix", (FixAllOutcome outcome) =>
                Assert.Equal(
                    [PreferCancellableTryAsyncAnalyzer.DiagnosticId, PreferCancellableTryAsyncAnalyzer.DiagnosticId],
                    outcome.Diagnostics.Select(diagnostic => diagnostic.Id)))
            .And("The fix-all result is a single change to the solution", (FixAllOutcome outcome) =>
                Assert.Equal([nameof(ApplyChangesOperation)], outcome.Operations))
            .And("Both calls use the operation token and retain the caller token", (FixAllOutcome outcome) =>
            {
                Assert.Contains("Result.TryAsync(token => client.GetAsync(\"a\", token), ct)", outcome.FixedSource, StringComparison.Ordinal);
                Assert.Contains("Result.TryAsync(token => client.SaveAsync(token), ct)", outcome.FixedSource, StringComparison.Ordinal);
            })
            .VerifyAsync();

    private sealed record FixAllOutcome(
        ImmutableArray<Diagnostic> Diagnostics,
        IReadOnlyList<string> Operations,
        string? FixedSource);

    private static Task<CodeFixOutcome> ApplyFixAsync(string member) =>
        AnalyzerTestHarness.RunCodeFixAsync(
            CreateProject(member),
            new PreferCancellableTryAsyncAnalyzer(),
            new PreferCancellableTryAsyncCodeFixProvider(),
            TestContext.Current.CancellationToken);

    private static async Task<FixAllOutcome> FixAllAsync(string member)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Project project = CreateProject(member);
        Document document = AnalyzerTestHarness.GetTestDocument(project);
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.AnalyzeProjectAsync(
            project, new PreferCancellableTryAsyncAnalyzer(), cancellationToken);
        PreferCancellableTryAsyncCodeFixProvider provider = new();
        FixAllContext fixAllContext = new(
            document,
            provider,
            FixAllScope.Document,
            FixTitle,
            [PreferCancellableTryAsyncAnalyzer.DiagnosticId],
            new DocumentDiagnosticProvider(),
            cancellationToken);
        CodeAction? action = await provider.GetFixAllProvider().GetFixAsync(fixAllContext);
        if (action is null)
        {
            return new(diagnostics, [], null);
        }

        ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(cancellationToken);
        return new(
            diagnostics,
            AnalyzerTestHarness.GetOperationNames(operations),
            await AnalyzerTestHarness.ApplyAsync(document, operations, cancellationToken));
    }

    private static Project CreateProject(string member) =>
        AnalyzerTestHarness.CreateProject(
            new AdhocWorkspace(),
            PreferCancellableTryAsyncAnalyzerTests.ResultDefinitions,
            PreferCancellableTryAsyncAnalyzerTests.Wrap(member));

    private sealed class DocumentDiagnosticProvider : FixAllContext.DiagnosticProvider
    {
        public override async Task<IEnumerable<Diagnostic>> GetDocumentDiagnosticsAsync(Document document, CancellationToken cancellationToken)
        {
            ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.AnalyzeProjectAsync(
                document.Project, new PreferCancellableTryAsyncAnalyzer(), cancellationToken);
            SyntaxTree tree = (await document.GetSyntaxTreeAsync(cancellationToken))!;
            return diagnostics.Where(diagnostic => diagnostic.Location.SourceTree == tree);
        }

        public override Task<IEnumerable<Diagnostic>> GetProjectDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult(Enumerable.Empty<Diagnostic>());

        public override Task<IEnumerable<Diagnostic>> GetAllDiagnosticsAsync(Project project, CancellationToken cancellationToken) =>
            Task.FromResult(Enumerable.Empty<Diagnostic>());
    }
}
