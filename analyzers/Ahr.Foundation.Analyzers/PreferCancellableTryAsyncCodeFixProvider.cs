using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ahr.Foundation.Analyzers;

/// <summary>Provides a code fix that moves a captured cancellation token into the cancellation-aware <c>Result.TryAsync</c> overload (AHRF007).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PreferCancellableTryAsyncCodeFixProvider))]
[Shared]
public sealed class PreferCancellableTryAsyncCodeFixProvider : CodeFixProvider
{
    private const string Title = "Pass the cancellation token to TryAsync";

    private static readonly string[] _parameterNames = ["token", "cancellationToken", "ct"];

    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(PreferCancellableTryAsyncAnalyzer.DiagnosticId);

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        Diagnostic diagnostic = context.Diagnostics[0];
        if (!diagnostic.Properties.ContainsKey(PreferCancellableTryAsyncAnalyzer.FixableProperty))
        {
            return;
        }

        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root?.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 0 } lambda ||
            lambda.Parent is not ArgumentSyntax { Parent: ArgumentListSyntax argumentList })
        {
            return;
        }

        var tokenReferences = diagnostic.AdditionalLocations
            .Select(location => root.FindNode(location.SourceSpan, getInnermostNodeForTie: true))
            .OfType<ExpressionSyntax>()
            .ToList();
        if (tokenReferences.Count == 0)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                cancellationToken => PassTokenAsync(context.Document, root, lambda, argumentList, tokenReferences, cancellationToken),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> PassTokenAsync(
        Document document,
        SyntaxNode root,
        ParenthesizedLambdaExpressionSyntax lambda,
        ArgumentListSyntax argumentList,
        List<ExpressionSyntax> tokenReferences,
        CancellationToken cancellationToken)
    {
        SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var parameterName = ChooseParameterName(semanticModel, lambda);

        ExpressionSyntax tokenArgument = tokenReferences[0].WithoutTrivia();
        ParenthesizedLambdaExpressionSyntax rewritten = lambda.ReplaceNodes(
            tokenReferences,
            (original, _) => SyntaxFactory.IdentifierName(parameterName).WithTriviaFrom(original));

        ParameterSyntax parameter = SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName));
        AnonymousFunctionExpressionSyntax replacementLambda = rewritten.ReturnType is null && rewritten.AttributeLists.Count == 0
            ? SyntaxFactory.SimpleLambdaExpression(rewritten.Modifiers, parameter, rewritten.Block, rewritten.ExpressionBody)
                .WithArrowToken(rewritten.ArrowToken)
            : rewritten.WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SingletonSeparatedList(parameter)));

        ArgumentSyntax newArgument = SyntaxFactory.Argument(tokenArgument);
        if (argumentList.Arguments.Any(argument => argument.NameColon is not null))
        {
            newArgument = newArgument.WithNameColon(SyntaxFactory.NameColon("cancellationToken"));
        }

        ArgumentListSyntax newArgumentList = argumentList
            .ReplaceNode(lambda, replacementLambda.WithTriviaFrom(lambda))
            .AddArguments(newArgument);

        return document.WithSyntaxRoot(root.ReplaceNode(argumentList, newArgumentList));
    }

    private static string ChooseParameterName(SemanticModel? semanticModel, ParenthesizedLambdaExpressionSyntax lambda)
    {
        var usedInside = new HashSet<string>(
            lambda.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)).Select(token => token.ValueText),
            StringComparer.Ordinal);

        bool IsFree(string name) =>
            !usedInside.Contains(name) &&
            (semanticModel is null || semanticModel.LookupSymbols(lambda.SpanStart, name: name).IsEmpty);

        var preferred = _parameterNames.FirstOrDefault(IsFree);
        if (preferred is not null)
        {
            return preferred;
        }

        var suffix = 1;
        while (!IsFree($"token{suffix}"))
        {
            suffix++;
        }

        return $"token{suffix}";
    }
}
