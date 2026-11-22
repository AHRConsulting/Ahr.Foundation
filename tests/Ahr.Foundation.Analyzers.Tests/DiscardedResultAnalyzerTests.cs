using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class DiscardedResultAnalyzerTests
{
    private static readonly BusinessContext _discardedResults = BusinessContext
        .Define("result-failure-observation", "Result failure observation", "Keep failures observable by handling every Result returned from an operation.")
        .Capability("handle-result-outcomes", "Observe the outcome of every Result operation")
        .Outcome("result-not-discarded", "A Result failure is never silently lost")
        .Rule("handle-result-before-discard", "A Result-returning operation must be returned, assigned, or handled")
        .Build();

    private const string ResultDefinitions = """
        using System;
        using System.Threading.Tasks;

        namespace Ahr.Foundation
        {
            public readonly struct Error { }
            public readonly struct Result
            {
                public Result Tap(Action action) => this;
                public Result Tap(Func<Result> action) => this;
                public Task<Result> TapAsync(Func<Task> action) => default!;
                public Result TapError(Action<Error> action) => this;
                public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure) => default!;
                public Result Match(Action onSuccess, Action<Error> onFailure) => this;
                public Task<Result> MatchAsync(Func<Task> onSuccess, Func<Error, Task> onFailure) => Task.FromResult(this);
            }
            public readonly struct Result<T>
            {
                public Result<T> Tap(Action<T> action) => this;
                public Result<T> Tap(Func<T, Result> action) => this;
                public Result<T> TapError(Action<Error> action) => this;
                public Result<TOut> Map<TOut>(Func<T, TOut> map) => default;
                public Result<T> TapSome(Action<T> action) => this;
            }
            public readonly struct Result<T, TError>
            {
                public Result<T, TError> TapError(Action<TError> action) => this;
            }
            public readonly struct Option<T>
            {
                public Option<T> Tap(Action<T> action) => this;
            }
            public static class TaskCompositionExtensions
            {
                public static Task<Result> TapErrorAsync(this Task<Result> resultTask, Action<Error> action) => resultTask;
            }
        }
        namespace Other
        {
            public readonly struct Result { }
        }
        """;

    private const string Helpers = """
        static Result Save() => default;
        static Result<int> Load() => default;
        static Result<int, string> LoadCustom() => default;
        static Task<Result> SaveAsync() => Task.FromResult(default(Result));
        static Task<Result<int>> LoadAsync() => Task.FromResult(default(Result<int>));
        static ValueTask<Result> SaveValueAsync() => default;
        static Option<int> Find() => default;
        static Other.Result Unrelated() => default;
        static void Log(Error error) { }
        static int Number() => 0;
        Service? service = null;
        class Service { public Result Save() => default; public Task<Result> SaveAsync() => Task.FromResult(default(Result)); }
        """;

    [Fact]
    public Task Analyze_DroppedResult_ReportsWarningOnExpression() =>
        Scenario.Define("Discarding a Result reports AHRF010", _discardedResults)
            .When("The analyzer examines a discarded Result expression", _ =>
                AnalyzeAsync("void M() { Save(); }"))
            .Then("AHRF010 identifies the discarded Result expression", (ImmutableArray<Diagnostic> diagnostics) =>
            {
                Diagnostic diagnostic = AnalyzerTestHarness.ReportsSingle(diagnostics, DiscardedResultAnalyzer.DiagnosticId, DiagnosticSeverity.Warning);
                Assert.Contains("'Result'", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
                Assert.Equal("Save()", diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan));
            })
            .VerifyAsync();

    [Theory]
    [InlineData("A generic Result returned by a method", "void M() { Load(); }")]
    [InlineData("A custom-error Result returned by a method", "void M() { LoadCustom(); }")]
    [InlineData("An awaited payload-free Result", "async Task M() { await SaveAsync(); }")]
    [InlineData("An awaited generic Result", "async Task M() { await LoadAsync(); }")]
    [InlineData("An awaited Result with ConfigureAwait", "async Task M() { await SaveAsync().ConfigureAwait(false); }")]
    [InlineData("An awaited ValueTask Result", "async Task M() { await SaveValueAsync(); }")]
    [InlineData("A task returned but not awaited", "void M() { SaveAsync(); }")]
    [InlineData("A task returned in an async method but not awaited", "async Task M() { SaveAsync(); }")]
    [InlineData("A ValueTask returned but not awaited", "void M() { SaveValueAsync(); }")]
    [InlineData("A conditional service call returning Result", "void M() { service?.Save(); }")]
    [InlineData("A conditional service call returning Task<Result>", "void M() { service?.SaveAsync(); }")]
    [InlineData("A Tap callback returning a discarded Result", "void M() { Load().Tap(_ => Number()); }")]
    [InlineData("A Map callback returning a discarded Result", "void M() { Load().Map(x => x + 1); }")]
    [InlineData("A Match callback returning a discarded Result", "void M() { Save().Match(() => Save(), _ => Save()); }")]
    [InlineData("A TapError followed by a discarded Map", "void M() { Load().TapError(Log).Map(x => x + 1); }")]
    [InlineData("A TapErrorAsync call returning a discarded task", "void M() { SaveAsync().TapErrorAsync(Log); }")]
    [InlineData("A MatchAsync call returning a discarded task", "void M() { Save().MatchAsync(() => Task.CompletedTask, _ => Task.CompletedTask); }")]
    [InlineData("A result captured by an uninvoked action", "void M() { Action a = () => Save(); }")]
    [InlineData("A result captured by an uninvoked async function", "void M() { Func<Task> a = async () => await SaveAsync(); }")]
    [InlineData("A result discarded in a local function", "void M() { void Local() { Save(); } Local(); }")]
    [InlineData("A result discarded inside a Tap callback", "Result<int> M() => Load().Tap(_ => { Action a = () => Save(); return Save(); });")]
    public Task Analyze_DroppedResultForms_ReportsAHRF010(string example, string member) =>
        Scenario.Define("Discarded Result usage reports AHRF010", _discardedResults)
            .When($"The analyzer checks {example}", _ => AnalyzeAsync(member))
            .Then("AHRF010 identifies the unhandled Result", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, DiscardedResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Theory]
    [InlineData("An explicitly discarded Result", "void M() { _ = Save(); }")]
    [InlineData("An explicitly discarded awaited Result", "async Task M() { _ = await SaveAsync(); }")]
    [InlineData("A Result assigned and then discarded", "void M() { Result r; r = Save(); _ = r; }")]
    [InlineData("A Result returned from the method", "Result M() => Save();")]
    [InlineData("A task Result returned from the method", "Task<Result> M() => SaveAsync();")]
    [InlineData("A Result returned by a function value", "void M() { Func<Result> f = () => Save(); }")]
    [InlineData("A Result whose error is handled", "void M() { Save().TapError(Log); }")]
    [InlineData("A custom-error Result whose error is handled", "void M() { LoadCustom().TapError(_ => { }); }")]
    [InlineData("A conditional Result whose error is handled", "void M() { service?.Save().TapError(Log); }")]
    [InlineData("An awaited task Result whose error is handled", "async Task M() { await SaveAsync().TapErrorAsync(Log); }")]
    [InlineData("An awaited configured task Result whose error is handled", "async Task M() { await SaveAsync().TapErrorAsync(Log).ConfigureAwait(false); }")]
    [InlineData("A Match that handles the failure", "void M() { Save().Match(() => { }, Log); }")]
    [InlineData("A value-returning Match with handled errors", "void M() { Save().Match(() => Number(), Log); }")]
    [InlineData("An awaited MatchAsync that handles both outcomes", "async Task M() { await Save().MatchAsync(() => Task.CompletedTask, _ => Task.CompletedTask); }")]
    [InlineData("A value-returning Match", "void M() { Save().Match(() => 1, _ => 0); }")]
    [InlineData("An Option returned from a method", "void M() { Find(); }")]
    [InlineData("An Option with a handled value", "void M() { Find().Tap(_ => { }); }")]
    [InlineData("An unrelated Result type", "void M() { Unrelated(); }")]
    [InlineData("An unrelated ordinary value", "void M() { Number(); }")]
    [InlineData("AHRF008 handling an ignored Result inside Tap", "Result<int> M() => Load().Tap(_ => { Save(); });")]
    [InlineData("AHRF008 handling an ignored task inside TapAsync", "Task<Result> M() => Save().TapAsync(async () => await SaveAsync());")]
    [InlineData("AHRF008 handling an ignored Result inside TapSome", "Result<int> M() => Load().TapSome(_ => { Save(); });")]
    public Task Analyze_HandledOrOutOfScope_ReportsNothing(string example, string member) =>
        Scenario.Define("Handled or out-of-scope result usage is not reported", _discardedResults)
            .When($"The analyzer checks {example}", _ => AnalyzeAsync(member))
            .Then("No AHRF010 diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_DropInsideTapFunctionThatReturnsResult_ReportsAHRF010() =>
        Scenario.Define("Discarding a Result inside a Tap callback reports AHRF010", _discardedResults)
            .When("The analyzer checks a Tap callback that drops an intermediate Result", _ =>
                AnalyzeAsync("Result<int> M() => Load().Tap(_ => { Save(); return Save(); });"))
            .Then("AHRF010 identifies the discarded intermediate Result", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, DiscardedResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string member) =>
        AnalyzerTestHarness.AnalyzeAsync(
            new DiscardedResultAnalyzer(),
            ResultDefinitions,
            $$"""
            #nullable enable
            using System;
            using System.Threading.Tasks;
            using Ahr.Foundation;
            class C
            {
                {{Helpers}}
                {{member}}
            }
            """);
}
