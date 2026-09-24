using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ahr.Foundation.Analyzers;

/// <summary>Reports a synchronous <c>Result.Try</c> overload invoked with an operation that returns an awaitable, which is never awaited.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferResultTryAsyncAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier for an awaitable operation passed to a synchronous Try overload.</summary>
    public const string DiagnosticId = "AHRF003";

    /// <summary>The identifier for an async void delegate passed to a synchronous Try overload.</summary>
    public const string AsyncVoidDiagnosticId = "AHRF004";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Prefer Result.TryAsync when the operation is awaitable",
        "The operation passed to '{0}' returns '{1}'; use Result.TryAsync so it is awaited instead of discarded or wrapped unobserved",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Result.Try's synchronous overloads only catch exceptions thrown before the first await. Passing a Task/ValueTask-returning delegate either discards the task (Try(Action)) or wraps it unawaited inside the Result (Try<T>/Try<T, TError>), silently hiding faults that occur once the operation actually runs. Use Result.TryAsync instead.");

    private static readonly DiagnosticDescriptor _asyncVoidRule = new(
        AsyncVoidDiagnosticId,
        "Do not pass an async lambda to Result.Try",
        "The operation passed to '{0}' is an async lambda, which compiles to an async void delegate; exceptions thrown after the first await will be unobserved instead of becoming a Failure. Use Result.TryAsync instead.",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An async lambda explicitly targeting Action (e.g. Result.Try((Action)(async () => await op()))) compiles to an async void delegate. Async void methods cannot be awaited or observed by the caller: any exception thrown after the first await escapes to the SynchronizationContext/unobserved-exception handler instead of being caught by Try, which can crash the process. Use Result.TryAsync instead so the operation is properly awaited. Note: an un-cast async lambda passed directly to Result.Try (e.g. Result.Try(async () => await op())) instead resolves to Try<T>(Func<T>) - since the compiler prefers a Task-returning conversion over an Action one - and is reported as AHRF003, not AHRF004.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_rule, _asyncVoidRule];

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

        if (method.Name != "Try" ||
            method.ContainingType is not { Name: "Result", Arity: 0 } containingType ||
            containingType.ContainingNamespace.ToDisplayString() != "Ahr.Foundation")
        {
            return;
        }

        // Try<T>(Func<T> operation) and Try<T, TError>(Func<T> operation, Func<Exception, TError> errorFactory):
        // if T itself is Task/Task<T>/ValueTask/ValueTask<T>, the operation's result is wrapped unawaited.
        if (method.Arity > 0)
        {
            if (AsAwaitableType(method.TypeArguments[0]) is { } awaitableTypeArgument)
            {
                Report(context, method, awaitableTypeArgument);
            }

            return;
        }

        // Try(Action operation): an awaitable-returning expression can still be converted to Action, discarding its result.
        if (invocation.Arguments.Length == 0)
        {
            return;
        }

        IOperation target = UnwrapDelegateTarget(invocation.Arguments[0].Value);

        // An async lambda with no return value (e.g. async () => await FetchAsync()) converts to Action as
        // async void. This is strictly worse than a discarded Task: exceptions thrown after the first await
        // are unobserved by the caller entirely, rather than merely living on an un-awaited Task.
        if (target is IAnonymousFunctionOperation { Symbol.IsAsync: true })
        {
            ReportAsyncVoid(context, method);
            return;
        }

        if (GetDiscardedAwaitableType(target) is { } awaitableFromAction)
        {
            Report(context, method, awaitableFromAction);
        }
    }

    private static void Report(OperationAnalysisContext context, IMethodSymbol method, ITypeSymbol awaitableType)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            _rule,
            context.Operation.Syntax.GetLocation(),
            method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            awaitableType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static void ReportAsyncVoid(OperationAnalysisContext context, IMethodSymbol method)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            _asyncVoidRule,
            context.Operation.Syntax.GetLocation(),
            method.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static ITypeSymbol? GetDiscardedAwaitableType(IOperation target)
    {
        // A lambda body converted to Action still permits a discarded awaitable expression statement,
        // e.g. () => FetchAsync(). An expression-bodied lambda lowers to the expression statement
        // optionally followed by a compiler-synthesized void return, so both shapes must be accepted.
        if (target is IAnonymousFunctionOperation anonymousFunction)
        {
            ImmutableArray<IOperation> operations = anonymousFunction.Body.Operations;
            IExpressionStatementOperation? expressionStatement = operations.Length switch
            {
                1 when operations[0] is IExpressionStatementOperation single => single,
                2 when operations[0] is IExpressionStatementOperation first &&
                    operations[1] is IReturnOperation { ReturnedValue: null } => first,
                _ => null,
            };

            return expressionStatement?.Operation.Type is { } expressionType
                ? AsAwaitableType(expressionType)
                : null;
        }

        // A method group conversion to Action, e.g. Result.Try(FetchAsync) or Result.Try((Action)FetchAsync).
        // The IOperation shape for a bare method-group delegate target does not always surface as an
        // IMethodReferenceOperation, so fall back to resolving the method symbol via the semantic model.
        if (target.Syntax is not null && target.SemanticModel is { } semanticModel)
        {
            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(target.Syntax);
            var methodSymbol = symbolInfo.Symbol as IMethodSymbol;
            if (methodSymbol is null && symbolInfo.CandidateSymbols.Length == 1)
            {
                methodSymbol = symbolInfo.CandidateSymbols[0] as IMethodSymbol;
            }

            if (methodSymbol is not null)
            {
                return AsAwaitableType(methodSymbol.ReturnType);
            }
        }

        return null;
    }

    // Unwraps explicit casts and lambda/method-group delegate creation to reach the underlying function operation.
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

    private static ITypeSymbol? AsAwaitableType(ITypeSymbol? type) =>
        type is INamedTypeSymbol
        {
            Name: "Task" or "ValueTask",
            ContainingNamespace.Name: "Tasks",
            ContainingNamespace.ContainingNamespace.Name: "Threading",
            ContainingNamespace.ContainingNamespace.ContainingNamespace.Name: "System",
        }
            ? type
            : null;
}
