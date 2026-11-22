using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class PreferResultTryAnalyzerTests
{
    private static readonly BusinessContext _preferResultTry = BusinessContext
        .Define("result-try-exception-boundaries", "Result.Try exception boundaries", "Use Result.Try when an exception boundary translates an operation into Result success or failure.")
        .Capability("capture-boundary-exceptions", "Translate exceptions from operations into Result failures")
        .Outcome("try-catch-replaced-with-try", "A simple catch-all Result conversion is reported")
        .Outcome("exception-semantics-preserved", "Specialized or non-equivalent exception handling is not reported")
        .Rule("prefer-result-try", "Use Result.Try for a catch-all conversion to Result failure")
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
                public static Result Success() => default;
                public static Result Failure(Error error) => default;
                public static Result Try(Action operation) => default;
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
    public Task Analyze_PayloadFreeResult_TryCatchConstructsFailure_ReportsAHRF002() =>
        Scenario.Define("A payload-free Result try/catch conversion reports AHRF002", _preferResultTry)
            .When("The analyzer inspects a catch-all that constructs Result success and failure", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    class C
                    {
                        Result M()
                        {
                            try
                            {
                                DoWork();
                                return Result.Success();
                            }
                            catch (Exception ex)
                            {
                                return Result.Failure(new Error(ex.Message));
                            }
                        }
                        void DoWork() { }
                    }
                    """))
            .Then("AHRF002 identifies the exception conversion", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAnalyzer.DiagnosticId, DiagnosticSeverity.Info))
            .VerifyAsync();

    [Fact]
    public Task Analyze_ResultOfT_TryCatchConstructsFailure_ReportsAHRF002() =>
        Scenario.Define("A generic Result try/catch conversion reports AHRF002", _preferResultTry)
            .When("The analyzer inspects a generic Result catch-all conversion", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    class C
                    {
                        Result<int> M()
                        {
                            try
                            {
                                return Result<int>.Success(DoWork());
                            }
                            catch (Exception ex)
                            {
                                return Result<int>.Failure(new Error(ex.Message));
                            }
                        }
                        int DoWork() => 1;
                    }
                    """))
            .Then("AHRF002 is reported", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_ResultOfTAndTError_TryCatchConstructsFailure_ReportsAHRF002() =>
        Scenario.Define("A custom-error Result try/catch conversion reports AHRF002", _preferResultTry)
            .When("The analyzer inspects a custom-error Result catch-all conversion", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    class C
                    {
                        Result<int, string> M()
                        {
                            try
                            {
                                return Result<int, string>.Success(DoWork());
                            }
                            catch (Exception ex)
                            {
                                return Result<int, string>.Failure(ex.Message);
                            }
                        }
                        int DoWork() => 1;
                    }
                    """))
            .Then("AHRF002 is reported", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_CatchRethrows_ReportsNoDiagnostic() =>
        Scenario.Define("A rethrown exception is not converted into a Result failure", _preferResultTry)
            .When("The analyzer inspects a catch block that rethrows", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    class C
                    {
                        Result M()
                        {
                            try
                            {
                                return Result.Success();
                            }
                            catch (Exception)
                            {
                                throw;
                            }
                        }
                    }
                    """))
            .Then("No Result.Try recommendation is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_CatchDoesSomethingOtherThanConstructResult_ReportsNoDiagnostic() =>
        Scenario.Define("A catch that wraps and rethrows an exception is not reported", _preferResultTry)
            .When("The analyzer inspects a catch that wraps the exception", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    class C
                    {
                        Result M()
                        {
                            try
                            {
                                return Result.Success();
                            }
                            catch (Exception ex)
                            {
                                throw new InvalidOperationException("wrapped", ex);
                            }
                        }
                    }
                    """))
            .Then("No Result.Try recommendation is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_CatchOfUnrelatedSpecificExceptionType_ReportsNoDiagnostic() =>
        Scenario.Define("A catch for a specific unrelated exception is not reported", _preferResultTry)
            .When("The analyzer inspects a catch for FileNotFoundException", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    using System.IO;
                    class C
                    {
                        Result M()
                        {
                            try
                            {
                                return Result.Success();
                            }
                            catch (FileNotFoundException)
                            {
                                throw new InvalidOperationException("io error");
                            }
                        }
                    }
                    """))
            .Then("No Result.Try recommendation is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryBodyDoesNotConstructSuccess_ReportsNoDiagnostic() =>
        Scenario.Define("A try body without a Result success conversion is not reported", _preferResultTry)
            .When("The analyzer inspects a Result failure returned after the try block", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    class C
                    {
                        Result M()
                        {
                            try
                            {
                                DoWork();
                            }
                            catch (Exception ex)
                            {
                                return Result.Failure(new Error(ex.Message));
                            }

                            return Result.Success();
                        }
                        void DoWork() { }
                    }
                    """))
            .Then("No Result.Try recommendation is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryAlreadyUsesResultTry_ReportsNoDiagnostic() =>
        Scenario.Define("An existing Result.Try call is not recommended again", _preferResultTry)
            .When("The analyzer inspects an operation already wrapped by Result.Try", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    class C
                    {
                        Result M()
                        {
                            try
                            {
                                return Result.Try(() => DoWork());
                            }
                            catch (Exception ex)
                            {
                                return Result.Failure(new Error(ex.Message));
                            }
                        }
                        void DoWork() { }
                    }
                    """))
            .Then("No duplicate Result.Try recommendation is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_NestedTryCatch_ReportsOnlyInnermostMatch() =>
        Scenario.Define("Nested catch-all Result conversions report only the innermost match", _preferResultTry)
            .When("The analyzer inspects nested try/catch conversions", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    class C
                    {
                        Result M()
                        {
                            try
                            {
                                try
                                {
                                    return Result.Success();
                                }
                                catch (Exception innerEx)
                                {
                                    return Result.Failure(new Error(innerEx.Message));
                                }
                            }
                            catch (Exception outerEx)
                            {
                                return Result.Failure(new Error(outerEx.Message));
                            }
                        }
                    }
                    """))
            .Then("Only one Result.Try recommendation is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Fact]
    public Task Analyze_OuterTryWithOnlyFinally_ReportsOnlyInnerMatch() =>
        Scenario.Define("A finally block does not add another Result.Try recommendation", _preferResultTry)
            .When("The analyzer inspects an inner conversion inside an outer finally block", _ =>
                AnalyzeAsync("""
                    using Ahr.Foundation;
                    using System;
                    class C
                    {
                        Result M()
                        {
                            try
                            {
                                try
                                {
                                    return Result.Success();
                                }
                                catch (Exception ex)
                                {
                                    return Result.Failure(new Error(ex.Message));
                                }
                            }
                            finally
                            {
                                Cleanup();
                            }
                        }
                        void Cleanup() { }
                    }
                    """))
            .Then("Only the inner Result.Try recommendation is reported", (ImmutableArray<Diagnostic> diagnostics) =>
                AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Theory]
    [InlineData("A catch-all clause that creates a failure", "catch { return Result.Failure(new Error(\"failed\")); }")]
    [InlineData("An unrelated exception clause followed by a catch-all", "catch (IOException) { return Result.Failure(new Error(\"io\")); } catch (Exception ex) { return Result.Failure(new Error(ex.Message)); }")]
    [InlineData("A cancellation rethrow followed by a catch-all conversion", "catch (OperationCanceledException) { throw; } catch (Exception ex) { return Result.Failure(new Error(ex.Message)); }")]
    public Task Analyze_CatchAllBuildsFailure_ReportsAHRF002(string example, string catches) =>
        Scenario.Define("A catch-all Result failure conversion reports AHRF002", _preferResultTry)
            .When($"The analyzer checks {example}", _ => AnalyzeAsync(WithCatches(catches)))
            .Then("AHRF002 identifies the catch-all conversion", (ImmutableArray<Diagnostic> diagnostics) => AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAnalyzer.DiagnosticId))
            .VerifyAsync();

    [Theory]
    [InlineData("Caller cancellation converted to a failure", "catch (OperationCanceledException) { return Result.Failure(new Error(\"cancelled\")); }")]
    [InlineData("Task cancellation converted to a failure", "catch (TaskCanceledException) { return Result.Failure(new Error(\"cancelled\")); }")]
    [InlineData("Caller cancellation handled before the catch-all", "catch (OperationCanceledException) { return Result.Failure(new Error(\"cancelled\")); } catch (Exception ex) { return Result.Failure(new Error(ex.Message)); }")]
    [InlineData("An unrelated IOException catch", "catch (IOException ex) { return Result.Failure(new Error(ex.Message)); }")]
    [InlineData("A filtered catch for IOException", "catch (Exception ex) when (ex is IOException) { return Result.Failure(new Error(ex.Message)); }")]
    public Task Analyze_CatchThatResultTryCannotReproduce_ReportsNoDiagnostic(string example, string catches) =>
        Scenario.Define("Exception handling that Result.Try cannot reproduce is not reported", _preferResultTry)
            .When($"The analyzer checks {example}", _ => AnalyzeAsync(WithCatches(catches)))
            .Then("No Result.Try recommendation is reported", (ImmutableArray<Diagnostic> diagnostics) => Assert.Empty(diagnostics))
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryCatchFinally_ReportsWithFinallyHint() =>
        Scenario.Define("A finally block is preserved in the Result.Try recommendation", _preferResultTry)
            .When("The analyzer inspects a Result catch-all with cleanup", _ =>
                AnalyzeAsync(WithCatches("catch (Exception ex) { return Result.Failure(new Error(ex.Message)); } finally { DoWork(); }")))
            .Then("The recommendation explains how to retain the finally block", (ImmutableArray<Diagnostic> diagnostics) =>
            {
                Diagnostic diagnostic = AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAnalyzer.DiagnosticId);
                Assert.EndsWith("keep the finally block in a try/finally around the call", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            })
            .VerifyAsync();

    [Fact]
    public Task Analyze_TryCatchWithoutFinally_HasNoFinallyHint() =>
        Scenario.Define("A Result.Try recommendation without finally gives no finally hint", _preferResultTry)
            .When("The analyzer inspects a catch-all Result conversion without cleanup", _ =>
                AnalyzeAsync(WithCatches("catch (Exception ex) { return Result.Failure(new Error(ex.Message)); }")))
            .Then("The recommendation describes the ordinary failure conversion", (ImmutableArray<Diagnostic> diagnostics) =>
            {
                Diagnostic diagnostic = AnalyzerTestHarness.ReportsSingle(diagnostics, PreferResultTryAnalyzer.DiagnosticId);
                Assert.EndsWith("failure", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            })
            .VerifyAsync();

    private static string WithCatches(string catches) => $$"""
        using Ahr.Foundation;
        using System;
        using System.IO;
        using System.Threading.Tasks;
        class C
        {
            Result M()
            {
                try
                {
                    DoWork();
                    return Result.Success();
                }
                {{catches}}
            }
            void DoWork() { }
        }
        """;

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
        => await AnalyzerTestHarness.AnalyzeAsync(
            new PreferResultTryAnalyzer(),
            ResultDefinitions,
            source);
}
