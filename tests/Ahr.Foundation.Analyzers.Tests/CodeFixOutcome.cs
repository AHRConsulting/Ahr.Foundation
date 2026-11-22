using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Ahr.Foundation.Analyzers.Tests;

/// <summary>What a code-fix run produced at each stage, for the scenario's Then and And steps to check.</summary>
/// <param name="Diagnostics">The analyzer diagnostics on the original project.</param>
/// <param name="ActionTitles">The titles of the fixes offered for the single diagnostic; empty unless exactly one was reported.</param>
/// <param name="Operations">The type names of the single offered fix's operations; empty unless exactly one fix was offered.</param>
/// <param name="FixedSource">The test document after the fix; null unless the fix is exactly one ApplyChangesOperation.</param>
/// <param name="DiagnosticsAfterFix">The analyzer diagnostics on the fixed project; empty when no fix was applied.</param>
internal sealed record CodeFixOutcome(
    ImmutableArray<Diagnostic> Diagnostics,
    IReadOnlyList<string> ActionTitles,
    IReadOnlyList<string> Operations,
    string? FixedSource,
    ImmutableArray<Diagnostic> DiagnosticsAfterFix);
