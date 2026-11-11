using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class PreferCancellableTryAsyncAnalyzerTests
{
    internal const string ResultDefinitions = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;

        namespace Ahr.Foundation
        {
            public readonly struct Result
            {
                public static Result Try(Action operation) => default;
                public static Task<Result> TryAsync(Func<Task> operation) => default!;
                public static Task<Result<T>> TryAsync<T>(Func<Task<T>> operation) => default!;
                public static Task<Result<T, TError>> TryAsync<T, TError>(Func<Task<T>> operation, Func<Exception, TError> errorFactory) => default!;
                public static Task<Result> TryAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken) => default!;
                public static Task<Result<T>> TryAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) => default!;
                public static Task<Result<T, TError>> TryAsync<T, TError>(Func<CancellationToken, Task<T>> operation, Func<Exception, TError> errorFactory, CancellationToken cancellationToken) => default!;
            }
            public readonly struct Result<T> { }
            public readonly struct Result<T, TError> { }
        }
        """;

    internal const string Usings = """
        using System;
        using System.Threading;
        using System.Threading.Tasks;
        using Ahr.Foundation;
        class Client { public Task<string> GetAsync(string url, CancellationToken token) => Task.FromResult(url); public Task SaveAsync(CancellationToken token) => Task.CompletedTask; }
        class Context { public CancellationToken RequestAborted { get; } }
        """;

    [Fact]
    public async Task Analyze_ParameterTokenCaptured_ReportsInfoAHRF007OnLambda()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            "Task M(Client client, CancellationToken ct) => Result.TryAsync(() => client.GetAsync(\"u\", ct));");

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("AHRF007", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
        Assert.Contains("'ct'", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.StartsWith("() =>", (await diagnostic.Location.SourceTree!.GetTextAsync(TestContext.Current.CancellationToken)).ToString(diagnostic.Location.SourceSpan), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_PayloadFreeOverload_ReportsAHRF007()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            "Task M(Client client, CancellationToken ct) => Result.TryAsync(() => client.SaveAsync(ct));");
        Assert.Equal("AHRF007", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_ErrorFactoryOverload_ReportsAHRF007()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            "Task M(Client client, CancellationToken ct) => Result.TryAsync(() => client.GetAsync(\"u\", ct), ex => ex.Message);");
        Assert.Equal("AHRF007", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_LocalToken_ReportsAHRF007()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
            Task M(Client client, CancellationTokenSource source)
            {
                var ct = source.Token;
                return Result.TryAsync(async () => await client.GetAsync("u", ct));
            }
            """);
        Assert.Equal("AHRF007", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_PropertyToken_ReportsAHRF007()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            "Task M(Client client, Context context) => Result.TryAsync(() => client.GetAsync(\"u\", context.RequestAborted));");
        Assert.Contains("'context.RequestAborted'", Assert.Single(diagnostics).GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_TokenNoneOrDefault_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
            void M(Client client)
            {
                _ = Result.TryAsync(() => client.GetAsync("u", CancellationToken.None));
                _ = Result.TryAsync(() => client.GetAsync("u", default));
            }
            """);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_TokenCreatedInsideOperation_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
            Task M(Client client) => Result.TryAsync(async () =>
            {
                using var timeout = new CancellationTokenSource(1000);
                return await client.GetAsync("u", timeout.Token);
            });
            """);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_CancellableOverloadAlreadyUsed_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            "Task M(Client client, CancellationToken ct) => Result.TryAsync(token => client.GetAsync(\"u\", token), ct);");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_MethodGroup_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            "Task M() => Result.TryAsync(LoadAsync); Task<int> LoadAsync() => Task.FromResult(1);");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_SynchronousTry_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            "void M(CancellationToken ct) => _ = Result.Try(() => ct.ThrowIfCancellationRequested());");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WithoutCancellableOverload_ReportsNothing()
    {
        const string definitions = """
            using System;
            using System.Threading.Tasks;
            namespace Ahr.Foundation
            {
                public readonly struct Result
                {
                    public static Task<Result<T>> TryAsync<T>(Func<Task<T>> operation) => default!;
                }
                public readonly struct Result<T> { }
            }
            """;
        ImmutableArray<Diagnostic> diagnostics = await AnalyzerTestHarness.AnalyzeAsync(
            new PreferCancellableTryAsyncAnalyzer(),
            definitions,
            Wrap("Task M(Client client, CancellationToken ct) => Result.TryAsync(() => client.GetAsync(\"u\", ct));"));
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_TwoDistinctTokens_ReportsWithoutFixableProperty()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(
            "Task M(Client client, CancellationToken a, CancellationToken b) => Result.TryAsync(async () => { await client.SaveAsync(a); return await client.GetAsync(\"u\", b); });");

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.False(diagnostic.Properties.ContainsKey("Fixable"));
    }

    internal static string Wrap(string member) => $$"""
        {{Usings}}
        class C
        {
            {{member}}
        }
        """;

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string member) =>
        AnalyzerTestHarness.AnalyzeAsync(new PreferCancellableTryAsyncAnalyzer(), ResultDefinitions, Wrap(member));
}
