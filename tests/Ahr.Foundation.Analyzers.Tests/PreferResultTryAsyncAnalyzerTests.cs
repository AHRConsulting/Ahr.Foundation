using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class PreferResultTryAsyncAnalyzerTests
{
    private static readonly BusinessContext _asyncResultTry = BusinessContext
        .Define("async-result-try-boundaries", "Asynchronous Result.Try boundaries", "Use asynchronous Result.Try APIs when operations return awaitables.")
        .Capability("capture-async-operation-errors", "Capture exceptions from asynchronous operations")
        .Outcome("async-operation-uses-try-async", "Awaitable operations are handled by Result.TryAsync")
        .Outcome("async-void-is-avoided", "An async delegate is not silently converted to async void")
        .Rule("match-try-api-to-operation", "Use Result.TryAsync for operations that return an awaitable")
        .Build();

    private const string ResultDefinitions = """
        using System;
        using System.Threading.Tasks;

        namespace Ahr.Foundation
        {
            public readonly struct Error
            {
                public Error(string message) { }
                public static Error FromException(Exception exception) => default;
            }
            public readonly struct Result
            {
                public static Result Success() => default;
                public static Result Failure(Error error) => default;
                public static Result Try(Action operation) => default;
                public static Result<T> Try<T>(Func<T> operation) => default;
                public static Result<T, TError> Try<T, TError>(Func<T> operation, Func<Exception, TError> errorFactory) => default;
                public static Task<Result> TryAsync(Func<Task> operation) => default!;
                public static Task<Result<T>> TryAsync<T>(Func<Task<T>> operation) => default!;
                public static Task<Result<T, TError>> TryAsync<T, TError>(Func<Task<T>> operation, Func<Exception, TError> errorFactory) => default!;
            }
            public readonly struct Result<T>
            {
                public static Result<T> Success(T value) => default;
                public static Result<T> Failure(Error error) => default;
            }
            public readonly struct Result<T, TError>
            {
                public static Result<T, TError> Success(T value) => default;
                public static Result<T, TError> Failure(TError error) => default;
            }
        }
        """;

    [Fact]
    public Task Analyze_TryOfT_LambdaReturnsTask_ReportsAHRF003() =>
        Scenario.Define("A synchronous Result.Try that discards a Task reports AHRF003", _asyncResultTry)
            .When("The analyzer inspects a Result.Try call whose lambda returns Task", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System.Threading.Tasks;
                    class C
                    {
                        void M()
                        {
                            _ = Result.Try(() => FetchAsync());
                        }
                        Task FetchAsync() => Task.CompletedTask;
                    }
                    """))
            .Then("AHRF003 warns that the asynchronous operation is discarded", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAsyncAnalyzer.DiagnosticId, DiagnosticSeverity.Warning))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryOfT_LambdaReturnsTaskOfT_ReportsAHRF003() =>
        Scenario.Define("A synchronous Result.Try that discards Task<T> reports AHRF003", _asyncResultTry)
            .When("The analyzer inspects a Result.Try call whose lambda returns Task<int>", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System.Threading.Tasks;
                    class C
                    {
                        void M()
                        {
                            _ = Result.Try(() => FetchAsync());
                        }
                        Task<int> FetchAsync() => Task.FromResult(1);
                    }
                    """))
            .Then("AHRF003 identifies the discarded Task result", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAsyncAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryOfT_LambdaReturnsValueTask_ReportsAHRF003() =>
        Scenario.Define("A synchronous Result.Try that discards a ValueTask reports AHRF003", _asyncResultTry)
            .When("The analyzer inspects a Result.Try call whose lambda returns ValueTask", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System.Threading.Tasks;
                    class C
                    {
                        void M()
                        {
                            _ = Result.Try(() => FetchAsync());
                        }
                        ValueTask FetchAsync() => default;
                    }
                    """))
            .Then("AHRF003 identifies the discarded ValueTask", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAsyncAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryOfTAndTError_LambdaReturnsTask_ReportsAHRF003() =>
        Scenario.Define("A custom-error Result.Try that discards a Task reports AHRF003", _asyncResultTry)
            .When("The analyzer inspects a custom-error Result.Try call whose lambda returns Task", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    using System.Threading.Tasks;
                    class C
                    {
                        void M()
                        {
                            _ = Result.Try(() => FetchAsync(), Error.FromException);
                        }
                        Task FetchAsync() => Task.CompletedTask;
                    }
                    """))
            .Then("AHRF003 identifies the discarded asynchronous operation", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAsyncAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_Try_MethodGroupReturnsTask_ReportsAHRF003() =>
        Scenario.Define("A synchronous Result.Try method group returning Task reports AHRF003", _asyncResultTry)
            .When("The analyzer inspects a Task-returning method group", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    using System.Threading.Tasks;
                    class C
                    {
                        void M()
                        {
                            _ = Result.Try(FetchAsync);
                        }
                        Task FetchAsync() => Task.CompletedTask;
                    }
                    """))
            .Then("AHRF003 identifies the discarded Task", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAsyncAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryAction_LambdaCastDiscardsTask_ReportsAHRF003() =>
        Scenario.Define("A synchronous Action cast that discards a Task reports AHRF003", _asyncResultTry)
            .When("The analyzer inspects an Action that calls a Task-returning operation", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    using System.Threading.Tasks;
                    class C
                    {
                        void M()
                        {
                            _ = Result.Try((Action)(() => FetchAsync()));
                        }
                        Task FetchAsync() => Task.CompletedTask;
                    }
                    """))
            .Then("AHRF003 identifies the discarded Task", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAsyncAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryAction_UncastAsyncLambdaAwaitsTask_PrefersTryOfT_ReportsAHRF003NotAHRF004() =>
        Scenario.Define("An uncast async lambda returning Task reports AHRF003, not AHRF004", _asyncResultTry)
            .When("The analyzer inspects an uncast async lambda passed to Result.Try", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System.Threading.Tasks;
                    class C
                    {
                        void M()
                        {
                            _ = Result.Try(async () => await FetchAsync());
                        }
                        Task FetchAsync() => Task.CompletedTask;
                    }
                    """))
            .Then("AHRF003 is reported instead of the async-void diagnostic", (ImmutableArray<Diagnostic> diagnostics) =>
            {
                _ = AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAsyncAnalyzer.DiagnosticId, DiagnosticSeverity.Warning);
                Assert.DoesNotContain(diagnostics, item => item.Id == PreferResultTryAsyncAnalyzer.AsyncVoidDiagnosticId);
            })
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryAction_ActionVariableIsAsyncLambda_NoDiagnostic() =>
        Scenario.Define("An async Action stored in a variable is outside the analyzer's data-flow scope", _asyncResultTry)
            .When("The analyzer inspects a variable assigned from an async lambda", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    using System.Threading.Tasks;
                    class C
                    {
                        void M()
                        {
                            Action operation = async () => await FetchAsync();
                            _ = Result.Try(operation);
                        }
                        Task FetchAsync() => Task.CompletedTask;
                    }
                    """))
            .Then("No async-void diagnostic is reported for the variable", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryAction_AsyncLambdaCastAwaitsTask_ReportsAHRF004() =>
        Scenario.Define("An async lambda explicitly cast to Action reports AHRF004", _asyncResultTry)
            .When("The analyzer inspects an async-void Action passed to Result.Try", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    using System.Threading.Tasks;
                    class C
                    {
                        void M()
                        {
                            _ = Result.Try((Action)(async () => await FetchAsync()));
                        }
                        Task FetchAsync() => Task.CompletedTask;
                    }
                    """))
            .Then("AHRF004 identifies the async-void delegate", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAsyncAnalyzer.AsyncVoidDiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryAction_CastAsyncLambdaWithNoAwaitable_ReportsAHRF004() =>
        Scenario.Define("An async lambda cast to Action reports AHRF004 even without await", _asyncResultTry)
            .When("The analyzer inspects an async Action with no await expression", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    class C
                    {
                        void M()
                        {
                            _ = Result.Try((Action)(async () => Console.WriteLine("work")));
                        }
                    }
                    """))
            .Then("AHRF004 identifies the async-void delegate", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAsyncAnalyzer.AsyncVoidDiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryOfT_LambdaReturnsNonAwaitable_NoDiagnostic() =>
        Scenario.Define("A synchronous Result.Try returning an ordinary value is valid", _asyncResultTry)
            .When("The analyzer inspects a lambda returning an integer", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    class C
                    {
                        void M()
                        {
                            _ = Result.Try(() => 5);
                        }
                    }
                    """))
            .Then("No asynchronous-operation diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryAction_LambdaDoesNotReturnAwaitable_NoDiagnostic() =>
        Scenario.Define("A synchronous Action passed to Result.Try is valid", _asyncResultTry)
            .When("The analyzer inspects a synchronous action", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    class C
                    {
                        void M()
                        {
                            Result.Try(() => Console.WriteLine("work"));
                        }
                    }
                    """))
            .Then("No asynchronous-operation diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryAsync_LambdaReturnsTask_NoDiagnostic() =>
        Scenario.Define("Result.TryAsync accepts a lambda that returns Task", _asyncResultTry)
            .When("The analyzer inspects a Task-returning Result.TryAsync call", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System.Threading.Tasks;
                    class C
                    {
                        async Task M()
                        {
                            _ = await Result.TryAsync(() => FetchAsync());
                        }
                        Task FetchAsync() => Task.CompletedTask;
                    }
                    """))
            .Then("No asynchronous-operation diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryAsync_AsyncLambdaAwaitsTask_NoDiagnostic() =>
        Scenario.Define("Result.TryAsync accepts an async lambda that awaits Task", _asyncResultTry)
            .When("The analyzer inspects an awaited Result.TryAsync call", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System.Threading.Tasks;
                    class C
                    {
                        async Task M()
                        {
                            _ = await Result.TryAsync(async () => await FetchAsync());
                        }
                        Task FetchAsync() => Task.CompletedTask;
                    }
                    """))
            .Then("No asynchronous-operation diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_UnrelatedTypeNamedResult_NoDiagnostic() =>
        Scenario.Define("An unrelated type named Result is not reported", _asyncResultTry)
            .When("The analyzer inspects a similarly named type outside Ahr.Foundation", _ =>
                AnalyzeAsync("""
                    using System;
                    using System.Threading.Tasks;
                    class Result
                    {
                        public static Result Try(Func<Task> operation) => default!;
                    }
                    class C
                    {
                        void M()
                        {
                            _ = Result.Try(() => FetchAsync());
                        }
                        Task FetchAsync() => Task.CompletedTask;
                    }
                    """))
            .Then("No Result.Try diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Empty(diagnostics))
            .VerifyAsync();

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
        => await AnalyzerTestHarness.AnalyzeAsync(
            new PreferResultTryAsyncAnalyzer(),
            ResultDefinitions,
            source);
}
