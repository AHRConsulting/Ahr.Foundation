using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class DiscardedResultAnalyzerTests
{
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
    public async Task Analyze_DroppedResult_ReportsWarningOnExpression()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("void M() { Save(); }");

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("AHRF010", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("'Result'", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Equal("Save()", (await diagnostic.Location.SourceTree!.GetTextAsync(TestContext.Current.CancellationToken)).ToString(diagnostic.Location.SourceSpan));
    }

    [Theory]
    [InlineData("void M() { Load(); }")]
    [InlineData("void M() { LoadCustom(); }")]
    [InlineData("async Task M() { await SaveAsync(); }")]
    [InlineData("async Task M() { await LoadAsync(); }")]
    [InlineData("async Task M() { await SaveAsync().ConfigureAwait(false); }")]
    [InlineData("async Task M() { await SaveValueAsync(); }")]
    [InlineData("void M() { SaveAsync(); }")]
    [InlineData("async Task M() { SaveAsync(); }")]
    [InlineData("void M() { SaveValueAsync(); }")]
    [InlineData("void M() { service?.Save(); }")]
    [InlineData("void M() { service?.SaveAsync(); }")]
    [InlineData("void M() { Load().Tap(_ => Number()); }")]
    [InlineData("void M() { Load().Map(x => x + 1); }")]
    [InlineData("void M() { Save().Match(() => Save(), _ => Save()); }")]
    [InlineData("void M() { Load().TapError(Log).Map(x => x + 1); }")]
    [InlineData("void M() { SaveAsync().TapErrorAsync(Log); }")]
    [InlineData("void M() { Save().MatchAsync(() => Task.CompletedTask, _ => Task.CompletedTask); }")]
    [InlineData("void M() { Action a = () => Save(); }")]
    [InlineData("void M() { Func<Task> a = async () => await SaveAsync(); }")]
    [InlineData("void M() { void Local() { Save(); } Local(); }")]
    [InlineData("Result<int> M() => Load().Tap(_ => { Action a = () => Save(); return Save(); });")]
    public async Task Analyze_DroppedResultForms_ReportsAHRF010(string member)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(member);
        Assert.Equal("AHRF010", Assert.Single(diagnostics).Id);
    }

    [Theory]
    [InlineData("void M() { _ = Save(); }")]
    [InlineData("async Task M() { _ = await SaveAsync(); }")]
    [InlineData("void M() { Result r; r = Save(); _ = r; }")]
    [InlineData("Result M() => Save();")]
    [InlineData("Task<Result> M() => SaveAsync();")]
    [InlineData("void M() { Func<Result> f = () => Save(); }")]
    [InlineData("void M() { Save().TapError(Log); }")]
    [InlineData("void M() { LoadCustom().TapError(_ => { }); }")]
    [InlineData("void M() { service?.Save().TapError(Log); }")]
    [InlineData("async Task M() { await SaveAsync().TapErrorAsync(Log); }")]
    [InlineData("async Task M() { await SaveAsync().TapErrorAsync(Log).ConfigureAwait(false); }")]
    [InlineData("void M() { Save().Match(() => { }, Log); }")] // returns the result, but the failure was handled
    [InlineData("void M() { Save().Match(() => Number(), Log); }")]
    [InlineData("async Task M() { await Save().MatchAsync(() => Task.CompletedTask, _ => Task.CompletedTask); }")]
    [InlineData("void M() { Save().Match(() => 1, _ => 0); }")]
    [InlineData("void M() { Find(); }")]
    [InlineData("void M() { Find().Tap(_ => { }); }")]
    [InlineData("void M() { Unrelated(); }")]
    [InlineData("void M() { Number(); }")]
    [InlineData("Result<int> M() => Load().Tap(_ => { Save(); });")] // AHRF008 reports this one
    [InlineData("Task<Result> M() => Save().TapAsync(async () => await SaveAsync());")] // AHRF008 reports this one
    [InlineData("Result<int> M() => Load().TapSome(_ => { Save(); });")] // AHRF008 reports this one
    public async Task Analyze_HandledOrOutOfScope_ReportsNothing(string member)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(member);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_DropInsideTapFunctionThatReturnsResult_ReportsAHRF010()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("Result<int> M() => Load().Tap(_ => { Save(); return Save(); });");
        Assert.Equal("AHRF010", Assert.Single(diagnostics).Id);
    }

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
