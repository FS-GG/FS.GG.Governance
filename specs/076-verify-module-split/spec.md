# Feature Specification: Verify god-module split (Phase C)

**Feature Branch**: `076-verify-module-split`

**Created**: 2026-06-27

**Status**: Draft

**Input**: User description: "next item in docs/reports/2026-06-26-203146-architecture-quality-deduplication-design.md" → Phase C of the architecture/quality/de-duplication roadmap: split the two Verify god modules (`VerifyCommand/Loop.fs`, `VerifyJson.fs`) along their feature seams, with a gated decision on the semi-radical `GateRunHost` unification recorded as an ADR.

## Overview

This is an internal maintainability refactor — the fourth scheduled phase (Phase C) of the architecture/quality/de-duplication roadmap, gated behind the now-DELIVERED Phase B (`075-command-host-skeleton`, whose golden diff was clean). It carries **no externally visible behavior change**: every command and projection golden, and every snapshot, must remain byte-identical. The "users" of this feature are the maintainers who extend, debug, and review the Verify command host and its JSON projection.

Two modules in `src/` have grown into god modules:

- **`FS.GG.Governance.VerifyCommand/Loop.fs` (1,009 LOC)** — its single `update` loop carries seven responsibilities (cache-eligibility, gate execution, cost-budget, provenance, release-readiness preview, surface checks, generated-view currency), a four-way sensed-facts join, and four parallel "notes" accumulators (`CurrencyNotes`, `Diagnostics`, `ViewCurrencyFindings`, `SurfaceFindings`). The three *optional* feature layers (release-preview, surface-fold, view-currency-fold) are what push it well past the base host size; the sibling `RouteCommand/Loop.fs` carrying only the base pipeline is 622 LOC.
- **`FS.GG.Governance.VerifyJson/VerifyJson.fs` (582 LOC)** — layers four features behind four entry points (`ofVerifyDecision`, `ofVerifyDecisionWithSurfaceChecks`, `ofVerifyDecisionWithPreview`, `ofVerifyDecisionWithGeneratedViews`) in one module, mixing the core verdict writers with surface-finding, release-readiness, and generated-view writers.

The roadmap also poses a **decision gate**: whether to pursue the semi-radical `GateRunHost` unification of the `route → ship → verify` pipeline trio (a single parameterized host replacing three near-identical `executionPlan`/`tryExecute`/projection skeletons). That decision is to be made and recorded as an Architecture Decision Record under `docs/decisions/`, justified or dropped, **within this feature**; the unification itself is NOT implemented here unless the ADR explicitly elects to and the change stays golden-byte-identical.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Verify host loop reads as a base pipeline plus opt-in layers (Priority: P1)

A maintainer opening `VerifyCommand/Loop.fs` to change or debug the base verify pipeline (cache-eligibility → gate execution → cost-budget → provenance) can read and modify it without wading through the release-readiness, surface-check, and view-currency feature code, which now live in clearly named sibling modules wired into the loop through explicit seams.

**Why this priority**: This is the largest maintainability win and the headline of Phase C — the base loop shrinks back toward Route's size and each optional feature becomes independently legible. It must land for the feature to deliver value.

**Independent Test**: Can be tested by building the `VerifyCommand` project, running its full test suite, and confirming every `verify.json` / command golden and snapshot is byte-identical to the pre-change baseline while the base `update` loop measurably shrinks and the three optional layers reside in separate modules.

**Acceptance Scenarios**:

1. **Given** the pre-refactor working tree, **When** the release-preview, surface-fold, and view-currency-fold layers are extracted into sibling modules and the core loop is rewired to call them through explicit seams, **Then** every `VerifyCommand.Tests` test passes and every command/projection golden and snapshot is byte-identical.
2. **Given** the refactored Verify host, **When** a maintainer disables or traces one optional layer, **Then** that layer's code is contained in a single sibling module and the base loop does not reference the other layers' accumulators.
3. **Given** the refactored modules, **When** the surface-area / `.fsi` drift tests run, **Then** the existing `Loop` module surface (the `parse`/`init`/`update`/`render`/`exitCode` contract and its `Model`/`Msg`/`Effect`/`ArtifactKind` types) is byte-identical to baseline, and the only baseline change is the additive record of the new public seam modules' own curated `.fsi` types (FR-004).

---

### User Story 2 - Verify JSON projection is split along its four feature seams (Priority: P1)

A maintainer extending the Verify JSON projection can work on the core verdict writers, the surface-checks writers, the release-readiness writers, or the generated-views writers in isolation, each in its own module, with a thin composing entry module preserving the four existing public entry points.

**Why this priority**: The projection twin is the other half of the god-module problem; splitting it along the same four seams as the host keeps the host/projection structure parallel and is required to call Phase C complete.

**Independent Test**: Can be tested by building `VerifyJson`, running `VerifyJson.Tests`, and confirming the four public entry points still exist with identical signatures and produce byte-identical JSON for every fixture.

**Acceptance Scenarios**:

1. **Given** the pre-refactor `VerifyJson.fs`, **When** it is split into Core / SurfaceChecks / ReleaseReadiness / GeneratedViews modules behind a thin composing entry module, **Then** all four public entry points (`ofVerifyDecision`, `ofVerifyDecisionWithSurfaceChecks`, `ofVerifyDecisionWithPreview`, `ofVerifyDecisionWithGeneratedViews`) keep identical signatures and every `VerifyJson.Tests` golden is byte-identical.
2. **Given** the split projection, **When** the `.fsi` / surface-baseline drift test runs, **Then** the existing `VerifyJson` module surface (`schemaVersion` + the four entry points) is byte-identical to baseline, and the only baseline change is the additive record of the new `Core`/`SurfaceChecks`/`ReleaseReadiness`/`GeneratedViews` seam modules' curated `.fsi` types (FR-004).

---

### User Story 3 - The GateRunHost unification decision is recorded as an ADR (Priority: P2)

An architect reviewing the roadmap can read a single Architecture Decision Record that states whether the semi-radical `GateRunHost` unification of the `route → ship → verify` trio is being pursued now, deferred, or dropped, with the rationale (referencing whether Phase B's golden diff stayed byte-identical).

**Why this priority**: The roadmap explicitly gates this decision on Phase B and requires it be justified or dropped in an ADR. It is a documentation deliverable, not a code change, so it is lower priority than the two splits but must be present for the feature to faithfully close out Phase C's stated scope.

**Independent Test**: Can be verified by confirming a new ADR exists under `docs/decisions/` that takes a clear position (pursue / defer / drop) on `GateRunHost` with stated rationale.

**Acceptance Scenarios**:

1. **Given** Phase B delivered with a clean golden diff, **When** Phase C is implemented, **Then** a new numbered ADR under `docs/decisions/` records the `GateRunHost` decision and its rationale.
2. **Given** the ADR elects NOT to implement `GateRunHost` in this feature, **When** the feature is delivered, **Then** the `route`/`ship`/`verify` host skeletons are left unchanged and the ADR documents the deferral condition.

---

### Edge Cases

- **Optional layer with no findings**: When a verify run produces no surface findings, no release preview, or no generated-view findings, the extracted sibling modules must contribute exactly the same (empty/absent) JSON and accumulator state the inline code did — verified by the existing empty-case goldens.
- **Cross-layer interaction**: Where an optional layer's output feeds the exit-code or diagnostics rollup, the seam must preserve the existing ordering and precedence so the `ExitDecision` and emitted diagnostics are unchanged.
- **Internal-only helpers**: Each extracted seam module exposes through its curated `.fsi` ONLY the entry points the composing module (or a sibling seam) calls; per-seam private helpers (e.g. VerifyJson's `rr*` writers, the closed-enum token helpers) stay absent from that `.fsi`, so the additive surface growth is the minimal seam entry set, not the whole writer plumbing. The EXISTING `Loop`/`VerifyJson` module surfaces never widen (FR-004).
- **Golden drift as a failure signal**: Any byte-level change to a command or projection golden is treated as evidence the extraction changed behavior and must be investigated and reverted, not re-baselined.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The base `VerifyCommand` host loop MUST be reduced to the base pipeline (cache-eligibility, gate execution, cost-budget, provenance) plus explicit seams, with the three optional feature layers — release-readiness preview, surface-check fold, and view-currency fold — extracted into separately named sibling modules within the `VerifyCommand` project.
- **FR-002**: The `VerifyJson` projection MUST be split into four modules along its existing feature seams (core verdict writers, surface-checks writers, release-readiness writers, generated-views writers) plus a thin composing entry module.
- **FR-003**: All four existing public `VerifyJson` entry points (`ofVerifyDecision`, `ofVerifyDecisionWithSurfaceChecks`, `ofVerifyDecisionWithPreview`, `ofVerifyDecisionWithGeneratedViews`) MUST retain identical names and signatures after the split.
- **FR-004**: The public surface of the **existing** `VerifyCommand.Loop` module (its `parse`/`init`/`update`/`render`/`exitCode`/`applyExecution`/`kindOf` contract and its `Model`/`Msg`/`Effect`/`ArtifactKind`/`ScopeSelector`/`OutputFormat`/`UsageError`/`ExitDecision`/`Phase`/`Diagnostic` types) and of the **existing** `VerifyJson` module (`schemaVersion` + the four entry points) MUST be byte-identical to the pre-refactor surface baselines — no existing line removed or changed. The extracted seam modules are **new, additively public** sibling modules, each with its own curated `.fsi`; the two reflective surface-drift baselines (`surface/FS.GG.Governance.VerifyCommand.surface.txt`, `surface/FS.GG.Governance.VerifyJson.surface.txt`) are re-blessed **additively** to record the new modules' types and nothing else. (Decision: chosen over hiding the seams as `internal`/nested-private modules, to keep the repo's `.fsi`-first discipline maximal — see `plan.md` / `research.md` D1. This makes the change **Tier 1 / contracted**, requiring the `.fsi` + surface-baseline updates this FR mandates.)
- **FR-005**: Every command golden (`verify.json` and any other command output) and every projection golden and snapshot fixture MUST remain byte-identical to the pre-refactor baseline. Byte-identical output is the acceptance test for the entire feature.
- **FR-006**: The full test suite MUST be green at every commit, with per-project test counts no lower than the pre-refactor baseline (new tests may be added; none silently lost). The only tolerated exception is the pre-existing, unrelated Cli `dotnet pack` timeout flake noted in prior phases.
- **FR-007**: The split MUST preserve the pure-core / impure-host split, the Elmish/MVU host boundary, the `.fsi`-signature-first discipline, and the acyclic dependency graph — no new cyclic edge and no host dependency introduced into a pure module.
- **FR-008**: A new Architecture Decision Record under `docs/decisions/` MUST record the `GateRunHost` unification decision (pursue / defer / drop) with rationale referencing Phase B's golden-diff result. If the ADR does not elect to implement `GateRunHost` in this feature, the `route`/`ship`/`verify` host skeletons MUST be left unchanged.
- **FR-009**: Where the extraction surfaces a member that is textually shared but type-divergent across hosts/projections (the Phase A/B/D precedent for `dispositionToken` / `CaptureHelpers` / `fail`), that member MUST stay local rather than be force-unified, and the divergence MUST be recorded in the feature's research notes.
- **FR-010**: One feature seam (one optional layer or one projection module) MUST be moved per commit, so each commit's test run isolates the cause of any golden drift.

### Key Entities

- **Verify host optional layer**: One of the three opt-in feature folds (release-readiness preview, surface-check fold, view-currency fold) currently inlined in the Verify `update` loop, to be extracted into its own module while feeding the same accumulators/exit rollup through an explicit seam.
- **Verify projection seam module**: One of the four `VerifyJson` feature areas (Core, SurfaceChecks, ReleaseReadiness, GeneratedViews), to become its own module behind a thin composing entry module.
- **GateRunHost decision ADR**: The numbered decision record under `docs/decisions/` capturing the pursue/defer/drop verdict on the `route → ship → verify` unification and its rationale.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The base `VerifyCommand/Loop.fs` core loop shrinks measurably toward the sibling Route host size (~620 LOC) — concretely, the post-split `Loop.fs` is **≤ 800 LOC** (down from 1,009; ≥ ~200 LOC moved) — with the three optional layers residing in separate modules rather than the single `update` loop. This is a **per-file** measure on the two god modules; the project's **aggregate** LOC MAY rise from new module headers and `.fsi` files (see Assumptions), and that aggregate rise is not an SC-001 failure.
- **SC-002**: 100% of command and projection goldens and snapshots are byte-identical before and after the refactor (zero golden drift).
- **SC-003**: The full test suite is green with per-project test counts greater than or equal to the pre-refactor baseline (modulo the known unrelated Cli pack-timeout flake).
- **SC-004**: `VerifyJson` exposes the same four public entry points with identical signatures, each producing identical JSON, and `VerifyJson.fs` is replaced by ≥4 focused modules plus a thin composing entry.
- **SC-005**: Exactly one ADR documenting the `GateRunHost` decision exists under `docs/decisions/`, taking an unambiguous position with stated rationale.
- **SC-006**: A maintainer can locate the code for any one optional Verify feature (release-preview, surface checks, view currency) in a single named module without reading the base loop, and vice versa.

## Assumptions

- Phase B (`075-command-host-skeleton`) is delivered with a byte-identical golden diff, satisfying the roadmap's gate for considering the `GateRunHost` unification. (Confirmed by CLAUDE.md / roadmap.)
- This phase is clarity-dominated; the roadmap's ~150 LOC reduction estimate is a side effect, not the goal — net LOC may even rise slightly due to new module headers and `.fsi` files, and that is acceptable.
- The four `VerifyJson` feature seams named in the roadmap (Core, SurfaceChecks, ReleaseReadiness, GeneratedViews) map cleanly onto the four existing entry points and their writer groups; no new seam needs to be invented.
- The default expectation for the `GateRunHost` ADR is **defer/drop** for this feature (the split is the committed scope; unification is a larger follow-up), but the planning phase may revisit this if the split surfaces a low-cost unification. The spec requires only that the decision be recorded, not which way it goes.
- The existing surface-baseline, drift, and golden/snapshot tests are sufficient acceptance instrumentation; no new behavioral test category is required, though additive structural tests (e.g., a scope guard on the new modules) may be added. The two surface-drift baselines ARE re-blessed once, additively, to record the new public seam modules (FR-004) — the only sanctioned baseline edit; any change to an EXISTING baseline line is treated as drift and reverted.
- This feature is **Tier 1 (contracted change)** per the constitution's Change Classification, because the chosen split publishes new seam modules as additive public API surface. It therefore carries the full artifact chain: `.fsi` files for each new module, additive surface-baseline updates, test evidence, and this spec/plan. Observable behavior (every golden/snapshot) is unchanged — the Tier 1 trigger is the additive surface, not a behavior change.
- Out of scope: Phase E (CLI render/IO decomposition), any change to Route/Ship hosts beyond what an elected `GateRunHost` ADR would require, and any change to the JSON contract, schema version, or externally observable behavior.
