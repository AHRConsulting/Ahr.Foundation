using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class DiscardedTapResultAnalyzerTests
{
    private static readonly BusinessContext _discardedStepResult = BusinessContext
        .Define("tap-result-preservation", "Result preservation in Tap callbacks", "Do not silently discard a Result or Option returned from a Tap callback.")
        .Capability("preserve-step-results", "Preserve results produced inside Tap callbacks")
        .Outcome("mismatched-result-reported", "A result from a different family is reported when discarded")
        .Outcome("same-family-result-accepted", "A callback returning the same result family remains valid")
        .Rule("return-or-discard-callback-result", "Return the callback result or explicitly discard it")
        .Build();

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
    public Task Analyze_ResultDiscardedByOption_ReportsWarningOnExpression() =>
        Scenario.Define("A Result discarded by an Option Tap callback reports AHRF008", _discardedStepResult)
            .When("The analyzer inspects an Option callback returning a Result", _ =>
                AnalyzeAsync("Option<int>.None.Tap(x => Save(x));"))
            .Then("AHRF008 points to the discarded Result expression", (ImmutableArray<Diagnostic> diagnostics) =>
            {
                Diagnostic diagnostic = AnalyzerTestHarness.ReportsSingle(diagnostics, DiscardedTapResultAnalyzer.DiagnosticId, DiagnosticSeverity.Warning);
                Assert.Equal("Save(x)", diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan));
            })
            .VerifyAsync();

    [Fact]
    public Task Analyze_OptionDiscardedByResult_ReportsAHRF008() =>
        Scenario.Define("An Option discarded by a Result Tap callback reports AHRF008", _discardedStepResult)
            .When("The analyzer inspects a Result callback returning an Option", _ =>
                AnalyzeAsync("default(Result<int>).Tap(x => Find(x));"))
            .Then("AHRF008 identifies the discarded Option", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DiscardedTapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_DifferentErrorTypeDiscarded_ReportsAHRF008() =>
        Scenario.Define("A Result with a different error type discarded by Tap reports AHRF008", _discardedStepResult)
            .When("The analyzer inspects a Tap callback returning a Result with another error family", _ =>
                AnalyzeAsync("default(Result<int, string>).Tap(x => Save(x));"))
            .Then("AHRF008 identifies the discarded Result", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DiscardedTapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Theory]
    [InlineData("Block callback drops Save result", "default(Result<Option<int>>).TapSome(x => { Save(x); });")]
    [InlineData("Expression callback discards returned Option", "default(Result<Option<int>>).TapSome(x => Find(x));")]
    public Task Analyze_TapSomeDiscardingResultOrOption_ReportsAHRF008(string example, string statement) =>
        Scenario.Define("A TapSome callback that discards a Result or Option reports AHRF008", _discardedStepResult)
            .When($"The analyzer inspects a TapSome callback: {example}", _ =>
                AnalyzeAsync(statement))
            .Then("AHRF008 identifies the discarded value", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DiscardedTapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TapSomeReturningResult_ReportsNothing() =>
        Scenario.Define("TapSome returning a Result does not report a discarded value", _discardedStepResult)
            .When("The analyzer inspects a TapSome callback returning its Result", _ =>
                AnalyzeAsync("default(Result<Option<int>>).TapSome(x => Save(x));"))
            .Then("No discarded-result diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_BlockBodyWithoutReturn_ReportsAHRF008() =>
        Scenario.Define("A block-bodied Tap callback that drops a Result reports AHRF008", _discardedStepResult)
            .When("The analyzer inspects a block callback that does not return its Result", _ =>
                AnalyzeAsync("default(Result<int>).Tap(x => { Save(x); });"))
            .Then("AHRF008 identifies the discarded Result", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DiscardedTapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_BlockBodyInsideCondition_ReportsAHRF008() =>
        Scenario.Define("A Result discarded inside a conditional Tap body reports AHRF008", _discardedStepResult)
            .When("The analyzer inspects a conditional block that drops its Result", _ =>
                AnalyzeAsync("default(Result<int>).Tap(x => { if (x > 0) { Save(x); } });"))
            .Then("AHRF008 identifies the discarded Result", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DiscardedTapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_AsyncBlockAwaitingResult_ReportsAHRF008() =>
        Scenario.Define("An awaited Result discarded by an async Tap callback reports AHRF008", _discardedStepResult)
            .When("The analyzer inspects an async callback that awaits but does not return a Result", _ =>
                AnalyzeAsync("default(Result<int>).TapAsync(async x => { await SaveAsync(x); });"))
            .Then("AHRF008 identifies the discarded Result", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DiscardedTapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_ExpressionReturningTaskOfOtherFamily_ReportsAHRF008() =>
        Scenario.Define("A task of another result family discarded by TapAsync reports AHRF008", _discardedStepResult)
            .When("The analyzer inspects an Option TapAsync callback returning Task<Result>", _ =>
                AnalyzeAsync("Option<int>.None.TapAsync(x => SaveAsync(x));"))
            .Then("AHRF008 identifies the discarded task result", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DiscardedTapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_MethodGroupReturningTaskOfOtherFamily_ReportsAHRF008() =>
        Scenario.Define("A method group returning another task family reports AHRF008", _discardedStepResult)
            .When("The analyzer inspects an Option TapAsync method group returning Task<Result>", _ =>
                AnalyzeAsync("Option<int>.None.TapAsync(SaveAsync);"))
            .Then("AHRF008 identifies the discarded task result", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DiscardedTapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TaskExtension_ReportsAHRF008() =>
        Scenario.Define("A task Option extension discarding a Result reports AHRF008", _discardedStepResult)
            .When("The analyzer inspects a task-composed Option TapAsync callback", _ =>
                AnalyzeAsync("Task.FromResult(Option<int>.None).TapAsync(x => Save(x));"))
            .Then("AHRF008 identifies the discarded Result", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DiscardedTapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_PayloadFreeResultBlockBody_ReportsAHRF008() =>
        Scenario.Define("A payload-free Tap callback that drops a Result reports AHRF008", _discardedStepResult)
            .When("The analyzer inspects a payload-free block callback", _ =>
                AnalyzeAsync("default(Result).Tap(() => { Save(1); });"))
            .Then("AHRF008 identifies the discarded Result", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DiscardedTapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_SameFamilyStep_ReportsNothing() =>
        Scenario.Define("Tap callbacks returning their own result family remain valid", _discardedStepResult)
            .When("The analyzer inspects Result and Option callbacks that return the same family", _ =>
                AnalyzeAsync("""
                    default(Result<int>).Tap(x => Save(x));
                    default(Result<int>).Tap(Save);
                    default(Result<int>).TapAsync(x => SaveAsync(x));
                    default(Result<int>).TapAsync(async x => await SaveAsync(x));
                    Option<int>.None.Tap(x => Find(x));
                    """))
            .Then("No discarded-result diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_DeliberateDiscard_ReportsNothing() =>
        Scenario.Define("Explicitly discarded callback results are not reported", _discardedStepResult)
            .When("The analyzer inspects callbacks that explicitly discard their results", _ =>
                AnalyzeAsync("""
                    default(Result<int>).Tap(x => { _ = Save(x); });
                    Option<int>.None.Tap(x => _ = Save(x));
                    default(Result<int>).TapAsync(async x => { _ = await SaveAsync(x); });
                    """))
            .Then("No discarded-result diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_PlainSideEffects_ReportNothing() =>
        Scenario.Define("Ordinary side effects in Tap callbacks are not reported", _discardedStepResult)
            .When("The analyzer inspects callbacks that only perform side effects", _ =>
                AnalyzeAsync("""
                    var list = new List<int>();
                    default(Result<int>).Tap(x => Log(x));
                    default(Result<int>).Tap(Log);
                    default(Result<int>).Tap(x => list.Add(x));
                    default(Result<int>).Tap(x => list.Remove(x));
                    default(Result<int>).TapAsync(x => LogAsync(x));
                    default(Result<int>).TapAsync(async x => await LogAsync(x));
                    """))
            .Then("No discarded-result diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_NestedLambda_ReportsNothing() =>
        Scenario.Define("A Result captured by a nested uninvoked lambda is not reported", _discardedStepResult)
            .When("The analyzer inspects a nested lambda that is not invoked", _ =>
                AnalyzeAsync("default(Result<int>).Tap(x => { Func<Result> later = () => { return Save(x); }; });"))
            .Then("No discarded-result diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_OtherMethodsAndTypes_ReportNothing() =>
        Scenario.Define("Other methods and unrelated types are not treated as Result Tap callbacks", _discardedStepResult)
            .When("The analyzer inspects TapError and an unrelated Tap method", _ =>
                AnalyzeAsync("""
                    default(Result<int>).TapError(e => { Save(e); });
                    new Other().Tap(x => { Save(x); });
                    """))
            .Then("No discarded-result diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

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
