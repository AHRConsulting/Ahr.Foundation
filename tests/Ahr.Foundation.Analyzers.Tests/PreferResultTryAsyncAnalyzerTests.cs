using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class PreferResultTryAsyncAnalyzerTests
{
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
    public async Task Analyze_TryOfT_LambdaReturnsTask_ReportsAHRF003()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("AHRF003", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public async Task Analyze_TryOfT_LambdaReturnsTaskOfT_ReportsAHRF003()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Equal("AHRF003", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_TryOfT_LambdaReturnsValueTask_ReportsAHRF003()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Equal("AHRF003", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_TryOfTAndTError_LambdaReturnsTask_ReportsAHRF003()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Equal("AHRF003", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_TryAction_MethodGroupReturnsTask_ReportsAHRF003()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
            using Ahr.Foundation;
            using System;
            using System.Threading.Tasks;
            class C
            {
                void M()
                {
                    _ = Result.Try((Action)FetchAsync);
                }
                Task FetchAsync() => Task.CompletedTask;
            }
            """);

        Assert.Equal("AHRF003", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_TryAction_LambdaCastDiscardsTask_ReportsAHRF003()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Equal("AHRF003", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_TryAction_UncastAsyncLambdaAwaitsTask_PrefersTryOfT_ReportsAHRF003NotAHRF004()
    {
        // An uncast async lambda binds to Try<T>(Func<T>) with T inferred as Task,
        // so this is an AHRF003 case rather than an async-void AHRF004 case.
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("AHRF003", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public async Task Analyze_TryAction_ActionVariableIsAsyncLambda_NoDiagnostic()
    {
        // Known limitation: the analyzer does not trace a delegate variable back to its
        // async-lambda assignment, so only inline async Action expressions are detected.
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_TryAction_AsyncLambdaCastAwaitsTask_ReportsAHRF004()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Equal("AHRF004", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_TryAction_CastAsyncLambdaWithNoAwaitable_ReportsAHRF004()
    {
        // An async lambda converted to Action is async void even when its body contains no await.
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
            using Ahr.Foundation;
            using System;
            class C
            {
                void M()
                {
                    _ = Result.Try((Action)(async () => Console.WriteLine("work")));
                }
            }
            """);

        Assert.Equal("AHRF004", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_TryOfT_LambdaReturnsNonAwaitable_NoDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
            using Ahr.Foundation;
            class C
            {
                void M()
                {
                    _ = Result.Try(() => 5);
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_TryAction_LambdaDoesNotReturnAwaitable_NoDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
            using Ahr.Foundation;
            using System;
            class C
            {
                void M()
                {
                    Result.Try(() => Console.WriteLine("work"));
                }
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_TryAsync_LambdaReturnsTask_NoDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_TryAsync_AsyncLambdaAwaitsTask_NoDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_UnrelatedTypeNamedResult_NoDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Empty(diagnostics);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
        => await AnalyzerTestHarness.AnalyzeAsync(
            new PreferResultTryAsyncAnalyzer(),
            ResultDefinitions,
            source);
}
