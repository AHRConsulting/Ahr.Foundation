using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ahr.Foundation.Analyzers;

/// <summary>Suggests the three-way helpers when <c>GetValueOrDefault</c> or <c>ToOption</c> on a result of an option discards the failure.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FlattenedResultOfOptionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier for a call that discards the failure of a result of an option.</summary>
    public const string DiagnosticId = "AHRF009";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Keep the failure of a result of an option",
        "'{0}' on '{1}' discards the failure; use Match with onSome, onNone, and onFailure, SomeOr, or EnsureSome, to keep it",
        "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "On a result of an option, GetValueOrDefault returns None for a failure, so a failure looks like a missing value, and ToOption returns a nested option without the error. Use the three-way Match, SomeOr for a default value, or EnsureSome when the failure matters.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol method = invocation.TargetMethod;

        if (method.Name is not ("GetValueOrDefault" or "ToOption") ||
            method.ContainingType is not { Name: "Result", Arity: > 0 } result ||
            !IsFoundationType(result) ||
            result.TypeArguments[0] is not INamedTypeSymbol { Name: "Option", Arity: 1 } option ||
            !IsFoundationType(option))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            _rule,
            invocation.Syntax.GetLocation(),
            method.Name,
            result.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static bool IsFoundationType(ITypeSymbol type) =>
        type.ContainingNamespace is { Name: "Foundation", ContainingNamespace.Name: "Ahr" };
}
