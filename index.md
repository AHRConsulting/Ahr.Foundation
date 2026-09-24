# Ahr.Foundation

<p class="landing-logo">
  <img src="assets/ahr.foundation.logo.svg" alt="Ahr.Foundation logo">
</p>

Welcome to the **Ahr.Foundation** documentation.

`Ahr.Foundation` provides platform-neutral `Result` and `Option` primitives for .NET, letting you model failure and absence explicitly instead of throwing exceptions for control flow. Bundled Roslyn analyzers catch invalid result construction and unsafe exception adapters at build time.

## Key Features

- **Platform Neutral**: Pure C# library targeting `.NET Standard 2.0` and `.NET 10.0+` with zero external runtime dependencies.
- **Bundled Analyzers**: [Four Roslyn rules](docs/analyzers.md) protect explicit result construction and safe exception-to-result adaptation.
- **Fluent Async Composition**: `TaskCompositionExtensions` chains asynchronous steps without nested awaits or intermediate null checks.
- **LINQ Query Syntax**: `Select`/`SelectMany` aliases let you compose dependent steps with `from ... select`, short-circuiting on the first failure.
- **API Tracked**: Public API changes are checked with `Microsoft.CodeAnalysis.PublicApiAnalyzers` and documented under the `0.x` versioning policy.

## Concepts

Operations compose as two tracks — **Success** and **Failure** — a pattern commonly called
Railway-Oriented Programming. `Bind` continues on the success track and diverts to the failure
track at the first error, skipping every later step. A pipeline therefore reads as a straight line
while still handling every error path.

## Getting Started

Install the package via NuGet:

```bash
dotnet add package Ahr.Foundation --version 0.1.0
```

`0.1.0` is a stable package. While the project remains on major version zero, minor releases may contain documented breaking changes based on consumer feedback.
