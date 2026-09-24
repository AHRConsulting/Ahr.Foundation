# Contributing

1. Discuss substantial API changes in an issue before implementation.
2. Keep public API changes compatible where practical and document every breaking change clearly. During `0.x`, breaking changes require a minor-version increase.
3. Add or update tests for every behavior change:
   - New functionality requires focused positive, negative, and relevant boundary tests.
   - Bug fixes require a regression test that fails without the fix.
   - Analyzer changes require diagnostic tests for matching and non-matching code; code-fix changes
     also require fixed-code and Fix All coverage where applicable.
   - Public API changes require tests for documented invariants and composition behavior.
   - Documentation-only changes do not require new unit tests.
4. Keep tests deterministic, independent, and focused on observable behavior. Use xUnit v3 native
   assertions and the `Method_Scenario_ExpectedBehavior` naming convention. Do not weaken or delete
   coverage solely to make a change pass.
5. Run format verification, Release build, all tests, and package inspection:
   ```bash
   dotnet restore Ahr.Foundation.slnx --locked-mode
   dotnet format Ahr.Foundation.slnx --verify-no-changes --no-restore
   dotnet build Ahr.Foundation.slnx -c Release --no-restore
   dotnet test --solution Ahr.Foundation.slnx -c Release --no-build
   dotnet pack src/Ahr.Foundation/Ahr.Foundation.csproj -c Release --no-build -p:AhrFoundationVersion=0.1.0
   scripts/verify-package.sh 0.1.0
   ```
6. Ensure zero external runtime dependencies are introduced to `src/Ahr.Foundation`.
7. Update `CHANGELOG.md`, documentation, samples, and public API baseline files
   (`PublicAPI.Unshipped.txt` / `PublicAPI.Shipped.txt`) where applicable.

To preview the documentation site locally:

```bash
dotnet tool update -g docfx
docfx metadata && docfx build
docfx serve _site
```

Contributions require acceptance by the repository maintainers. No contributor license or copyright terms are implied by this document.
