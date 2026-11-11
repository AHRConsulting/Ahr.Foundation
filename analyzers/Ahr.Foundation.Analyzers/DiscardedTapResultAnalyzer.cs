using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ahr.Foundation.Analyzers;

/// <summary>Reports a <c>Tap</c>, <c>TapAsync</c>, <c>TapSome</c>, or <c>TapSomeAsync</c> function that discards a result or option, which silently ignores a failure.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DiscardedTapResultAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier for a Tap function that discards a result or option.</summary>
    public const string DiagnosticId = "AHRF008";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Do not discard a result or option in Tap",
        "'{0}' discards the '{1}' returned here, so a failure is ignored; return a result of the same family, use Bind, or assign it to '_' to ignore it deliberately",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A Tap function that returns a result or option of the same family stops the pipeline when it fails. A function that returns the other family, a different error type, or nothing because its block has no return statement discards the value instead, so a failure goes unnoticed. Return a value of the same family, convert it, use Bind, or write '_ = operation();' to ignore it deliberately.");

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

        if (method.Name is not ("Tap" or "TapAsync" or "TapSome" or "TapSomeAsync") ||
            method.ContainingType?.ContainingNamespace.ToDisplayString() != "Ahr.Foundation")
        {
            return;
        }

        IArgumentOperation? action = invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Type.TypeKind == TypeKind.Delegate);
        if (action?.Parameter?.Type is not INamedTypeSymbol { DelegateInvokeMethod: { } invoke } ||
            !ReturnsNoValue(invoke.ReturnType))
        {
            return;
        }

        IOperation target = UnwrapDelegateTarget(action.Value);
        if (target is IAnonymousFunctionOperation function)
        {
            ReportDiscards(context, method, function.Body);
        }
        else if (target is IMethodReferenceOperation reference && IsResultOrOption(reference.Method.ReturnType))
        {
            Report(context, method, reference.Syntax, reference.Method.ReturnType);
        }
    }

    private static void ReportDiscards(OperationAnalysisContext context, IMethodSymbol method, IOperation operation)
    {
        foreach (IOperation child in operation.Children)
        {
            if (child is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                continue;
            }

            if (child is IExpressionStatementOperation { Operation: not ISimpleAssignmentOperation, Operation.Type: { } type } statement && IsResultOrOption(type))
            {
                Report(context, method, statement.Operation.Syntax, type);
            }
            else if (child is IReturnOperation { ReturnedValue: { } returned } && IsResultOrOption(Unconvert(returned).Type))
            {
                Report(context, method, returned.Syntax, Unconvert(returned).Type!);
            }
            else
            {
                ReportDiscards(context, method, child);
            }
        }
    }

    private static void Report(OperationAnalysisContext context, IMethodSymbol method, SyntaxNode syntax, ITypeSymbol type) =>
        context.ReportDiagnostic(Diagnostic.Create(
            _rule,
            syntax.GetLocation(),
            method.Name,
            type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));

    private static IOperation Unconvert(IOperation operation) =>
        operation is IConversionOperation conversion ? conversion.Operand : operation;

    private static bool ReturnsNoValue(ITypeSymbol type) =>
        type.SpecialType == SpecialType.System_Void ||
        (type is INamedTypeSymbol { Name: "Task", Arity: 0 } && IsThreadingTasks(type));

    private static bool IsResultOrOption(ITypeSymbol? type)
    {
        ITypeSymbol? unwrapped = type is INamedTypeSymbol { Name: "Task" or "ValueTask", Arity: 1 } awaitable && IsThreadingTasks(type)
            ? awaitable.TypeArguments[0]
            : type;
        return unwrapped is INamedTypeSymbol
        {
            Name: "Result" or "Option",
            ContainingNamespace: { Name: "Foundation", ContainingNamespace.Name: "Ahr" },
        };
    }

    private static bool IsThreadingTasks(ITypeSymbol type) =>
        type.ContainingNamespace is
        {
            Name: "Tasks",
            ContainingNamespace.Name: "Threading",
            ContainingNamespace.ContainingNamespace.Name: "System",
        };

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
}
