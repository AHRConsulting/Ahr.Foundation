using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class DiscardedTapResultAnalyzerTests
{
    private const string ResultDefinitions = """
        using System;
        using System.Threading.Tasks;

        namespace Ahr.Foundation
        {
            public readonly struct Result
            {
                public Result Tap(Action action) => this;
                public Result Tap(Func<Result> action) => this;
                public Result Tap<TOut>(Func<Result<TOut>> action) => this;
                public Task<Result> TapAsync(Func<Task> action) => default!;
                public Task<Result> TapAsync(Func<Task<Result>> action) => default!;
            }
            public readonly struct Result<T>
            {
                public Result<T> Tap(Action<T> action) => this;
                public Result<T> Tap(Func<T, Result> action) => this;
                public Result<T> Tap<TOut>(Func<T, Result<TOut>> action) => this;
                public Task<Result<T>> TapAsync(Func<T, Task> action) => default!;
                public Task<Result<T>> TapAsync(Func<T, Task<Result>> action) => default!;
                public Result<T> TapError(Action<int> action) => this;
            }
            public readonly struct Result<T, TError>
            {
                public Result<T, TError> Tap(Action<T> action) => this;
                public Result<T, TError> Tap<TOut>(Func<T, Result<TOut, TError>> action) => this;
            }
            public readonly struct Option<T>
            {
                public static Option<T> None => default;
                public Option<T> Tap(Action<T> action) => this;
                public Option<T> Tap<TOut>(Func<T, Option<TOut>> action) => this;
                public Task<Option<T>> TapAsync(Func<T, Task> action) => default!;
            }
            public static class ResultExtensions
            {
                public static Result<Option<T>> TapSome<T>(this Result<Option<T>> result, Action<T> action) => result;
                public static Result<Option<T>> TapSome<T>(this Result<Option<T>> result, Func<T, Result> action) => result;
            }
            public static class TaskCompositionExtensions
            {
                public static Task<Option<T>> TapAsync<T>(this Task<Option<T>> optionTask, Action<T> action) => optionTask;
                public static Task<Option<T>> TapAsync<T>(this Task<Option<T>> optionTask, Func<T, Task> action) => optionTask;
            }
        }
        """;

    private const string Helpers = """
        static Result Save(int x) => default;
        static Task<Result> SaveAsync(int x) => Task.FromResult(default(Result));
        static Option<int> Find(int x) => default;
        static void Log(int x) { }
        static Task LogAsync(int x) => Task.CompletedTask;
        """;

    [Fact]
    public async Task Analyze_ResultDiscardedByOption_ReportsWarningOnExpression()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("Option<int>.None.Tap(x => Save(x));");

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("AHRF008", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal("Save(x)", (await diagnostic.Location.SourceTree!.GetTextAsync(TestContext.Current.CancellationToken)).ToString(diagnostic.Location.SourceSpan));
    }

    [Fact]
    public async Task Analyze_OptionDiscardedByResult_ReportsAHRF008()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("default(Result<int>).Tap(x => Find(x));");
        Assert.Equal("AHRF008", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_DifferentErrorTypeDiscarded_ReportsAHRF008()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("default(Result<int, string>).Tap(x => Save(x));");
        Assert.Equal("AHRF008", Assert.Single(diagnostics).Id);
    }

    [Theory]
    [InlineData("default(Result<Option<int>>).TapSome(x => { Save(x); });")]
    [InlineData("default(Result<Option<int>>).TapSome(x => Find(x));")]
    public async Task Analyze_TapSomeDiscardingResultOrOption_ReportsAHRF008(string statement)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(statement);
        Assert.Equal("AHRF008", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_TapSomeReturningResult_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("default(Result<Option<int>>).TapSome(x => Save(x));");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_BlockBodyWithoutReturn_ReportsAHRF008()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("default(Result<int>).Tap(x => { Save(x); });");
        Assert.Equal("AHRF008", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_BlockBodyInsideCondition_ReportsAHRF008()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("default(Result<int>).Tap(x => { if (x > 0) { Save(x); } });");
        Assert.Equal("AHRF008", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_AsyncBlockAwaitingResult_ReportsAHRF008()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("default(Result<int>).TapAsync(async x => { await SaveAsync(x); });");
        Assert.Equal("AHRF008", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_ExpressionReturningTaskOfOtherFamily_ReportsAHRF008()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("Option<int>.None.TapAsync(x => SaveAsync(x));");
        Assert.Equal("AHRF008", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_MethodGroupReturningTaskOfOtherFamily_ReportsAHRF008()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("Option<int>.None.TapAsync(SaveAsync);");
        Assert.Equal("AHRF008", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_TaskExtension_ReportsAHRF008()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("Task.FromResult(Option<int>.None).TapAsync(x => Save(x));");
        Assert.Equal("AHRF008", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_PayloadFreeResultBlockBody_ReportsAHRF008()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("default(Result).Tap(() => { Save(1); });");
        Assert.Equal("AHRF008", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_SameFamilyStep_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
            default(Result<int>).Tap(x => Save(x));
            default(Result<int>).Tap(Save);
            default(Result<int>).TapAsync(x => SaveAsync(x));
            default(Result<int>).TapAsync(async x => await SaveAsync(x));
            Option<int>.None.Tap(x => Find(x));
            """);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_DeliberateDiscard_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
            default(Result<int>).Tap(x => { _ = Save(x); });
            Option<int>.None.Tap(x => _ = Save(x));
            default(Result<int>).TapAsync(async x => { _ = await SaveAsync(x); });
            """);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_PlainSideEffects_ReportNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
            var list = new List<int>();
            default(Result<int>).Tap(x => Log(x));
            default(Result<int>).Tap(Log);
            default(Result<int>).Tap(x => list.Add(x));
            default(Result<int>).Tap(x => list.Remove(x));
            default(Result<int>).TapAsync(x => LogAsync(x));
            default(Result<int>).TapAsync(async x => await LogAsync(x));
            """);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_NestedLambda_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("default(Result<int>).Tap(x => { Func<Result> later = () => { return Save(x); }; });");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_OtherMethodsAndTypes_ReportNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
            default(Result<int>).TapError(e => { Save(e); });
            new Other().Tap(x => { Save(x); });
            """);
        Assert.Empty(diagnostics);
    }

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string statements) =>
        AnalyzerTestHarness.AnalyzeAsync(
            new DiscardedTapResultAnalyzer(),
            ResultDefinitions,
            $$"""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Ahr.Foundation;
            class Other { public Other Tap(Action<int> action) => this; }
            class C
            {
                {{Helpers}}
                void M()
                {
                    {{statements}}
                }
            }
            """);
}
