using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class DefaultResultConstructionCodeFixProviderTests
{
    private static readonly BusinessContext _resultInitialization = BusinessContext
        .Define("explicit-result-initialization-code-fix", "Explicit Result initialization fixes", "Offer a safe success factory when a Result is default-constructed.")
        .Capability("replace-invalid-default-result", "Replace a default Result with its explicit success factory")
        .Outcome("default-result-fixed-with-success", "The code fix creates a valid successful Result")
        .Rule("use-explicit-result-factories", "Create Results with their explicit success or failure factories")
        .Build();

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
    public Task CodeFix_ResultDefault_ReplacesWithResultSuccess()
    {
        const string initialSource = """
            namespace Ahr.Foundation;
            class TestClass
            {
                Result GetResult() => default(Result);
            }
            """;

        return Scenario.Define("The default Result code fix uses an explicit success factory", _resultInitialization)
            .When("The analyzer and code fix process a default Result", _ =>
                ApplyFixAsync(initialSource))
            .Then("AHRF001 is reported for the default Result", (CodeFixOutcome outcome) =>
                AnalyzerTestHarness.ReportsSingle(outcome.Diagnostics, DefaultResultConstructionAnalyzer.DiagnosticId))
            .And("A single fix titled Use 'Result.Success()' is offered", (CodeFixOutcome outcome) =>
                Assert.Equal(["Use 'Result.Success()'"], outcome.ActionTitles))
            .And("The fix is a single change to the solution", (CodeFixOutcome outcome) =>
                Assert.Equal([nameof(ApplyChangesOperation)], outcome.Operations))
            .And("The rewritten code uses Result.Success()", (CodeFixOutcome outcome) =>
                Assert.Contains("Result.Success()", outcome.FixedSource, StringComparison.Ordinal))
            .And("No AHRF001 diagnostic remains after the fix", (CodeFixOutcome outcome) =>
                Assert.Empty(outcome.DiagnosticsAfterFix))
            .VerifyAsync();
    }

    private static Task<CodeFixOutcome> ApplyFixAsync(string initialSource) =>
        AnalyzerTestHarness.RunCodeFixAsync(
            AnalyzerTestHarness.CreateProject(new AdhocWorkspace(), ResultDefinitions, initialSource),
            new DefaultResultConstructionAnalyzer(),
            new DefaultResultConstructionCodeFixProvider(),
            TestContext.Current.CancellationToken);
}
