using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class FlattenedResultOfOptionAnalyzerTests
{
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
    [InlineData("Result<Option<int>> r", "r.GetValueOrDefault()")]
    [InlineData("Result<Option<int>> r", "r.GetValueOrDefault(Option<int>.None)")]
    [InlineData("Result<Option<int>> r", "r.ToOption()")]
    [InlineData("Result<Option<int>, string> r", "r.GetValueOrDefault()")]
    [InlineData("Result<Option<int>, string> r", "r.GetValueOrDefault(Option<int>.None)")]
    [InlineData("Result<Option<int>, string> r", "r.ToOption()")]
    public async Task Analyze_ResultOfOption_ReportsInfoOnCall(string parameter, string call)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(parameter, call);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("AHRF009", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Equal(call, (await diagnostic.Location.SourceTree!.GetTextAsync(TestContext.Current.CancellationToken)).ToString(diagnostic.Location.SourceSpan));
    }

    [Theory]
    [InlineData("Result<int> r", "r.GetValueOrDefault()")]
    [InlineData("Result<int, string> r", "r.ToOption()")]
    [InlineData("Option<Option<int>> o", "o.GetValueOrDefault()")]
    [InlineData("Other.Result<Option<int>> r", "r.GetValueOrDefault()")]
    public async Task Analyze_OtherReceiver_ReportsNothing(string parameter, string call) =>
        Assert.Empty(await AnalyzeAsync(parameter, call));

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string parameter, string call) =>
        AnalyzerTestHarness.AnalyzeAsync(
            new FlattenedResultOfOptionAnalyzer(),
            ResultDefinitions,
            $$"""
            using Ahr.Foundation;
            class C
            {
                void M({{parameter}}) { _ = {{call}}; }
            }
            """);
}
