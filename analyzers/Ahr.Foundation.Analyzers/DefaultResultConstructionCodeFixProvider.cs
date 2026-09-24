using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ahr.Foundation.Analyzers;

/// <summary>Provides code fixes for default result construction (AHRF001).</summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DefaultResultConstructionCodeFixProvider))]
[Shared]
public sealed class DefaultResultConstructionCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public override ImmutableArray<string> FixableDiagnosticIds => [DefaultResultConstructionAnalyzer.DiagnosticId];

    /// <inheritdoc />
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        Diagnostic diagnostic = context.Diagnostics[0];
        SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        if (node is null)
        {
            return;
        }

        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        TypeInfo typeInfo = semanticModel.GetTypeInfo(node, context.CancellationToken);
        ITypeSymbol? typeSymbol = typeInfo.Type ?? typeInfo.ConvertedType;

        if (typeSymbol is not INamedTypeSymbol named ||
            named.ContainingNamespace?.ToDisplayString() != "Ahr.Foundation" ||
            named.Name != "Result")
        {
            return;
        }

        // Only provide automated code fixes for Result and Result<T> where Success can be inferred or defaulted
        if (named.Arity == 0)
        {
            // Result -> Result.Success()
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Use 'Result.Success()'",
                    createChangedDocument: _ => ReplaceWithSuccess(context.Document, root, node, "Result.Success()"),
                    equivalenceKey: "UseResultSuccess"),
                diagnostic);
        }
    }

    private static Task<Document> ReplaceWithSuccess(
        Document document,
        SyntaxNode root,
        SyntaxNode oldNode,
        string replacementCode)
    {
        ExpressionSyntax replacementSyntax = SyntaxFactory.ParseExpression(replacementCode)
            .WithTriviaFrom(oldNode);

        SyntaxNode newRoot = root.ReplaceNode(oldNode, replacementSyntax);
        return Task.FromResult(document.WithSyntaxRoot(newRoot));
    }
}
