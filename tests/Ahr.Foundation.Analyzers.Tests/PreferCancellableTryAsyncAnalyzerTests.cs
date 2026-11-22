using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class PreferCancellableTryAsyncAnalyzerTests
{
    private static readonly BusinessContext _cancellableTry = BusinessContext
        .Define("caller-cancellation-propagation", "Caller cancellation propagation", "Pass a caller's cancellation token to asynchronous Result.Try boundaries.")
        .Capability("preserve-caller-cancellation", "Preserve cancellation requested by the caller")
        .Outcome("token-forwarded-to-try-async", "The asynchronous operation receives the caller token")
        .Outcome("ambiguous-token-not-fixed", "Ambiguous cancellation-token sources are not given a code fix")
        .Rule("pass-caller-token-to-try-async", "Use the cancellable TryAsync overload when an operation uses the caller token")
        .Build();

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
    public Task Analyze_ParameterTokenCaptured_ReportsInfoAHRF007OnLambda() =>
        Scenario.Define("A captured caller token reports AHRF007 on the operation lambda", _cancellableTry)
            .When("The analyzer inspects an operation that captures a cancellation parameter", _ =>
                AnalyzeAsync("Task M(Client client, CancellationToken ct) => Result.TryAsync(() => client.GetAsync(\"u\", ct));"))
            .Then("AHRF007 identifies the token and operation lambda", (ImmutableArray<Diagnostic> diagnostics) =>
            {
                Diagnostic diagnostic = AnalyzerTestHarness.ReportsSingle(diagnostics, PreferCancellableTryAsyncAnalyzer.DiagnosticId, DiagnosticSeverity.Info);
                Assert.Contains("'ct'", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
                Assert.StartsWith("() =>", diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan), StringComparison.Ordinal);
            })
            .VerifyAsync();

    [Fact]
    public Task Analyze_PayloadFreeOverload_ReportsAHRF007() =>
        Scenario.Define("A payload-free TryAsync call reports a captured caller token", _cancellableTry)
            .When("The analyzer inspects a payload-free operation that uses the caller token", _ =>
                AnalyzeAsync("Task M(Client client, CancellationToken ct) => Result.TryAsync(() => client.SaveAsync(ct));"))
            .Then("AHRF007 identifies the missing cancellable overload", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, PreferCancellableTryAsyncAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_ErrorFactoryOverload_ReportsAHRF007() =>
        Scenario.Define("A custom-error TryAsync call reports a captured caller token", _cancellableTry)
            .When("The analyzer inspects a custom-error operation that uses the caller token", _ =>
                AnalyzeAsync("Task M(Client client, CancellationToken ct) => Result.TryAsync(() => client.GetAsync(\"u\", ct), ex => ex.Message);"))
            .Then("AHRF007 identifies the missing cancellable overload", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, PreferCancellableTryAsyncAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_LocalToken_ReportsAHRF007() =>
        Scenario.Define("A local token derived from the caller reports AHRF007", _cancellableTry)
            .When("The analyzer inspects a local copy of the caller token", _ =>
                AnalyzeAsync("""
                    Task M(Client client, CancellationTokenSource source)
                    {
                        var ct = source.Token;
                        return Result.TryAsync(async () => await client.GetAsync("u", ct));
                    }
                    """))
            .Then("AHRF007 identifies the missing cancellable overload", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, PreferCancellableTryAsyncAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_PropertyToken_ReportsAHRF007() =>
        Scenario.Define("A caller cancellation property reports AHRF007", _cancellableTry)
            .When("The analyzer inspects an operation using RequestAborted", _ =>
                AnalyzeAsync("Task M(Client client, Context context) => Result.TryAsync(() => client.GetAsync(\"u\", context.RequestAborted));"))
            .Then("AHRF007 identifies the property token", (ImmutableArray<Diagnostic> diagnostics) =>
            {
                Diagnostic diagnostic = AnalyzerTestHarness.ReportsSingle(diagnostics, PreferCancellableTryAsyncAnalyzer.DiagnosticId);
                Assert.Contains("'context.RequestAborted'", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            })
            .VerifyAsync();

    [Fact]
    public Task Analyze_TokenNoneOrDefault_ReportsNothing() =>
        Scenario.Define("None and default cancellation tokens do not report AHRF007", _cancellableTry)
            .When("The analyzer inspects operations using None and default tokens", _ =>
                AnalyzeAsync("""
                    void M(Client client)
                    {
                        _ = Result.TryAsync(() => client.GetAsync("u", CancellationToken.None));
                        _ = Result.TryAsync(() => client.GetAsync("u", default));
                    }
                    """))
            .Then("No cancellable overload recommendation is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TokenCreatedInsideOperation_ReportsNothing() =>
        Scenario.Define("A token created within the operation is not treated as caller cancellation", _cancellableTry)
            .When("The analyzer inspects a token created inside the asynchronous operation", _ =>
                AnalyzeAsync("""
                    Task M(Client client) => Result.TryAsync(async () =>
                    {
                        using var timeout = new CancellationTokenSource(1000);
                        return await client.GetAsync("u", timeout.Token);
                    });
                    """))
            .Then("No caller-token recommendation is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_CancellableOverloadAlreadyUsed_ReportsNothing() =>
        Scenario.Define("An already cancellable TryAsync call is not reported", _cancellableTry)
            .When("The analyzer inspects a call that forwards its token", _ =>
                AnalyzeAsync("Task M(Client client, CancellationToken ct) => Result.TryAsync(token => client.GetAsync(\"u\", token), ct);"))
            .Then("No additional cancellable overload recommendation is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_MethodGroup_ReportsNothing() =>
        Scenario.Define("A method group without a captured token is not reported", _cancellableTry)
            .When("The analyzer inspects a method group passed to TryAsync", _ =>
                AnalyzeAsync("Task M() => Result.TryAsync(LoadAsync); Task<int> LoadAsync() => Task.FromResult(1);"))
            .Then("No caller-token recommendation is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_SynchronousTry_ReportsNothing() =>
        Scenario.Define("A synchronous Result.Try call is not reported for cancellation", _cancellableTry)
            .When("The analyzer inspects a synchronous Result.Try operation", _ =>
                AnalyzeAsync("void M(CancellationToken ct) => _ = Result.Try(() => ct.ThrowIfCancellationRequested());"))
            .Then("No asynchronous cancellation recommendation is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_WithoutCancellableOverload_ReportsNothing()
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

        return Scenario.Define("No cancellable overload means no AHRF007 recommendation", _cancellableTry)
            .When("The analyzer inspects a token-capturing call with no cancellable API", _ =>
                AnalyzerTestHarness.AnalyzeAsync(
                    new PreferCancellableTryAsyncAnalyzer(),
                    definitions,
                    Wrap("Task M(Client client, CancellationToken ct) => Result.TryAsync(() => client.GetAsync(\"u\", ct));")))
            .Then("No unsupported overload recommendation is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Empty(diagnostics))
            .VerifyAsync();
    }

    [Fact]
    public Task Analyze_TwoDistinctTokens_ReportsWithoutFixableProperty() =>
        Scenario.Define("Distinct cancellation tokens report AHRF007 without a code-fix property", _cancellableTry)
            .When("The analyzer inspects one operation using two cancellation tokens", _ =>
                AnalyzeAsync("Task M(Client client, CancellationToken a, CancellationToken b) => Result.TryAsync(async () => { await client.SaveAsync(a); return await client.GetAsync(\"u\", b); });"))
            .Then("The diagnostic does not claim that one token can be safely forwarded", (ImmutableArray<Diagnostic> diagnostics) =>
            {
                Diagnostic diagnostic = AnalyzerTestHarness.ReportsSingle(diagnostics, PreferCancellableTryAsyncAnalyzer.DiagnosticId);
                Assert.False(diagnostic.Properties.ContainsKey("Fixable"));
            })
            .VerifyAsync();

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
