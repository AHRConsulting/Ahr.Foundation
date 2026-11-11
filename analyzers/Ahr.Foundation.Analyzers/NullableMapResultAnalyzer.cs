using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ahr.Foundation.Analyzers;

/// <summary>Reports a <c>Map</c>, <c>MapAsync</c>, <c>MapSome</c>, <c>MapSomeAsync</c>, <c>Select</c>, <c>Try</c>, or <c>TryAsync</c> call whose function produces a nullable value, which throws at runtime when it is null.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullableMapResultAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier for a mapping that produces a nullable success value.</summary>
    public const string DiagnosticId = "AHRF006";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Do not produce a nullable success value",
        "'{0}' produces nullable '{1}', which throws when the value is null; {2}",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Some and Success never hold null, so Map, MapAsync, MapSome, MapSomeAsync, Select, Try, and TryAsync throw ArgumentNullException when the function returns null. When the value may legitimately be absent, use Bind and convert it explicitly, for example option.Bind(x => x.MiddleName.ToOption()), return a Failure that explains why the value is missing, or, for Try and TryAsync, return an Option from the operation, for example Result.Try(() => Find(id).ToOption()).");

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

        if (method.Name is not ("Map" or "MapAsync" or "MapSome" or "MapSomeAsync" or "Select" or "Try" or "TryAsync") ||
            method.ContainingType?.ContainingNamespace.ToDisplayString() != "Ahr.Foundation")
        {
            return;
        }

        if (UnwrapAwaitable(method.ReturnType) is not INamedTypeSymbol
            {
                Name: "Option" or "Result",
                Arity: 1 or 2,
                ContainingNamespace: { Name: "Foundation", ContainingNamespace.Name: "Ahr" },
            } mapped)
        {
            return;
        }

        ITypeSymbol value = mapped.TypeArguments[0];
        if (method.Name is "MapSome" or "MapSomeAsync")
        {
            if (value is not INamedTypeSymbol { Name: "Option", Arity: 1 } option)
            {
                return;
            }

            value = option.TypeArguments[0];
        }

        if (!IsNullable(value))
        {
            return;
        }

        IArgumentOperation? mapping = invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Type.TypeKind == TypeKind.Delegate);
        context.ReportDiagnostic(Diagnostic.Create(
            _rule,
            (mapping?.Syntax ?? invocation.Syntax).GetLocation(),
            method.Name,
            value.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            method.Name is "Try" or "TryAsync"
                ? "return an Option from the operation instead, for example with ToOption()"
                : "use Bind to return None (for example with ToOption()) or an explicit Failure instead"));
    }

    private static ITypeSymbol UnwrapAwaitable(ITypeSymbol type) =>
        type is INamedTypeSymbol
        {
            Name: "Task" or "ValueTask",
            Arity: 1,
            ContainingNamespace.Name: "Tasks",
            ContainingNamespace.ContainingNamespace.Name: "Threading",
            ContainingNamespace.ContainingNamespace.ContainingNamespace.Name: "System",
        } awaitable
            ? awaitable.TypeArguments[0]
            : type;

    private static bool IsNullable(ITypeSymbol type) =>
        type.NullableAnnotation == NullableAnnotation.Annotated ||
        type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
}
