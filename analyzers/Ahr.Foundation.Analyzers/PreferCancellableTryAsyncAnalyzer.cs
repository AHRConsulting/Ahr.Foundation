using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ahr.Foundation.Analyzers;

/// <summary>Reports a <c>Result.TryAsync</c> call whose operation uses a caller's <see cref="CancellationToken"/> without passing it to the cancellation-aware overload.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferCancellableTryAsyncAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier for a TryAsync operation that uses a cancellation token from its caller.</summary>
    public const string DiagnosticId = "AHRF007";

    internal const string FixableProperty = "Fixable";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Pass the cancellation token to Result.TryAsync",
        "The operation passed to '{0}' uses the cancellation token '{1}'; pass it to the TryAsync overload that takes a CancellationToken so only that token's cancellation propagates and other cancellations, such as timeouts, become a Failure",
        "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Result.TryAsync overloads without a CancellationToken propagate every OperationCanceledException, including cancellations the caller did not request, such as an HttpClient timeout. The overloads that take a CancellationToken propagate only the caller's cancellation and convert any other cancellation into a Failure. Switching overloads changes how those other cancellations surface, so review callers that catch TaskCanceledException before applying the change.");

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

        if (method.Name != "TryAsync" ||
            method.ContainingType is not { Name: "Result", Arity: 0 } containingType ||
            containingType.ContainingNamespace.ToDisplayString() != "Ahr.Foundation" ||
            method.Parameters.Any(parameter => IsCancellationToken(parameter.Type)) ||
            !HasCancellableOverload(containingType, method) ||
            invocation.Arguments.Length == 0 ||
            UnwrapDelegateTarget(invocation.Arguments[0].Value) is not IAnonymousFunctionOperation operation)
        {
            return;
        }

        SyntaxNode lambda = operation.Syntax;
        var tokens = operation.Body
            .Descendants()
            .Where(reference => IsCancellationToken(reference.Type) && IsCallerToken(reference, lambda))
            .ToList();
        if (tokens.Count == 0)
        {
            return;
        }

        var outermost = tokens.Where(reference => !tokens.Any(other => other != reference && other.Syntax.Span.Contains(reference.Syntax.Span))).ToList();
        var distinct = outermost.Select(reference => reference.Syntax.ToString()).Distinct(StringComparer.Ordinal).ToList();

        ImmutableDictionary<string, string?> properties = distinct.Count == 1
            ? ImmutableDictionary<string, string?>.Empty.Add(FixableProperty, "true")
            : ImmutableDictionary<string, string?>.Empty;

        context.ReportDiagnostic(Diagnostic.Create(
            _rule,
            lambda.GetLocation(),
            outermost.Select(reference => reference.Syntax.GetLocation()),
            properties,
            method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            string.Join("', '", distinct)));
    }

    private static bool HasCancellableOverload(INamedTypeSymbol containingType, IMethodSymbol method) =>
        containingType.GetMembers("TryAsync").OfType<IMethodSymbol>().Any(candidate =>
            candidate.Arity == method.Arity &&
            candidate.Parameters.Length == method.Parameters.Length + 1 &&
            IsCancellationToken(candidate.Parameters[candidate.Parameters.Length - 1].Type));

    // A caller's token is declared outside the lambda; CancellationToken.None is excluded.
    private static bool IsCallerToken(IOperation reference, SyntaxNode lambda) => reference switch
    {
        ILocalReferenceOperation local => IsDeclaredOutside(local.Local, lambda),
        IParameterReferenceOperation parameter => IsDeclaredOutside(parameter.Parameter, lambda),
        IFieldReferenceOperation { Instance: null } field => !IsCancellationToken(field.Field.ContainingType),
        IPropertyReferenceOperation { Instance: null } property => !IsCancellationToken(property.Property.ContainingType),
        IMemberReferenceOperation { Instance: { } instance } => IsOutsideInstance(instance, lambda),
        _ => false,
    };

    private static bool IsOutsideInstance(IOperation instance, SyntaxNode lambda) => instance switch
    {
        IInstanceReferenceOperation => true,
        ILocalReferenceOperation local => IsDeclaredOutside(local.Local, lambda),
        IParameterReferenceOperation parameter => IsDeclaredOutside(parameter.Parameter, lambda),
        IMemberReferenceOperation { Instance: null } => true,
        IMemberReferenceOperation { Instance: { } inner } => IsOutsideInstance(inner, lambda),
        _ => false,
    };

    private static bool IsDeclaredOutside(ISymbol symbol, SyntaxNode lambda) =>
        symbol.DeclaringSyntaxReferences.All(reference =>
            reference.SyntaxTree != lambda.SyntaxTree || !lambda.Span.Contains(reference.Span));

    private static IOperation UnwrapDelegateTarget(IOperation operation)
    {
        IOperation current = operation;
        while (true)
        {
            switch (current)
            {
                case IConversionOperation conversion:
                    current = conversion.Operand;
                    continue;
                case IDelegateCreationOperation delegateCreation:
                    current = delegateCreation.Target;
                    continue;
                default:
                    return current;
            }
        }
    }

    private static bool IsCancellationToken(ITypeSymbol? type) =>
        type is INamedTypeSymbol
        {
            Name: "CancellationToken",
            ContainingNamespace.Name: "Threading",
            ContainingNamespace.ContainingNamespace.Name: "System",
        };
}
