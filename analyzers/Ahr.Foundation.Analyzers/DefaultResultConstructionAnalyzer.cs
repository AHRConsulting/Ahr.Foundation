using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ahr.Foundation.Analyzers;

/// <summary>Reports default construction of Ahr.Foundation result values.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DefaultResultConstructionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier for default result construction.</summary>
    public const string DiagnosticId = "AHRF001";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Result values must be created explicitly",
        "Create '{0}' with its Success or Failure factory; default construction is uninitialized",
        "Correctness",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Default-constructed Result values are explicitly uninitialized and throw when inspected or composed.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeDefaultValue, OperationKind.DefaultValue);
        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
        context.RegisterOperationAction(AnalyzeArrayCreation, OperationKind.ArrayCreation);
    }

    private static void AnalyzeDefaultValue(OperationAnalysisContext context)
    {
        var operation = (IDefaultValueOperation)context.Operation;
        ReportIfResult(context, operation.Type, operation.Syntax.GetLocation());
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var operation = (IObjectCreationOperation)context.Operation;
        if (operation.Arguments.Length == 0)
        {
            ReportIfResult(context, operation.Type, operation.Syntax.GetLocation());
        }
    }

    private static void AnalyzeArrayCreation(OperationAnalysisContext context)
    {
        var operation = (IArrayCreationOperation)context.Operation;
        var arrayType = operation.Type as IArrayTypeSymbol;
        if (arrayType is not null &&
            (operation.Initializer is null || operation.Initializer.ElementValues.IsEmpty))
        {
            ReportIfResult(context, arrayType.ElementType, operation.Syntax.GetLocation());
        }
    }

    private static void ReportIfResult(OperationAnalysisContext context, ITypeSymbol? type, Location location)
    {
        if (type is not INamedTypeSymbol named ||
            named.ContainingNamespace.ToDisplayString() != "Ahr.Foundation" ||
            named.Name != "Result" ||
            named.Arity is < 0 or > 2)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(_rule, location, named.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }
}
