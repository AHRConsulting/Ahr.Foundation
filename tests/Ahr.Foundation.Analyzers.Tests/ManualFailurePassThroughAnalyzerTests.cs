using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class ManualFailurePassThroughAnalyzerTests
{
    private static readonly BusinessContext _failurePassThrough = BusinessContext
        .Define("result-failure-pass-through", "Result failure propagation", "Continue a failed Result pipeline without replacing its original failure.")
        .Capability("preserve-result-failures", "Preserve a Result failure when continuing a pipeline")
        .Capability("compose-with-bind", "Use Bind to compose operations that return results")
        .Outcome("failure-preserved-through-bind", "A failure passes through dependent Result operations")
        .Outcome("mechanical-fix-offered", "Simple failure pass-through branches can be replaced with Bind")
        .Rule("do-not-rebuild-unchanged-failures", "Use Bind instead of manually checking and reconstructing an unchanged failure")
        .Build();

    private const string ResultDefinitions = """
        using System;

        namespace Ahr.Foundation
        {
            public readonly struct Error
            {
                public Error(string message) { }
            }
            public readonly struct Result
            {
                public bool IsSuccess => true;
                public bool IsFailure => false;
                public Error Error => default;
                public static Result Success() => default;
                public static Result Failure(Error error) => default;
                public Result Bind(Func<Result> bind) => this;
            }
            public readonly struct Result<T>
            {
                public bool IsSuccess => true;
                public bool IsFailure => false;
                public T Value => default!;
                public Error Error => default;
                public static Result<T> Success(T value) => default;
                public static Result<T> Failure(Error error) => default;
                public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind) => default;
            }
            public readonly struct Result<T, TError>
            {
                public bool IsSuccess => true;
                public bool IsFailure => false;
                public T Value => default!;
                public TError Error => default!;
                public static Result<T, TError> Failure(TError error) => default;
                public Result<TOut, TError> Bind<TOut>(Func<T, Result<TOut, TError>> bind) => default;
            }
        }
        """;

    private const string Helpers = """
        static Result<int> Next(string value) => default;
        static Result<int, string> NextCustom(string value) => default;
        static void Log() { }
        static Error Wrap(Error error) => error;
        static System.Threading.Tasks.Task<Result<int>> NextAsync(string value) => default;
        """;

    [Theory]
    [InlineData("A conditional expression checking IsFailure", "Result<int> M(Result<string> x) => x.IsFailure ? Result<int>.Failure(x.Error) : Next(x.Value);")]
    [InlineData("A conditional expression checking IsSuccess", "Result<int> M(Result<string> x) => x.IsSuccess ? Next(x.Value) : Result<int>.Failure(x.Error);")]
    [InlineData("A negated IsSuccess conditional expression", "Result<int> M(Result<string> x) => !x.IsSuccess ? Result<int>.Failure(x.Error) : Next(x.Value);")]
    [InlineData("An if statement that returns the original failure", "Result<int> M(Result<string> x) { if (x.IsFailure) return Result<int>.Failure(x.Error); return Next(x.Value); }")]
    [InlineData("A negated IsSuccess if statement", "Result<int> M(Result<string> x) { if (!x.IsSuccess) { return Result<int>.Failure(x.Error); } return Next(x.Value); }")]
    [InlineData("A custom-error Result failure passed on unchanged", "Result<int, string> M(Result<string, string> x) { if (x.IsFailure) return Result<int, string>.Failure(x.Error); return NextCustom(x.Value); }")]
    [InlineData("A payload-free Result conditional expression", "Result M(Result x) => x.IsFailure ? Result.Failure(x.Error) : Result.Success();")]
    [InlineData("A local Result failure passed on unchanged", "Result<int> M() { Result<string> x = default; if (x.IsFailure) return Result<int>.Failure(x.Error); return Next(x.Value); }")]
    [InlineData("A Result failure passed on inside a loop", "Result<int> M(Result<string>[] all) { foreach (var x in all) { return x.IsFailure ? Result<int>.Failure(x.Error) : Next(x.Value); } return default; }")]
    [InlineData("A task result with a manually returned failure", "System.Threading.Tasks.Task<Result<int>> M(Result<string> x) { if (x.IsFailure) { return System.Threading.Tasks.Task.FromResult(Result<int>.Failure(x.Error)); } return NextAsync(x.Value); }")]
    [InlineData("A task result conditional expression", "System.Threading.Tasks.Task<Result<int>> M(Result<string> x) => x.IsFailure ? System.Threading.Tasks.Task.FromResult(Result<int>.Failure(x.Error)) : NextAsync(x.Value);")]
    [InlineData("A ValueTask result with a manually returned failure", "System.Threading.Tasks.ValueTask<Result<int>> M(Result<string> x) { if (x.IsFailure) return System.Threading.Tasks.ValueTask.FromResult(Result<int>.Failure(x.Error)); return default; }")]
    public Task Analyze_FailurePassedOnUnchanged_ReportsAHRF011(string example, string member) =>
        Scenario.Define("Unchanged Result failure pass-through reports AHRF011", _failurePassThrough)
            .When($"The analyzer checks {example}", _ => AnalyzeAsync(member))
            .Then("AHRF011 identifies the manually forwarded failure", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, ManualFailurePassThroughAnalyzer.DiagnosticId, DiagnosticSeverity.Info))
            .VerifyAsync();

    [Theory]
    [InlineData("A failure transformed by a wrapper", "Result<int> M(Result<string> x) => x.IsFailure ? Result<int>.Failure(Wrap(x.Error)) : Next(x.Value);")]
    [InlineData("A failure replaced with a new error", "Result<int> M(Result<string> x) => x.IsFailure ? Result<int>.Failure(new Error(\"changed\")) : Next(x.Value);")]
    [InlineData("An if statement with a side effect before forwarding", "Result<int> M(Result<string> x) { if (x.IsFailure) { Log(); return Result<int>.Failure(x.Error); } return Next(x.Value); }")]
    [InlineData("A loop that observes a failure", "Result<int> M(Result<string>[] all) { foreach (var x in all) { if (x.IsFailure) return Result<int>.Failure(x.Error); } return default; }")]
    [InlineData("A try/finally around failure handling", "Result<int> M(Result<string> x) { try { if (x.IsFailure) return Result<int>.Failure(x.Error); return Next(x.Value); } finally { Log(); } }")]
    [InlineData("A using scope around failure handling", "Result<int> M(Result<string> x, IDisposable d) { using (d) { if (x.IsFailure) return Result<int>.Failure(x.Error); return Next(x.Value); } }")]
    [InlineData("A lock scope around failure handling", "Result<int> M(Result<string> x, object gate) { lock (gate) { if (x.IsFailure) return Result<int>.Failure(x.Error); return Next(x.Value); } }")]
    [InlineData("A branch handling two results", "Result<int> M(Result<string> a, Result<string> b) { if (a.IsFailure || b.IsFailure) return Result<int>.Failure(a.Error); return Next(a.Value); }")]
    [InlineData("A success branch with a side effect", "Result<int> M(Result<string> x) { if (x.IsSuccess) { Log(); return Next(x.Value); } else { return Result<int>.Failure(x.Error); } }")]
    [InlineData("An explicit else branch", "Result<int> M(Result<string> x) { if (x.IsFailure) return Result<int>.Failure(x.Error); else return Next(x.Value); }")]
    [InlineData("A failure copied from a different result", "Result<int> M(Result<string> x, Result<string> y) => x.IsFailure ? Result<int>.Failure(y.Error) : Next(x.Value);")]
    [InlineData("A custom error converted to a built-in error", "Result<int> M(Result<string, Error> x) => x.IsFailure ? Result<int>.Failure(x.Error) : Next(x.Value);")]
    [InlineData("A task result failure wrapped with a different error", "System.Threading.Tasks.Task<Result<int>> M(Result<string> x) { if (x.IsFailure) return System.Threading.Tasks.Task.FromResult(Result<int>.Failure(Wrap(x.Error))); return NextAsync(x.Value); }")]
    public Task Analyze_FailureHandledOrOutOfScope_ReportsNothing(string example, string member) =>
        Scenario.Define("Handled or changed Result failures are not reported", _failurePassThrough)
            .When($"The analyzer checks {example}", _ => AnalyzeAsync(member))
            .Then("No AHRF011 diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Empty(diagnostics))
            .VerifyAsync();

    [Theory]
    [InlineData(
        "A conditional result with a built-in error",
        "Result<int> M(Result<string> x) => x.IsFailure ? Result<int>.Failure(x.Error) : Next(x.Value);",
        "Result<int> M(Result<string> x) => x.Bind(value => Next(value));")]
    [InlineData(
        "A conditional result that reuses its value",
        "Result<int> M(Result<string> x) => x.IsSuccess ? Next(x.Value + x.Value) : Result<int>.Failure(x.Error);",
        "Result<int> M(Result<string> x) => x.Bind(value => Next(value + value));")]
    [InlineData(
        "A payload-free conditional result",
        "Result M(Result x) => x.IsFailure ? Result.Failure(x.Error) : Result.Success();",
        "Result M(Result x) => x.Bind(() => Result.Success());")]
    [InlineData(
        "A conditional result that also captures an external value",
        "Result<int> M(Result<string> x, string value) => x.IsFailure ? Result<int>.Failure(x.Error) : Next(x.Value + value);",
        "Result<int> M(Result<string> x, string value) => x.Bind(v => Next(v + value));")]
    public Task CodeFix_ConditionalExpression_RewritesToBind(string example, string member, string expected) =>
        Scenario.Define("The AHRF011 code fix replaces manual propagation with Bind", _failurePassThrough)
            .When($"The code fix rewrites {example}", _ => ApplyFixAsync(member))
            .Then("AHRF011 is reported for the manual failure pass-through", (CodeFixOutcome outcome) =>
                AnalyzerTestHarness.ReportsSingle(outcome.Diagnostics, ManualFailurePassThroughAnalyzer.DiagnosticId))
            .And("A single code fix is offered", (CodeFixOutcome outcome) =>
                Assert.Single(outcome.ActionTitles))
            .And("The fix is a single change to the solution", (CodeFixOutcome outcome) =>
                Assert.Equal([nameof(ApplyChangesOperation)], outcome.Operations))
            .And("The rewritten code contains the expected Bind pipeline", (CodeFixOutcome outcome) =>
                Assert.Contains(expected, outcome.FixedSource, StringComparison.Ordinal))
            .And("No AHRF011 diagnostic remains after the fix", (CodeFixOutcome outcome) =>
                Assert.Empty(outcome.DiagnosticsAfterFix))
            .VerifyAsync();

    [Theory]
    [InlineData("A block-bodied if statement", "Result<int> M(Result<string> x) { if (x.IsFailure) return Result<int>.Failure(x.Error); return Next(x.Value); }")]
    [InlineData("A conditional expression with a nested branch", "Result<int> M(Result<string> x) => x.IsFailure ? Result<int>.Failure(x.Error) : x.IsSuccess ? Next(x.Value) : default;")]
    [InlineData("An async conditional expression", "async System.Threading.Tasks.Task<Result<int>> M(Result<string> x) => x.IsFailure ? Result<int>.Failure(x.Error) : await System.Threading.Tasks.Task.FromResult(Next(x.Value));")]
    [InlineData("A task-returning conditional expression", "System.Threading.Tasks.Task<Result<int>> M(Result<string> x) => x.IsFailure ? System.Threading.Tasks.Task.FromResult(Result<int>.Failure(x.Error)) : NextAsync(x.Value);")]
    public Task CodeFix_ShapeWithoutMechanicalRewrite_OffersNoFix(string example, string member) =>
        Scenario.Define("The AHRF011 code fix declines unsupported shapes", _failurePassThrough)
            .When($"The code fix examines {example}", _ => ApplyFixAsync(member))
            .Then("AHRF011 is reported for the manual failure pass-through", (CodeFixOutcome outcome) =>
                AnalyzerTestHarness.ReportsSingle(outcome.Diagnostics, ManualFailurePassThroughAnalyzer.DiagnosticId))
            .And("No mechanical rewrite is offered", (CodeFixOutcome outcome) =>
                Assert.Empty(outcome.ActionTitles))
            .VerifyAsync();

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string member) =>
        AnalyzerTestHarness.AnalyzeAsync(new ManualFailurePassThroughAnalyzer(), ResultDefinitions, Source(member));

    private static Task<CodeFixOutcome> ApplyFixAsync(string member) =>
        AnalyzerTestHarness.RunCodeFixAsync(
            AnalyzerTestHarness.CreateProject(new AdhocWorkspace(), ResultDefinitions, Source(member)),
            new ManualFailurePassThroughAnalyzer(),
            new ManualFailurePassThroughCodeFixProvider(),
            TestContext.Current.CancellationToken);

    private static string Source(string member) => $$"""
        #nullable disable
        using System;
        using Ahr.Foundation;
        class C
        {
            {{Helpers}}
            {{member}}
        }
        """;
}
