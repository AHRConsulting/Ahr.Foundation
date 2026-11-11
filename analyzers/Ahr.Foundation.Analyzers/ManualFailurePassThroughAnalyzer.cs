using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ahr.Foundation.Analyzers;

/// <summary>Reports a failure passed on unchanged by hand, which <c>Bind</c> or <c>BindAsync</c> already does.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ManualFailurePassThroughAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier for a failure passed on by hand.</summary>
    public const string DiagnosticId = "AHRF011";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Pass a failure on with Bind",
        "'{0}' passes its failure on by hand; use Bind or BindAsync instead",
        "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Checking IsFailure and returning Failure(result.Error) unchanged, directly or wrapped in Task.FromResult, repeats what Bind and BindAsync do. A failure that is changed, logged, or combined with others is not reported, nor is a check inside a loop or a try, using, or lock block.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeConditional, OperationKind.Conditional);
    }

    private static void AnalyzeConditional(OperationAnalysisContext context)
    {
        var conditional = (IConditionalOperation)context.Operation;
        if (!TryGetCheck(conditional.Condition, out ISymbol checkedResult, out var failureWhenTrue) ||
            IsInsideGuardedBlock(conditional, stopAtLoop: conditional.Type is null))
        {
            return;
        }

        var reported = conditional.Type is not null
            ? IsConditionalPassThrough(conditional, checkedResult, failureWhenTrue)
            : failureWhenTrue && conditional.WhenFalse is null && IsReturnedPassThrough(conditional.WhenTrue, checkedResult);

        if (reported)
        {
            context.ReportDiagnostic(Diagnostic.Create(_rule, conditional.Syntax.GetLocation(), checkedResult.Name));
        }
    }

    // Accepts x.IsFailure, !x.IsSuccess, x.IsSuccess, and !x.IsFailure on a local or parameter holding a result.
    private static bool TryGetCheck(IOperation condition, out ISymbol checkedResult, out bool failureWhenTrue)
    {
        checkedResult = null!;
        failureWhenTrue = false;

        var negated = false;
        if (condition is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not } not)
        {
            negated = true;
            condition = not.Operand;
        }

        if (condition is not IPropertyReferenceOperation { Property.Name: "IsFailure" or "IsSuccess" } check ||
            !IsResult(check.Instance?.Type) ||
            ReferencedSymbol(check.Instance) is not { } symbol)
        {
            return false;
        }

        checkedResult = symbol;
        failureWhenTrue = check.Property.Name == "IsFailure" != negated;
        return true;
    }

    private static bool IsConditionalPassThrough(IConditionalOperation conditional, ISymbol checkedResult, bool failureWhenTrue)
    {
        IOperation? failureBranch = failureWhenTrue ? conditional.WhenTrue : conditional.WhenFalse;
        return failureBranch is not null && IsPassThrough(failureBranch, checkedResult);
    }

    private static bool IsReturnedPassThrough(IOperation branch, ISymbol checkedResult)
    {
        if (branch is IBlockOperation { Operations.Length: 1 } block)
        {
            branch = block.Operations[0];
        }

        return branch is IReturnOperation { ReturnedValue: { } returned } && IsPassThrough(returned, checkedResult);
    }

    // Also matches Task.FromResult(Failure(x.Error)) and ValueTask.FromResult(...), which BindAsync replaces.
    private static bool IsPassThrough(IOperation operation, ISymbol checkedResult) =>
        IsDirectPassThrough(operation, checkedResult) ||
        (Unconvert(operation) is IInvocationOperation
        {
            TargetMethod: { Name: "FromResult", IsStatic: true, ContainingType: { Name: "Task" or "ValueTask" } taskType },
            Arguments.Length: 1,
        } wrapped &&
        taskType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" &&
        IsDirectPassThrough(wrapped.Arguments[0].Value, checkedResult));

    // Matches Failure(x.Error) on a result of the same family, so Bind keeps the same error type.
    private static bool IsDirectPassThrough(IOperation operation, ISymbol checkedResult) =>
        Unconvert(operation) is IInvocationOperation
        {
            TargetMethod: { Name: "Failure", IsStatic: true, ContainingType: { } failureType },
            Arguments.Length: 1,
        } failure &&
        IsResult(failureType) &&
        Unconvert(failure.Arguments[0].Value) is IPropertyReferenceOperation { Property.Name: "Error", Instance: { } errorOwner } &&
        SymbolEqualityComparer.Default.Equals(ReferencedSymbol(errorOwner), checkedResult) &&
        errorOwner.Type is INamedTypeSymbol checkedType &&
        HasSameErrorType(failureType, checkedType);

    private static bool HasSameErrorType(INamedTypeSymbol failureType, INamedTypeSymbol checkedType) =>
        checkedType.Arity == 2
            ? failureType.Arity == 2 && SymbolEqualityComparer.Default.Equals(failureType.TypeArguments[1], checkedType.TypeArguments[1])
            : failureType.Arity < 2;

    private static bool IsInsideGuardedBlock(IOperation operation, bool stopAtLoop)
    {
        for (IOperation? current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation or IMethodBodyOperation)
            {
                return false;
            }

            if (current is ITryOperation or IUsingOperation or ILockOperation || (stopAtLoop && current is ILoopOperation))
            {
                return true;
            }
        }

        return false;
    }

    private static ISymbol? ReferencedSymbol(IOperation? operation) => Unconvert(operation) switch
    {
        ILocalReferenceOperation local => local.Local,
        IParameterReferenceOperation parameter => parameter.Parameter,
        _ => null,
    };

    private static IOperation? Unconvert(IOperation? operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
        {
            operation = conversion.Operand;
        }

        return operation;
    }

    private static bool IsResult(ITypeSymbol? type) =>
        type is INamedTypeSymbol
        {
            Name: "Result",
            ContainingNamespace: { Name: "Foundation", ContainingNamespace.Name: "Ahr" },
        };
}
