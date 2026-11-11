using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Ahr.Foundation.Analyzers;

/// <summary>Suggests <c>Result.Try</c>/<c>Result.TryAsync</c> instead of a manual try/catch that constructs a Result failure.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferResultTryAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier for a manual try/catch that could use a Try helper instead.</summary>
    public const string DiagnosticId = "AHRF002";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Prefer Result.Try or Result.TryAsync over a manual try/catch",
        "Use 'Result.Try' or 'Result.TryAsync' instead of a manual try/catch that constructs a '{0}' failure{1}",
        "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "A try block that only produces a Success and a catch block that only produces a Failure duplicates the behavior of Result.Try/Result.TryAsync. A finally block stays: put it in a try/finally around the call, or use a using declaration inside the operation.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeTryStatement, SyntaxKind.TryStatement);
    }

    private static void AnalyzeTryStatement(SyntaxNodeAnalysisContext context)
    {
        var tryStatement = (TryStatementSyntax)context.Node;
        if (tryStatement.Catches.Count == 0)
        {
            return;
        }

        SemanticModel model = context.SemanticModel;
        CancellationToken cancellationToken = context.CancellationToken;

        INamedTypeSymbol? exceptionType = model.Compilation.GetTypeByMetadataName("System.Exception");
        INamedTypeSymbol? canceledType = model.Compilation.GetTypeByMetadataName("System.OperationCanceledException");

        // Result.Try catches every exception except cancellation, which it rethrows. Only a catch-all that builds
        // the failure matches that; handling cancellation or one specific exception type does not.
        string? resultTypeName = null;
        foreach (CatchClauseSyntax catchClause in tryStatement.Catches)
        {
            if (IsPureRethrow(catchClause))
            {
                continue;
            }

            ITypeSymbol? caught = catchClause.Declaration is { } declaration
                ? model.GetTypeInfo(declaration.Type, cancellationToken).Type
                : exceptionType;
            if (InheritsFrom(caught, canceledType))
            {
                return;
            }

            if (catchClause.Filter is null && SymbolEqualityComparer.Default.Equals(caught, exceptionType))
            {
                resultTypeName ??= FindResultMemberCall(catchClause, tryStatement, model, "Failure", cancellationToken);
            }
        }

        if (resultTypeName is null)
        {
            return;
        }

        if (FindResultMemberCall(tryStatement.Block, tryStatement, model, "Success", cancellationToken) is null)
        {
            return;
        }

        var finallyHint = tryStatement.Finally is null ? string.Empty : "; keep the finally block in a try/finally around the call";
        context.ReportDiagnostic(Diagnostic.Create(_rule, tryStatement.TryKeyword.GetLocation(), resultTypeName, finallyHint));
    }

    private static bool InheritsFrom(ITypeSymbol? type, INamedTypeSymbol? baseType)
    {
        for (ITypeSymbol? current = type; current is not null && baseType is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPureRethrow(CatchClauseSyntax catchClause)
    {
        SyntaxList<StatementSyntax> statements = catchClause.Block.Statements;
        return statements.Count == 1 && statements[0] is ThrowStatementSyntax { Expression: null };
    }

    // Only considers invocations directly owned by the given try statement, so a nested try/catch is analyzed independently.
    private static string? FindResultMemberCall(SyntaxNode container, TryStatementSyntax boundary, SemanticModel model, string memberName, CancellationToken cancellationToken)
    {
        foreach (InvocationExpressionSyntax invocation in container.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            TryStatementSyntax? nearestTry = invocation.FirstAncestorOrSelf<TryStatementSyntax>();
            if (nearestTry is not null && nearestTry != boundary)
            {
                continue;
            }

            if (model.GetSymbolInfo(invocation, cancellationToken).Symbol is IMethodSymbol { Name: var name } method &&
                name == memberName &&
                method.ContainingType is INamedTypeSymbol { Name: "Result" } named &&
                named.ContainingNamespace.ToDisplayString() == "Ahr.Foundation" &&
                named.Arity is >= 0 and <= 2)
            {
                return named.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            }
        }

        return null;
    }
}
