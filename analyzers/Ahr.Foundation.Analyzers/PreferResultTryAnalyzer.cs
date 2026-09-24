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
        "Use 'Result.Try' or 'Result.TryAsync' instead of a manual try/catch that constructs a '{0}' failure",
        "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "A try block that only produces a Success and a catch block that only produces a Failure duplicates the behavior of Result.Try/Result.TryAsync.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [_rule];

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

        string? resultTypeName = null;
        foreach (CatchClauseSyntax catchClause in tryStatement.Catches)
        {
            if (IsPureRethrow(catchClause))
            {
                continue;
            }

            resultTypeName ??= FindResultMemberCall(catchClause, tryStatement, model, "Failure", cancellationToken);
            if (resultTypeName is not null)
            {
                break;
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

        context.ReportDiagnostic(Diagnostic.Create(_rule, tryStatement.TryKeyword.GetLocation(), resultTypeName));
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
