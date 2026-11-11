using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ahr.Foundation.Analyzers;

/// <summary>Rewrites a conditional expression that passes a failure on by hand (AHRF011) to <c>Bind</c>.</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ManualFailurePassThroughCodeFixProvider))]
[Shared]
public sealed class ManualFailurePassThroughCodeFixProvider : CodeFixProvider
{
    private static readonly string[] _parameterNames = ["value", "v", "item"];

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ManualFailurePassThroughAnalyzer.DiagnosticId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        SemanticModel? model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        Diagnostic diagnostic = context.Diagnostics[0];

        // Only the conditional expression form has a mechanical rewrite; an early return is left to the author.
        if (root?.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not ConditionalExpressionSyntax conditional ||
            model is null ||
            CreateReplacement(conditional, model, context.CancellationToken) is not { } replacement)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Use 'Bind'",
                createChangedDocument: _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(conditional, replacement))),
                equivalenceKey: "UseBind"),
            diagnostic);
    }

    private static ExpressionSyntax? CreateReplacement(ConditionalExpressionSyntax conditional, SemanticModel model, CancellationToken cancellationToken)
    {
        if (!TryReadCheck(conditional.Condition, out IdentifierNameSyntax checkedName, out var failureWhenTrue) ||
            model.GetSymbolInfo(checkedName, cancellationToken).Symbol is not { } checkedSymbol ||
            model.GetTypeInfo(checkedName, cancellationToken).Type is not INamedTypeSymbol checkedType)
        {
            return null;
        }

        ExpressionSyntax continuation = failureWhenTrue ? conditional.WhenFalse : conditional.WhenTrue;
        ExpressionSyntax failure = failureWhenTrue ? conditional.WhenTrue : conditional.WhenFalse;

        // A failure wrapped in Task.FromResult needs BindAsync, whose return type may not match; leave it to the author.
        if (failure is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "FromResult" } } ||
            continuation.DescendantNodesAndSelf().OfType<AwaitExpressionSyntax>().Any())
        {
            return null;
        }

        // Every remaining use of the checked result must be '.Value', which becomes the lambda parameter.
        List<MemberAccessExpressionSyntax> valueReads = [];
        foreach (IdentifierNameSyntax identifier in continuation.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
        {
            if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, checkedSymbol))
            {
                continue;
            }

            if (identifier.Parent is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Value" } valueRead ||
                valueRead.Expression != identifier ||
                checkedType.Arity == 0)
            {
                return null;
            }

            valueReads.Add(valueRead);
        }

        var parameter = checkedType.Arity == 0 ? null : FreeParameterName(conditional, continuation, model);
        if (checkedType.Arity > 0 && parameter is null)
        {
            return null;
        }

        ExpressionSyntax body = continuation.ReplaceNodes(valueReads, (original, _) => SyntaxFactory.IdentifierName(parameter!).WithTriviaFrom(original));
        var lambda = parameter is null ? $"() => {body.WithoutTrivia()}" : $"{parameter} => {body.WithoutTrivia()}";
        return SyntaxFactory.ParseExpression($"{checkedName.Identifier.ValueText}.Bind({lambda})").WithTriviaFrom(conditional);
    }

    private static bool TryReadCheck(ExpressionSyntax condition, out IdentifierNameSyntax checkedName, out bool failureWhenTrue)
    {
        checkedName = null!;
        var negated = false;
        if (condition is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } not)
        {
            negated = true;
            condition = not.Operand;
        }

        while (condition is ParenthesizedExpressionSyntax parenthesized)
        {
            condition = parenthesized.Expression;
        }

        if (condition is not MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax name, Name.Identifier.ValueText: "IsFailure" or "IsSuccess" } check)
        {
            failureWhenTrue = false;
            return false;
        }

        checkedName = name;
        failureWhenTrue = check.Name.Identifier.ValueText == "IsFailure" != negated;
        return true;
    }

    private static string? FreeParameterName(ConditionalExpressionSyntax conditional, ExpressionSyntax continuation, SemanticModel model)
    {
        var used = new HashSet<string>(continuation.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Select(identifier => identifier.Identifier.ValueText));
        return _parameterNames.FirstOrDefault(name =>
            !used.Contains(name) && model.LookupSymbols(conditional.SpanStart, name: name).IsEmpty);
    }
}
