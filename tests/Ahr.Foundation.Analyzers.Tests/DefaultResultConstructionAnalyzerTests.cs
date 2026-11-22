using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class DefaultResultConstructionAnalyzerTests
{
    private static readonly BusinessContext _resultInitialization = BusinessContext
        .Define("explicit-result-initialization", "Explicit Result initialization", "Prevent Result values from entering an invalid default-constructed state.")
        .Capability("construct-valid-results", "Construct Results through explicit factories")
        .Outcome("invalid-result-defaults-rejected", "Default and parameterless Result construction is reported")
        .Outcome("safe-default-options-allowed", "Default Option values and unrelated types remain allowed")
        .Rule("use-explicit-result-factories", "Create Results with their explicit success or failure factories")
        .Build();

    private const string ResultDefinitions = """
        namespace Ahr.Foundation
        {
            public readonly struct Result
            {
                public static Result Success() => new Result(1);
                private Result(int _) { }
            }
            public readonly struct Result<T>
            {
                public static Result<T> Success(T value) => new Result<T>(value);
                private Result(T value) { }
            }
            public readonly struct Result<T, TError>
            {
                public static Result<T, TError> Failure(TError error) => new Result<T, TError>(error);
                private Result(TError error) { }
            }
            public readonly struct Option<T> { }
        }
        """;

    [Fact]
    public Task Analyze_DefaultExpression_ReportsAHRF001() =>
        Scenario.Define("A default Result expression reports AHRF001", _resultInitialization)
            .When("The analyzer inspects a default payload-free Result expression", _ =>
                AnalyzeAsync("class C { Ahr.Foundation.Result M() => default(Ahr.Foundation.Result); }"))
            .Then("AHRF001 is reported as an error", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, DefaultResultConstructionAnalyzer.DiagnosticId, DiagnosticSeverity.Error))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TargetTypedDefault_ReportsAHRF001() =>
        Scenario.Define("A target-typed Result default reports AHRF001", _resultInitialization)
            .When("The analyzer inspects a target-typed Result default", _ =>
                AnalyzeAsync("class C { Ahr.Foundation.Result<int> M() => default; }"))
            .Then("AHRF001 identifies the invalid construction", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DefaultResultConstructionAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_ParameterlessExplicitNew_ReportsAHRF001() =>
        Scenario.Define("A parameterless custom-error Result constructor reports AHRF001", _resultInitialization)
            .When("The analyzer inspects a parameterless custom-error Result constructor", _ =>
                AnalyzeAsync("class C { Ahr.Foundation.Result<int, string> M() => new Ahr.Foundation.Result<int, string>(); }"))
            .Then("AHRF001 identifies the invalid construction", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DefaultResultConstructionAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_ParameterlessExplicitNewForEveryResultShape_ReportsAHRF001() =>
        Scenario.Define("Parameterless Result constructors are reported for every Result shape", _resultInitialization)
            .When("The analyzer inspects all Result constructor shapes", _ =>
                AnalyzeAsync("""
                    class C
                    {
                        Ahr.Foundation.Result A() => new Ahr.Foundation.Result();
                        Ahr.Foundation.Result<int> B() => new Ahr.Foundation.Result<int>();
                        Ahr.Foundation.Result<int, string> D() => new Ahr.Foundation.Result<int, string>();
                    }
                    """))
            .Then("AHRF001 identifies each invalid constructor", (ImmutableArray<Diagnostic> diagnostics) =>
                Assert.Equal([DefaultResultConstructionAnalyzer.DiagnosticId, DefaultResultConstructionAnalyzer.DiagnosticId, DefaultResultConstructionAnalyzer.DiagnosticId], diagnostics.Select(diagnostic => diagnostic.Id)))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TargetTypedParameterlessNew_ReportsAHRF001() =>
        Scenario.Define("A target-typed parameterless Result constructor reports AHRF001", _resultInitialization)
            .When("The analyzer inspects a target-typed parameterless constructor", _ =>
                AnalyzeAsync("class C { Ahr.Foundation.Result<int> M() => new(); }"))
            .Then("AHRF001 identifies the invalid construction", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DefaultResultConstructionAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_ZeroInitializedArray_ReportsAHRF005() =>
        Scenario.Define("A zero-initialized Result array reports AHRF005", _resultInitialization)
            .When("The analyzer inspects an uninitialized Result array", _ =>
                AnalyzeAsync("class C { object M() => new Ahr.Foundation.Result[2]; }"))
            .Then("AHRF005 is reported as informational guidance", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, DefaultResultConstructionAnalyzer.ArrayDiagnosticId, DiagnosticSeverity.Info))
            .VerifyAsync();

    [Fact]
    public Task Analyze_EmptyInitializedArray_ReportsAHRF005() =>
        Scenario.Define("An empty generic Result array reports AHRF005", _resultInitialization)
            .When("The analyzer inspects a Result array without element initializers", _ =>
                AnalyzeAsync("class C { object M() => new Ahr.Foundation.Result<int>[] { }; }"))
            .Then("AHRF005 identifies the uninitialized array", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DefaultResultConstructionAnalyzer.ArrayDiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_CustomErrorResultArray_ReportsAHRF005() =>
        Scenario.Define("A zero-initialized custom-error Result array reports AHRF005", _resultInitialization)
            .When("The analyzer inspects a custom-error Result array without initializers", _ =>
                AnalyzeAsync("class C { object M() => new Ahr.Foundation.Result<int, string>[3]; }"))
            .Then("AHRF005 identifies the uninitialized array", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DefaultResultConstructionAnalyzer.ArrayDiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_OptionArray_ReportsNothing() =>
        Scenario.Define("An Option array is not reported as a Result array", _resultInitialization)
            .When("The analyzer inspects an Option array", _ =>
                AnalyzeAsync("class C { object M() => new Ahr.Foundation.Option<int>[3]; }"))
            .Then("No Result construction diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_DefaultCustomErrorResult_ReportsAHRF001() =>
        Scenario.Define("A default custom-error Result reports AHRF001", _resultInitialization)
            .When("The analyzer inspects a default custom-error Result", _ =>
                AnalyzeAsync("class C { Ahr.Foundation.Result<int, string> M() => default; }"))
            .Then("AHRF001 identifies the invalid construction", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DefaultResultConstructionAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_AliasedResult_ReportsAHRF001() =>
        Scenario.Define("A default Result alias reports AHRF001", _resultInitialization)
            .When("The analyzer inspects a default through a Result alias", _ =>
                AnalyzeAsync("using R = Ahr.Foundation.Result<int>; class C { R M() => default; }"))
            .Then("AHRF001 identifies the invalid construction", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, DefaultResultConstructionAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_GenericTypeParameter_ReportsNoDiagnostic() =>
        Scenario.Define("A generic type parameter default is not an invalid Result construction", _resultInitialization)
            .When("The analyzer inspects a default generic type parameter", _ =>
                AnalyzeAsync("class C<T> { T M() => default; }"))
            .Then("No Result construction diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_ExplicitFactories_ReportsNoDiagnostic() =>
        Scenario.Define("Explicit Result factories do not report AHRF001", _resultInitialization)
            .When("The analyzer inspects Results created by their success factories", _ =>
                AnalyzeAsync("class C { Ahr.Foundation.Result M() => Ahr.Foundation.Result.Success(); Ahr.Foundation.Result<int> N() => Ahr.Foundation.Result<int>.Success(1); }"))
            .Then("No Result construction diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_DefaultOption_ReportsNoDiagnostic() =>
        Scenario.Define("A default Option remains valid", _resultInitialization)
            .When("The analyzer inspects a default Option", _ =>
                AnalyzeAsync("class C { Ahr.Foundation.Option<int> M() => default; }"))
            .Then("No Result construction diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_UnrelatedResultType_ReportsNoDiagnostic() =>
        Scenario.Define("An unrelated Result type is not reported", _resultInitialization)
            .When("The analyzer inspects a Result type outside Ahr.Foundation", _ =>
                AnalyzeAsync("namespace Other { struct Result { } class C { Result M() => default; } }"))
            .Then("No Result construction diagnostic is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
        => await AnalyzerTestHarness.AnalyzeAsync(
            new DefaultResultConstructionAnalyzer(),
            ResultDefinitions,
            source);
}
