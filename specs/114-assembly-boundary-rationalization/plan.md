# Implementation Plan

## Constitution check

- **Public contracts**: preserve the existing SpecKit and DesignSystem namespaces and `.fsi` files.
- **Dependency direction**: retain `Adapters.Spi` as the only dependency of the combined built-in
  adapter assembly; kernel and SPI remain upstream.
- **Purity**: move files without changing their pure implementations.
- **Evidence**: parse tracked project files, classify the real graph, and commit its deterministic DOT
  projection.
- **Scope**: Tier 2 internal refactor; no package or public surface baseline changes.

## Design

1. Replace the two non-packable sibling adapter projects with one non-packable
   `FS.GG.Governance.Adapters.BuiltIn` project.
2. Preserve source namespaces and compile signatures before implementations.
3. Replace pairs of production/test project references with the one built-in reference.
4. Extend the existing dependency-fence graph with explicit boundary classifications.
5. Add a deterministic Graphviz projection and tests for classification completeness and seam removal.

## Verification

- Adapter SpecKit and DesignSystem test projects.
- Dependency-fence tests.
- Full `dotnet build FS.GG.Governance.sln` and `dotnet test FS.GG.Governance.sln`.
- Coordination path verification and current-head PR checks.
