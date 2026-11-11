using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ahr.Foundation.Analyzers;

/// <summary>Reports a statement that drops a <c>Result</c> or an awaitable of one, which silently loses a failure.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiscardedResultAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier for a statement that drops a result.</summary>
    public const string DiagnosticId = "AHRF010";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Do not discard a result",
        "This statement discards the '{0}', so a failure is lost; handle it, return it, end the chain with TapError, or assign it to '_' to ignore it deliberately",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A Result is often the only signal that an operation failed, so a statement that drops one loses the failure, even when the call is awaited. Handle or return the result, end the chain with TapError or TapErrorAsync, or write '_ = operation();' to ignore it deliberately.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeStatement, OperationKind.ExpressionStatement);
    }

    private static void AnalyzeStatement(OperationAnalysisContext context)
    {
        var statement = (IExpressionStatementOperation)context.Operation;
        IOperation expression = statement.Operation;

        if (expression is not (IInvocationOperation or IAwaitOperation or IConditionalAccessOperation) ||
            !IsResult(expression.Type) ||
            EndsWithFailureHandler(expression) ||
            IsReportedByTapRule(statement))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            _rule,
            expression.Syntax.GetLocation(),
            expression.Type!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static bool IsResult(ITypeSymbol? type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
        {
            type = nullable.TypeArguments[0];
        }

        if (type is INamedTypeSymbol { Name: "Task" or "ValueTask", Arity: 1 } awaitable && IsThreadingTasks(type))
        {
            type = awaitable.TypeArguments[0];
        }

        return type is INamedTypeSymbol
        {
            Name: "Result",
            ContainingNamespace: { Name: "Foundation", ContainingNamespace.Name: "Ahr" },
        };
    }

    // TapError and a Match with actions have already seen the failure. Their unawaited async forms may not have,
    // so they stay reported.
    private static bool EndsWithFailureHandler(IOperation expression)
    {
        var awaited = false;
        if (expression is IAwaitOperation awaitOperation)
        {
            awaited = true;
            expression = awaitOperation.Operation;
            if (expression is IInvocationOperation { TargetMethod.Name: "ConfigureAwait", Instance: { } configured })
            {
                expression = configured;
            }
        }

        while (expression is IConditionalAccessOperation conditional)
        {
            expression = conditional.WhenNotNull;
        }

        if (expression is not IInvocationOperation { TargetMethod: { } method } ||
            method.ContainingType?.ContainingNamespace.ToDisplayString() != "Ahr.Foundation")
        {
            return false;
        }

        if (method.Name == (awaited ? "TapErrorAsync" : "TapError"))
        {
            return true;
        }

        // A Match whose functions return a value hands back that value, so a result returned there is still checked.
        return method.Name == (awaited ? "MatchAsync" : "Match") &&
            method.Parameters.All(parameter =>
                parameter.Type is not INamedTypeSymbol { DelegateInvokeMethod: { } invoke } ||
                ReturnsNoValue(invoke.ReturnType));
    }

    // AHRF008 already reports this drop with Tap-specific advice, so it is not reported twice.
    private static bool IsReportedByTapRule(IOperation statement)
    {
        IOperation? current = statement.Parent;
        while (current is not null and not IAnonymousFunctionOperation and not ILocalFunctionOperation)
        {
            current = current.Parent;
        }

        if (current is not IAnonymousFunctionOperation)
        {
            return false;
        }

        IOperation? parent = current.Parent;
        while (parent is IConversionOperation or IDelegateCreationOperation)
        {
            parent = parent.Parent;
        }

        return parent is IArgumentOperation
        {
            Parameter.Type: INamedTypeSymbol { DelegateInvokeMethod: { } invoke },
            Parent: IInvocationOperation { TargetMethod: { Name: "Tap" or "TapAsync" or "TapSome" or "TapSomeAsync" } method },
        } &&
            method.ContainingType?.ContainingNamespace.ToDisplayString() == "Ahr.Foundation" &&
            ReturnsNoValue(invoke.ReturnType);
    }

    private static bool ReturnsNoValue(ITypeSymbol type) =>
        type.SpecialType == SpecialType.System_Void ||
        (type is INamedTypeSymbol { Name: "Task", Arity: 0 } && IsThreadingTasks(type));

    private static bool IsThreadingTasks(ITypeSymbol type) =>
        type.ContainingNamespace is
        {
            Name: "Tasks",
            ContainingNamespace.Name: "Threading",
            ContainingNamespace.ContainingNamespace.Name: "System",
        };
}
