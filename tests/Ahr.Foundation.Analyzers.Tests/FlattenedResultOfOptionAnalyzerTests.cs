using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class FlattenedResultOfOptionAnalyzerTests
{
    private static readonly BusinessContext _nestedResults = BusinessContext
        .Define("nested-result-options", "Nested result and option handling", "Preserve failure information when a result contains an optional value.")
        .Capability("preserve-result-failures", "Retain a result's failure while handling an optional value")
        .Outcome("nested-failure-preserved", "Extracting a nested option does not discard the result failure")
        .Rule("avoid-lossy-result-extraction", "Do not unwrap a Result<Option<T>> through APIs that discard its failure")
        .Build();

    private const string ResultDefinitions = """
        namespace Ahr.Foundation
        {
            public readonly struct Result<T>
            {
                public T? GetValueOrDefault() => default;
                public T GetValueOrDefault(T fallback) => fallback;
                public Option<T> ToOption() => default;
            }
            public readonly struct Result<T, TError>
            {
                public T? GetValueOrDefault() => default;
                public T GetValueOrDefault(T fallback) => fallback;
                public Option<T> ToOption() => default;
            }
            public readonly struct Option<T>
            {
                public static Option<T> None => default;
                public T? GetValueOrDefault() => default;
            }
        }
        namespace Other
        {
            public readonly struct Result<T>
            {
                public T? GetValueOrDefault() => default;
            }
        }
        """;

    [Theory]
    [InlineData("Built-in Result with parameterless extraction", "Result<Option<int>> r", "r.GetValueOrDefault()")]
    [InlineData("Built-in Result with an explicit None fallback", "Result<Option<int>> r", "r.GetValueOrDefault(Option<int>.None)")]
    [InlineData("Built-in Result converted to Option", "Result<Option<int>> r", "r.ToOption()")]
    [InlineData("Custom-error Result with parameterless extraction", "Result<Option<int>, string> r", "r.GetValueOrDefault()")]
    [InlineData("Custom-error Result with an explicit None fallback", "Result<Option<int>, string> r", "r.GetValueOrDefault(Option<int>.None)")]
    [InlineData("Custom-error Result converted to Option", "Result<Option<int>, string> r", "r.ToOption()")]
    public Task Analyze_ResultOfOption_ReportsInfoOnCall(
        string example,
        string parameter,
        string call) =>
        Scenario.Define("Unwrapping a nested option from a result can discard its failure", _nestedResults)
            .When($"The analyzer checks {example}", _ => AnalyzeAsync(CreateSource(parameter, call)))
            .Then("The analyzer reports AHRF009 as an informational diagnostic", (ImmutableArray<Diagnostic> diagnostics) =>
            {
                Diagnostic diagnostic = AnalyzerTestHarness.ReportsSingle(diagnostics, FlattenedResultOfOptionAnalyzer.DiagnosticId, DiagnosticSeverity.Info);
                Assert.Equal(
                    call,
                    diagnostic.Location.SourceTree!
                        .GetText(TestContext.Current.CancellationToken)
                        .ToString(diagnostic.Location.SourceSpan));
            })
            .VerifyAsync();

    [Theory]
    [InlineData("A Result with a non-optional value", "Result<int> r", "r.GetValueOrDefault()")]
    [InlineData("A custom-error Result converted to Option", "Result<int, string> r", "r.ToOption()")]
    [InlineData("An Option containing another Option", "Option<Option<int>> o", "o.GetValueOrDefault()")]
    [InlineData("An unrelated Result type", "Other.Result<Option<int>> r", "r.GetValueOrDefault()")]
    public Task Analyze_OtherReceiver_ReportsNothing(
        string example,
        string parameter,
        string call) =>
        Scenario.Define("Extracting a value outside Result<Option<T>> is not reported", _nestedResults)
            .When($"The analyzer checks {example}", _ => AnalyzeAsync(CreateSource(parameter, call)))
            .Then("No AHRF009 diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source) =>
        AnalyzerTestHarness.AnalyzeAsync(
            new FlattenedResultOfOptionAnalyzer(),
            ResultDefinitions,
            source);

    private static string CreateSource(string parameter, string call) =>
        $$"""
            using Ahr.Foundation;
            class C
            {
                void M({{parameter}}) { _ = {{call}}; }
            }
            """;
}
