# Feature Specification: Assembly Boundary Rationalization

**Issue**: FS.GG.Governance#308
**Tier**: 2 — internal architecture; public namespaces and signatures remain stable

## Problem

Governance has 84 source projects. Some assemblies enforce purity, package ownership, or deployment
boundaries, while others only separate code that is restored, referenced, tested, and changed
together. Without an explicit classification, low-value seams and load-bearing boundaries look alike.

## User stories

### US1 — Review the real dependency graph

As a maintainer, I can regenerate the tracked source-project graph and see every assembly classified as
`security-or-purity`, `packaging`, or `organizational`.

**Acceptance**

- The graph is derived from tracked `.fsproj` files, not a hand-written substitute.
- Every source project has exactly one reviewed classification.
- Graph drift fails a test until the artifact is intentionally regenerated.

### US2 — Remove one low-value seam

As a maintainer, I pay for one built-in adapter assembly instead of two when those adapters have the
same dependency and production-consumer sets.

**Acceptance**

- SpecKit and DesignSystem remain separate public namespaces with their curated `.fsi` contracts.
- Their code shares one non-packable `Adapters.BuiltIn` assembly.
- The separate `Adapters.SpecKit` and `Adapters.DesignSystem` projects are absent.
- The `Adapters.Spi` security/purity boundary and all other classified boundaries remain.
- Existing adapter and dependency-fence tests pass.

## Non-goals

- No public API or behavior change.
- No merge of package-producing projects, effect owners, executables, SPI, or executable-leaf helpers.
- No new production dependency.

## Success criteria

- Source project count falls from 84 to 83.
- Every source assembly is classified and the committed DOT graph matches the real graph.
- The full solution builds and tests successfully.
