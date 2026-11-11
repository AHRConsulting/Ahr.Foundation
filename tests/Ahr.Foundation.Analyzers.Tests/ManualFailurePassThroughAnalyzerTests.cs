using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class ManualFailurePassThroughAnalyzerTests
{
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
    [InlineData("Result<int> M(Result<string> x) => x.IsFailure ? Result<int>.Failure(x.Error) : Next(x.Value);")]
    [InlineData("Result<int> M(Result<string> x) => x.IsSuccess ? Next(x.Value) : Result<int>.Failure(x.Error);")]
    [InlineData("Result<int> M(Result<string> x) => !x.IsSuccess ? Result<int>.Failure(x.Error) : Next(x.Value);")]
    [InlineData("Result<int> M(Result<string> x) { if (x.IsFailure) return Result<int>.Failure(x.Error); return Next(x.Value); }")]
    [InlineData("Result<int> M(Result<string> x) { if (!x.IsSuccess) { return Result<int>.Failure(x.Error); } return Next(x.Value); }")]
    [InlineData("Result<int, string> M(Result<string, string> x) { if (x.IsFailure) return Result<int, string>.Failure(x.Error); return NextCustom(x.Value); }")]
    [InlineData("Result M(Result x) => x.IsFailure ? Result.Failure(x.Error) : Result.Success();")]
    [InlineData("Result<int> M() { Result<string> x = default; if (x.IsFailure) return Result<int>.Failure(x.Error); return Next(x.Value); }")]
    [InlineData("Result<int> M(Result<string>[] all) { foreach (var x in all) { return x.IsFailure ? Result<int>.Failure(x.Error) : Next(x.Value); } return default; }")]
    [InlineData("System.Threading.Tasks.Task<Result<int>> M(Result<string> x) { if (x.IsFailure) { return System.Threading.Tasks.Task.FromResult(Result<int>.Failure(x.Error)); } return NextAsync(x.Value); }")]
    [InlineData("System.Threading.Tasks.Task<Result<int>> M(Result<string> x) => x.IsFailure ? System.Threading.Tasks.Task.FromResult(Result<int>.Failure(x.Error)) : NextAsync(x.Value);")]
    [InlineData("System.Threading.Tasks.ValueTask<Result<int>> M(Result<string> x) { if (x.IsFailure) return System.Threading.Tasks.ValueTask.FromResult(Result<int>.Failure(x.Error)); return default; }")]
    public async Task Analyze_FailurePassedOnUnchanged_ReportsAHRF011(string member)
    {
        Diagnostic diagnostic = Assert.Single(await AnalyzeAsync(member));
        Assert.Equal("AHRF011", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
    }

    [Theory]
    [InlineData("Result<int> M(Result<string> x) => x.IsFailure ? Result<int>.Failure(Wrap(x.Error)) : Next(x.Value);")]
    [InlineData("Result<int> M(Result<string> x) => x.IsFailure ? Result<int>.Failure(new Error(\"changed\")) : Next(x.Value);")]
    [InlineData("Result<int> M(Result<string> x) { if (x.IsFailure) { Log(); return Result<int>.Failure(x.Error); } return Next(x.Value); }")]
    [InlineData("Result<int> M(Result<string>[] all) { foreach (var x in all) { if (x.IsFailure) return Result<int>.Failure(x.Error); } return default; }")]
    [InlineData("Result<int> M(Result<string> x) { try { if (x.IsFailure) return Result<int>.Failure(x.Error); return Next(x.Value); } finally { Log(); } }")]
    [InlineData("Result<int> M(Result<string> x, IDisposable d) { using (d) { if (x.IsFailure) return Result<int>.Failure(x.Error); return Next(x.Value); } }")]
    [InlineData("Result<int> M(Result<string> x, object gate) { lock (gate) { if (x.IsFailure) return Result<int>.Failure(x.Error); return Next(x.Value); } }")]
    [InlineData("Result<int> M(Result<string> a, Result<string> b) { if (a.IsFailure || b.IsFailure) return Result<int>.Failure(a.Error); return Next(a.Value); }")]
    [InlineData("Result<int> M(Result<string> x) { if (x.IsSuccess) { Log(); return Next(x.Value); } else { return Result<int>.Failure(x.Error); } }")]
    [InlineData("Result<int> M(Result<string> x) { if (x.IsFailure) return Result<int>.Failure(x.Error); else return Next(x.Value); }")]
    [InlineData("Result<int> M(Result<string> x, Result<string> y) => x.IsFailure ? Result<int>.Failure(y.Error) : Next(x.Value);")]
    [InlineData("Result<int> M(Result<string, Error> x) => x.IsFailure ? Result<int>.Failure(x.Error) : Next(x.Value);")]
    [InlineData("System.Threading.Tasks.Task<Result<int>> M(Result<string> x) { if (x.IsFailure) return System.Threading.Tasks.Task.FromResult(Result<int>.Failure(Wrap(x.Error))); return NextAsync(x.Value); }")]
    public async Task Analyze_FailureHandledOrOutOfScope_ReportsNothing(string member) =>
        Assert.Empty(await AnalyzeAsync(member));

    [Theory]
    [InlineData(
        "Result<int> M(Result<string> x) => x.IsFailure ? Result<int>.Failure(x.Error) : Next(x.Value);",
        "Result<int> M(Result<string> x) => x.Bind(value => Next(value));")]
    [InlineData(
        "Result<int> M(Result<string> x) => x.IsSuccess ? Next(x.Value + x.Value) : Result<int>.Failure(x.Error);",
        "Result<int> M(Result<string> x) => x.Bind(value => Next(value + value));")]
    [InlineData(
        "Result M(Result x) => x.IsFailure ? Result.Failure(x.Error) : Result.Success();",
        "Result M(Result x) => x.Bind(() => Result.Success());")]
    [InlineData(
        "Result<int> M(Result<string> x, string value) => x.IsFailure ? Result<int>.Failure(x.Error) : Next(x.Value + value);",
        "Result<int> M(Result<string> x, string value) => x.Bind(v => Next(v + value));")]
    public async Task CodeFix_ConditionalExpression_RewritesToBind(string member, string expected)
    {
        var fixedSource = await ApplyFixAsync(member);
        Assert.NotNull(fixedSource);
        Assert.Contains(expected, fixedSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Result<int> M(Result<string> x) { if (x.IsFailure) return Result<int>.Failure(x.Error); return Next(x.Value); }")]
    [InlineData("Result<int> M(Result<string> x) => x.IsFailure ? Result<int>.Failure(x.Error) : x.IsSuccess ? Next(x.Value) : default;")]
    [InlineData("async System.Threading.Tasks.Task<Result<int>> M(Result<string> x) => x.IsFailure ? Result<int>.Failure(x.Error) : await System.Threading.Tasks.Task.FromResult(Next(x.Value));")]
    [InlineData("System.Threading.Tasks.Task<Result<int>> M(Result<string> x) => x.IsFailure ? System.Threading.Tasks.Task.FromResult(Result<int>.Failure(x.Error)) : NextAsync(x.Value);")]
    public async Task CodeFix_ShapeWithoutMechanicalRewrite_OffersNoFix(string member) =>
        Assert.Null(await ApplyFixAsync(member));

    private static Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string member) =>
        AnalyzerTestHarness.AnalyzeAsync(new ManualFailurePassThroughAnalyzer(), ResultDefinitions, Source(member));

    private static async Task<string?> ApplyFixAsync(string member)
    {
        AdhocWorkspace workspace = new();
        Project project = AnalyzerTestHarness.CreateProject(workspace, ResultDefinitions, Source(member));
        Document document = project.Documents.Single(candidate => candidate.Name == "TestClass.cs");

        Compilation compilation = (await document.Project.GetCompilationAsync(TestContext.Current.CancellationToken))!;
        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers([new ManualFailurePassThroughAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        Diagnostic diagnostic = Assert.Single(diagnostics);

        List<CodeAction> actions = [];
        await new ManualFailurePassThroughCodeFixProvider().RegisterCodeFixesAsync(
            new CodeFixContext(document, diagnostic, (action, _) => actions.Add(action), TestContext.Current.CancellationToken));
        if (actions.Count == 0)
        {
            return null;
        }

        ImmutableArray<CodeActionOperation> operations = await Assert.Single(actions).GetOperationsAsync(TestContext.Current.CancellationToken);
        Document changed = Assert.IsType<ApplyChangesOperation>(Assert.Single(operations)).ChangedSolution.GetDocument(document.Id)!;
        return (await changed.GetTextAsync(TestContext.Current.CancellationToken)).ToString();
    }

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
