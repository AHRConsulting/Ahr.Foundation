using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class NullableMapResultAnalyzerTests
{
    private const string ResultDefinitions = """
        #nullable enable
        using System;
        using System.Threading.Tasks;

        namespace Ahr.Foundation
        {
            public readonly struct Error { }
            public readonly struct Option<T>
            {
                public Option<TOut> Map<TOut>(Func<T, TOut> map) => default;
                public Task<Option<TOut>> MapAsync<TOut>(Func<T, Task<TOut>> map) => default!;
                public Option<TOut> Bind<TOut>(Func<T, Option<TOut>> bind) => default;
            }
            public readonly struct Result
            {
                public static Result Try(Action operation) => default;
                public static Result<T> Try<T>(Func<T> operation) => default;
                public static Result<T, TError> Try<T, TError>(Func<T> operation, Func<Exception, TError> errorFactory) => default;
                public static Task<Result<T>> TryAsync<T>(Func<System.Threading.CancellationToken, Task<T>> operation, System.Threading.CancellationToken cancellationToken) => default!;
            }
            public readonly struct Result<T>
            {
                public Result<TOut> Map<TOut>(Func<T, TOut> map) => default;
            }
            public readonly struct Result<T, TError>
            {
                public Result<TOut, TError> Map<TOut>(Func<T, TOut> map) => default;
            }
            public static class ResultExtensions
            {
                public static Result<Option<TOut>> MapSome<T, TOut>(this Result<Option<T>> result, Func<T, TOut> map) => default;
                public static Result<Option<TOut>, TError> MapSome<T, TError, TOut>(this Result<Option<T>, TError> result, Func<T, TOut> map) => default;
                public static Task<Result<Option<TOut>>> MapSomeAsync<T, TOut>(this Result<Option<T>> result, Func<T, Task<TOut>> map) => default!;
            }
            public static class OptionExtensions
            {
                public static Option<T> ToOption<T>(this T? value) where T : class => default;
            }
            public static class LinqExtensions
            {
                public static Option<TOut> Select<T, TOut>(this Option<T> option, Func<T, TOut> selector) => default;
            }
            public static class TaskCompositionExtensions
            {
                public static Task<Option<TOut>> MapAsync<T, TOut>(this Task<Option<T>> optionTask, Func<T, TOut> map) => default!;
                public static Task<Result<Option<TOut>, TError>> MapSomeAsync<T, TError, TOut>(this Task<Result<Option<T>, TError>> resultTask, Func<T, TOut> map) => default!;
            }
        }
        """;

    private const string Customer = "class Customer { public string Name = \"\"; public string? MiddleName; public int? Age; }";

    [Fact]
    public async Task Analyze_OptionMapToNullableReference_ReportsAHRF006OnLambda()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("Option<string> M(Option<Customer> c) => c.Map(x => x.MiddleName)!;");

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("AHRF006", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("string?", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.Equal("x => x.MiddleName", (await diagnostic.Location.SourceTree!.GetTextAsync(TestContext.Current.CancellationToken)).ToString(diagnostic.Location.SourceSpan));
    }

    [Fact]
    public async Task Analyze_ResultMapToNullableValueType_ReportsAHRF006()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("void M(Result<Customer> c) => _ = c.Map(x => x.Age);");
        Assert.Equal("AHRF006", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_CustomErrorResultMapToNullable_ReportsAHRF006()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("void M(Result<Customer, int> c) => _ = c.Map(x => x.MiddleName);");
        Assert.Equal("AHRF006", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_AsyncMappingToNullable_ReportsAHRF006()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("void M(Option<Customer> c) => _ = c.MapAsync(x => Task.FromResult(x.MiddleName));");
        Assert.Equal("AHRF006", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_TaskExtensionMapAsyncToNullable_ReportsAHRF006()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("void M(Task<Option<Customer>> c) => _ = c.MapAsync(x => x.MiddleName);");
        Assert.Equal("AHRF006", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_SelectToNullable_ReportsAHRF006()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("void M(Option<Customer> c) => _ = c.Select(x => x.MiddleName);");
        Assert.Equal("AHRF006", Assert.Single(diagnostics).Id);
    }

    [Theory]
    [InlineData("void M(Result<Option<Customer>> c) => _ = c.MapSome(x => x.MiddleName);")]
    [InlineData("void M(Result<Option<Customer>> c) => _ = c.MapSome(x => x.Age);")]
    [InlineData("void M(Result<Option<Customer>, int> c) => _ = c.MapSome(x => x.MiddleName);")]
    [InlineData("void M(Result<Option<Customer>> c) => _ = c.MapSomeAsync(x => Task.FromResult(x.MiddleName));")]
    [InlineData("void M(Task<Result<Option<Customer>, int>> c) => _ = c.MapSomeAsync(x => x.MiddleName);")]
    public async Task Analyze_MapSomeToNullable_ReportsAHRF006(string member)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(member);
        Assert.Equal("AHRF006", Assert.Single(diagnostics).Id);
    }

    [Theory]
    [InlineData("void M(Customer c) => _ = Result.Try(() => c.MiddleName);")]
    [InlineData("void M(Customer c) => _ = Result.Try(() => c.Age, ex => ex.Message);")]
    [InlineData("void M(Func<System.Threading.CancellationToken, Task<object?>> show) => _ = Result.TryAsync(show, default);")]
    public async Task Analyze_TryToNullable_ReportsAHRF006WithOptionAdvice(string member)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(member);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("AHRF006", diagnostic.Id);
        Assert.Contains("return an Option from the operation", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_TryToNonNullable_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("void M(Customer c) { _ = Result.Try(() => c.Name); _ = Result.Try(() => c.MiddleName.ToOption()); _ = Result.Try(() => { }); }");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_MapSomeToNonNullable_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("void M(Result<Option<Customer>> c) { _ = c.MapSome(x => x.Name); _ = c.MapSome(x => x.Age ?? 0); }");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_MapToNonNullable_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("void M(Option<Customer> c) { _ = c.Map(x => x.Name); _ = c.Map(x => x.Age ?? 0); }");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_BindWithToOption_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("void M(Option<Customer> c) => _ = c.Bind(x => x.MiddleName.ToOption());");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_NullableDisabledContext_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("void M(Option<Customer> c) => _ = c.Map(x => x.Name);", nullable: "disable");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_MapOnUnrelatedType_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
            void M(Other c) => _ = c.Map(x => x.MiddleName);
            }
            class Other { public Other Map(Func<Customer, string?> map) => this; }
            class Unused {
            """);
        Assert.Empty(diagnostics);
    }

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string member, string nullable = "enable") =>
        AnalyzerTestHarness.AnalyzeAsync(
            new NullableMapResultAnalyzer(),
            ResultDefinitions,
            $$"""
            #nullable {{nullable}}
            using System;
            using System.Linq;
            using System.Threading.Tasks;
            using Ahr.Foundation;
            {{Customer}}
            class C
            {
                {{member}}
            }
            """);
}
