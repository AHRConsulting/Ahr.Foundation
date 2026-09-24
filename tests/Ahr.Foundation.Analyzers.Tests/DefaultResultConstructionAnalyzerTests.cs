using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class DefaultResultConstructionAnalyzerTests
{
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
    public async Task Analyze_DefaultExpression_ReportsAHRF001()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("class C { Ahr.Foundation.Result M() => default(Ahr.Foundation.Result); }");
        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("AHRF001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public async Task Analyze_TargetTypedDefault_ReportsAHRF001()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("class C { Ahr.Foundation.Result<int> M() => default; }");
        Assert.Equal("AHRF001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_ParameterlessExplicitNew_ReportsAHRF001()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("class C { Ahr.Foundation.Result<int, string> M() => new Ahr.Foundation.Result<int, string>(); }");
        Assert.Equal("AHRF001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_ParameterlessExplicitNewForEveryResultShape_ReportsAHRF001()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
            class C
            {
                Ahr.Foundation.Result A() => new Ahr.Foundation.Result();
                Ahr.Foundation.Result<int> B() => new Ahr.Foundation.Result<int>();
                Ahr.Foundation.Result<int, string> C() => new Ahr.Foundation.Result<int, string>();
            }
            """);
        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal("AHRF001", diagnostic.Id));
    }

    [Fact]
    public async Task Analyze_TargetTypedParameterlessNew_ReportsAHRF001()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("class C { Ahr.Foundation.Result<int> M() => new(); }");
        Assert.Equal("AHRF001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_ZeroInitializedArray_ReportsAHRF001()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("class C { object M() => new Ahr.Foundation.Result[2]; }");
        Assert.Equal("AHRF001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_EmptyInitializedArray_ReportsAHRF001()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("class C { object M() => new Ahr.Foundation.Result<int>[2] { }; }");
        Assert.Equal("AHRF001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_DefaultCustomErrorResult_ReportsAHRF001()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("class C { Ahr.Foundation.Result<int, string> M() => default; }");
        Assert.Equal("AHRF001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_AliasedResult_ReportsAHRF001()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("using R = Ahr.Foundation.Result<int>; class C { R M() => default; }");
        Assert.Equal("AHRF001", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_GenericTypeParameter_ReportsNoDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("class C<T> { T M() => default; }");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_ExplicitFactories_ReportsNoDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("class C { Ahr.Foundation.Result M() => Ahr.Foundation.Result.Success(); Ahr.Foundation.Result<int> N() => Ahr.Foundation.Result<int>.Success(1); }");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_DefaultOption_ReportsNoDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("class C { Ahr.Foundation.Option<int> M() => default; }");
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_UnrelatedResultType_ReportsNoDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("namespace Other { struct Result { } class C { Result M() => default; } }");
        Assert.Empty(diagnostics);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
        => await AnalyzerTestHarness.AnalyzeAsync(
            new DefaultResultConstructionAnalyzer(),
            ResultDefinitions,
            source);
}
