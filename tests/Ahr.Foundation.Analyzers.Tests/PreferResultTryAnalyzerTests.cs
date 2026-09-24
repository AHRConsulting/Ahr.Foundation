using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

public sealed class PreferResultTryAnalyzerTests
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
    public async Task Analyze_PayloadFreeResult_TryCatchConstructsFailure_ReportsAHRF002()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("AHRF002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Info, diagnostic.Severity);
    }

    [Fact]
    public async Task Analyze_ResultOfT_TryCatchConstructsFailure_ReportsAHRF002()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Equal("AHRF002", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_ResultOfTAndTError_TryCatchConstructsFailure_ReportsAHRF002()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Equal("AHRF002", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public async Task Analyze_CatchRethrows_ReportsNoDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_CatchDoesSomethingOtherThanConstructResult_ReportsNoDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_CatchOfUnrelatedSpecificExceptionType_ReportsNoDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_TryBodyDoesNotConstructSuccess_ReportsNoDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_TryAlreadyUsesResultTry_ReportsNoDiagnostic()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_NestedTryCatch_ReportsOnlyInnermostMatch()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        _ = Assert.Single(diagnostics);
    }

    [Fact]
    public async Task Analyze_OuterTryWithOnlyFinally_ReportsOnlyInnerMatch()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("""
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
            """);

        _ = Assert.Single(diagnostics);
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
        => await AnalyzerTestHarness.AnalyzeAsync(
            new PreferResultTryAnalyzer(),
            ResultDefinitions,
            source);
}
