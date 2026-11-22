using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class NullableMapResultAnalyzerTests
{
    private static readonly BusinessContext _nullableMapping = BusinessContext
        .Define("nullable-result-mapping", "Nullable mapping results", "Represent nullable mapping outcomes with Option instead of returning nullable values from Result and Option workflows.")
        .Capability("preserve-nullable-outcomes", "Preserve nullable outcomes explicitly")
        .Outcome("nullable-mapping-reported", "Nullable mapping results are identified")
        .Outcome("non-nullable-mapping-accepted", "Non-nullable mapping results remain valid")
        .Rule("represent-nullable-values-as-options", "Use Option for nullable outcomes from Result and Option mappings")
        .Build();

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
    public Task Analyze_OptionMapToNullableReference_ReportsAHRF006OnLambda() =>
        Scenario.Define("Option.Map returning a nullable reference reports AHRF006", _nullableMapping)
            .When("The analyzer inspects an Option mapping to a nullable property", _ =>
                AnalyzeAsync("Option<string> M(Option<Customer> c) => c.Map(x => x.MiddleName)!;"))
            .Then("AHRF006 identifies the nullable mapping and lambda", (ImmutableArray<Diagnostic> diagnostics) =>
            {
                Diagnostic diagnostic = AnalyzerTestHarness.ReportsSingle(diagnostics, NullableMapResultAnalyzer.DiagnosticId, DiagnosticSeverity.Warning);
                Assert.Contains("string?", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
                Assert.Equal("x => x.MiddleName", diagnostic.Location.SourceTree!.GetText(TestContext.Current.CancellationToken).ToString(diagnostic.Location.SourceSpan));
            })
            .VerifyAsync();

    [Fact]
    public Task Analyze_ResultMapToNullableValueType_ReportsAHRF006() =>
        Scenario.Define("Result.Map returning a nullable value reports AHRF006", _nullableMapping)
            .When("The analyzer inspects a Result mapping to a nullable value property", _ =>
                AnalyzeAsync("void M(Result<Customer> c) => _ = c.Map(x => x.Age);"))
            .Then("AHRF006 identifies the nullable mapping", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, NullableMapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_CustomErrorResultMapToNullable_ReportsAHRF006() =>
        Scenario.Define("Custom-error Result.Map returning a nullable value reports AHRF006", _nullableMapping)
            .When("The analyzer inspects a custom-error Result mapping to a nullable reference", _ =>
                AnalyzeAsync("void M(Result<Customer, int> c) => _ = c.Map(x => x.MiddleName);"))
            .Then("AHRF006 identifies the nullable mapping", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, NullableMapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_AsyncMappingToNullable_ReportsAHRF006() =>
        Scenario.Define("Option.MapAsync returning a nullable value reports AHRF006", _nullableMapping)
            .When("The analyzer inspects an asynchronous Option mapping to a nullable property", _ =>
                AnalyzeAsync("void M(Option<Customer> c) => _ = c.MapAsync(x => Task.FromResult(x.MiddleName));"))
            .Then("AHRF006 identifies the nullable mapping", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, NullableMapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TaskExtensionMapAsyncToNullable_ReportsAHRF006() =>
        Scenario.Define("Task<Option<T>>.MapAsync returning a nullable value reports AHRF006", _nullableMapping)
            .When("The analyzer inspects task-composed Option mapping to a nullable property", _ =>
                AnalyzeAsync("void M(Task<Option<Customer>> c) => _ = c.MapAsync(x => x.MiddleName);"))
            .Then("AHRF006 identifies the nullable mapping", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, NullableMapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_SelectToNullable_ReportsAHRF006() =>
        Scenario.Define("Option LINQ Select returning a nullable value reports AHRF006", _nullableMapping)
            .When("The analyzer inspects an Option Select to a nullable property", _ =>
                AnalyzeAsync("void M(Option<Customer> c) => _ = c.Select(x => x.MiddleName);"))
            .Then("AHRF006 identifies the nullable selection", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, NullableMapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Theory]
    [InlineData("Built-in MapSome to nullable reference", "void M(Result<Option<Customer>> c) => _ = c.MapSome(x => x.MiddleName);")]
    [InlineData("Built-in MapSome to nullable value", "void M(Result<Option<Customer>> c) => _ = c.MapSome(x => x.Age);")]
    [InlineData("Custom-error MapSome to nullable reference", "void M(Result<Option<Customer>, int> c) => _ = c.MapSome(x => x.MiddleName);")]
    [InlineData("MapSomeAsync to nullable reference", "void M(Result<Option<Customer>> c) => _ = c.MapSomeAsync(x => Task.FromResult(x.MiddleName));")]
    [InlineData("Task-composed custom-error MapSomeAsync to nullable reference", "void M(Task<Result<Option<Customer>, int>> c) => _ = c.MapSomeAsync(x => x.MiddleName);")]
    public Task Analyze_MapSomeToNullable_ReportsAHRF006(string example, string member) =>
        Scenario.Define("MapSome returning a nullable value reports AHRF006", _nullableMapping)
            .When($"The analyzer inspects {example}", _ => AnalyzeAsync(member))
            .Then("AHRF006 identifies the nullable mapping", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, NullableMapResultAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Theory]
    [InlineData("Try returning a nullable reference", "void M(Customer c) => _ = Result.Try(() => c.MiddleName);")]
    [InlineData("custom-error Try returning a nullable value", "void M(Customer c) => _ = Result.Try(() => c.Age, ex => ex.Message);")]
    [InlineData("TryAsync returning a nullable object", "void M(Func<System.Threading.CancellationToken, Task<object?>> show) => _ = Result.TryAsync(show, default);")]
    public Task Analyze_TryToNullable_ReportsAHRF006WithOptionAdvice(string example, string member) =>
        Scenario.Define("Result.Try returning a nullable value advises using Option", _nullableMapping)
            .When($"The analyzer inspects {example}", _ => AnalyzeAsync(member))
            .Then("AHRF006 recommends representing the nullable outcome as Option", (ImmutableArray<Diagnostic> diagnostics) =>
            {
                Diagnostic diagnostic = AnalyzerTestHarness.ReportsSingle(diagnostics, NullableMapResultAnalyzer.DiagnosticId);
                Assert.Contains("return an Option from the operation", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            })
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryToNonNullable_ReportsNothing() =>
        Scenario.Define("Result.Try returning non-nullable values is not reported", _nullableMapping)
            .When("The analyzer inspects non-nullable and Option-wrapped Try results", _ =>
                AnalyzeAsync("void M(Customer c) { _ = Result.Try(() => c.Name); _ = Result.Try(() => c.MiddleName.ToOption()); _ = Result.Try(() => { }); }"))
            .Then("No nullable-result diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_MapSomeToNonNullable_ReportsNothing() =>
        Scenario.Define("MapSome returning non-nullable values is not reported", _nullableMapping)
            .When("The analyzer inspects MapSome mappings with non-nullable results", _ =>
                AnalyzeAsync("void M(Result<Option<Customer>> c) { _ = c.MapSome(x => x.Name); _ = c.MapSome(x => x.Age ?? 0); }"))
            .Then("No nullable-result diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_MapToNonNullable_ReportsNothing() =>
        Scenario.Define("Option.Map returning non-nullable values is not reported", _nullableMapping)
            .When("The analyzer inspects non-nullable Option mappings", _ =>
                AnalyzeAsync("void M(Option<Customer> c) { _ = c.Map(x => x.Name); _ = c.Map(x => x.Age ?? 0); }"))
            .Then("No nullable-result diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_BindWithToOption_ReportsNothing() =>
        Scenario.Define("Option.Bind returning Option from a nullable property is not reported", _nullableMapping)
            .When("The analyzer inspects a Bind that converts a nullable value to Option", _ =>
                AnalyzeAsync("void M(Option<Customer> c) => _ = c.Bind(x => x.MiddleName.ToOption());"))
            .Then("No nullable-map diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_NullableDisabledContext_ReportsNothing() =>
        Scenario.Define("Nullable mapping analysis is inactive when nullable annotations are disabled", _nullableMapping)
            .When("The analyzer inspects a mapping in a nullable-disabled context", _ =>
                AnalyzeAsync("void M(Option<Customer> c) => _ = c.Map(x => x.Name);", nullable: "disable"))
            .Then("No nullable-map diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_MapOnUnrelatedType_ReportsNothing() =>
        Scenario.Define("A Map method on an unrelated type is not reported", _nullableMapping)
            .When("The analyzer inspects a similarly named method outside the Foundation types", _ =>
                AnalyzeAsync("""
                    void M(Other c) => _ = c.Map(x => x.MiddleName);
                    }
                    class Other { public Other Map(Func<Customer, string?> map) => this; }
                    class Unused {
                    """))
            .Then("No nullable-map diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

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
