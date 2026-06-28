# Feature Specification: Publish the Reference Gate Set as a Content Package

**Feature Branch**: `086-reference-gate-set-package`

**Created**: 2026-06-28

**Status**: Draft

**Input**: User description: "next governance item on the project coordination board." → resolved to **FS.GG.Governance#15 (H3, Workstream Governance)**: *Publish `FS.GG.Governance.ReferenceGateSet` content package (source of truth for the Templates overlay).*

> **Board context**: This is the only open governance item on the FS-GG Coordination board with no blocker (#14 remains blocked by the not-yet-delivered `FS.GG.Contracts` package, SDD#8). Completing it **unblocks** the Templates overlay drift gate (Templates#14). It is labelled `contract-change` + `roadmap`: the contract it touches is the governance config schema bundle.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Templates consumes one published source of truth for its governance overlay (Priority: P1)

The Templates repo currently has to hand-copy the populated `.fsgg` gate set into its `fs-gg-governance` overlay, which silently drifts from the governance repo's validated reference. As the Templates maintainer, I want to obtain the reference gate set from a **single published, versioned artifact** so my overlay drift gate (`git diff --exit-code`) has an authoritative thing to compare against, and a stale copy fails the build instead of shipping.

**Why this priority**: This is the reason the item exists on the roadmap — it is the dependency that unblocks Templates#14. Without a published source of truth, the Templates overlay drift gate cannot be wired and the populated-overlay direction (P3/P4) stays unenforced.

**Independent Test**: Install the published package into a clean consumer, materialize its `.fsgg` files, and run the Templates-side drift comparison against them — an unmodified overlay passes, a tampered overlay fails. Delivers value even before any other story: Templates gets its source of truth.

**Acceptance Scenarios**:

1. **Given** the package is published at a known version, **When** a consumer installs it and materializes its content, **Then** exactly the four reference config files (`governance.yml`, `policy.yml`, `capabilities.yml`, `tooling.yml`) appear at a documented, predictable location.
2. **Given** a consumer's overlay is byte-identical to the package content, **When** the drift comparison runs, **Then** it reports no difference (exit 0).
3. **Given** a consumer's overlay differs from the package content, **When** the drift comparison runs, **Then** it reports the difference and fails.

---

### User Story 2 - The shipped artifact is provably the validated reference set (Priority: P2)

As the governance maintainer, I want the published package content to be **the exact same files that the existing G1–G7 reference-set tests validate**, with no second copy, so that what consumers receive is provably the gate set whose invariants (3 gates, declared commands, light-by-default, ratchet behaviour) are frozen by the test suite.

**Why this priority**: A published reference that can drift from its own tests would defeat the purpose — consumers would pin a "reference" that was never the validated one. Single-sourcing is what makes the package trustworthy, but it is only valuable once the package exists (US1).

**Independent Test**: Confirm the package draws its content from `samples/sdd-reference-gate-set/.fsgg/` (the directory the G1–G7 guard loads), not from a duplicated copy; mutate a sample file and confirm both the tests and the package guard react.

**Acceptance Scenarios**:

1. **Given** the reference sample files are the package's source, **When** the package is built, **Then** its contents are byte-identical to `samples/sdd-reference-gate-set/.fsgg/`.
2. **Given** the G1–G7 reference-set tests are failing, **When** the package build/guard runs, **Then** the package cannot be produced/published (the tested artifact and the shipped artifact are the same thing).

---

### User Story 3 - Schema-versioned package so consumers can pin coherently (Priority: P3)

As a consumer pinning the reference set, I want the package version to **reflect the schema versions of the contained config files** (capabilities schema = 2, the other three = 1) so that a schema-version change is visible as a distinguishable package version and I can pin a coherent set rather than guessing.

**Why this priority**: Versioning by schema makes future schema migrations legible to consumers, but the package is useful as a source of truth (US1) before this coherence policy is layered on.

**Independent Test**: Inspect the produced package's version metadata and confirm it is derived from the contained schema versions per the documented scheme; simulate a schema bump and confirm the derived package version changes accordingly.

**Acceptance Scenarios**:

1. **Given** the contained files declare their schema versions, **When** the package version is computed, **Then** it is derived deterministically from those schema versions per a documented rule.
2. **Given** a contained file's `schemaVersion` is bumped, **When** the package is re-built, **Then** the package version changes to a distinguishable value.

---

### Edge Cases

- **Sample changes without test/version update**: a maintainer edits a `.fsgg` file but the G1–G7 invariants no longer hold → the build/guard MUST fail rather than ship a "reference" that contradicts its frozen invariants.
- **Mixed schema versions across the four files**: the version-derivation rule MUST be defined for the case where the four files do not share a single schema version (current state: caps=2, rest=1).
- **Consumer materialization location**: the documented location MUST be predictable and stable across versions so a consumer's drift gate path does not break on upgrade.
- **Content-only guarantee**: a consumer that installs the package MUST NOT be forced to take on any governance runtime/assembly dependency to read the files.
- **Feed availability**: the org package feed is not yet provisioned (admin-blocked, .github#21). The feature MUST still produce a consumable artifact (and register the contract) even if the org-feed push is deferred.

## Requirements *(mandatory)*

### Change Classification

**Tier 1 (contracted change).** This feature introduces a **new published package contract** — a new cross-repo surface (`FS.GG.Governance.ReferenceGateSet`: package id + content layout + version-derivation rule) consumed by Templates#14. It therefore carries the full Tier-1 obligations: registry entry, compatibility projection, and an ADR (FR-008/SC-006). The usual Tier-1 `.fsi`/surface-baseline obligations are **vacuously satisfied** — the package ships no assembly and authors no F# public API; the contract surface is the package metadata and content layout, which is what gets registered. (No public F# surface ≠ Tier 2: the *contract* is what sets the tier, not the presence of an API.)

### Functional Requirements

- **FR-001**: The system MUST produce a distributable **content** package named `FS.GG.Governance.ReferenceGateSet` that carries the four reference config files (`governance.yml`, `policy.yml`, `capabilities.yml`, `tooling.yml`).
- **FR-002**: The package content MUST be sourced from `samples/sdd-reference-gate-set/.fsgg/` — the same directory the existing G1–G7 reference-set guard loads — with **no duplicated second copy** of the files.
- **FR-003**: The package content MUST be byte-identical to the on-disk reference sample at build time (0 drift between tested and shipped).
- **FR-004**: Producing/publishing the package MUST be guarded so it cannot ship when the existing G1–G7 reference-set tests fail.
- **FR-005**: When a consumer installs the package, the four config files MUST be materialized at a **documented, predictable, version-stable** location suitable for a drift comparison.
- **FR-006**: The package version MUST be derived deterministically from the contained schema versions (capabilities = 2, governance/policy/tooling = 1) per a documented rule.
- **FR-007**: The package MUST be content-only: installing it MUST NOT impose any governance runtime or assembly dependency on the consumer.
- **FR-008**: The package MUST be registered as a versioned cross-repo contract in the FS-GG registry (`registry/dependencies.yml`) with the compatibility projection (`docs/registry/compatibility.md`) updated, per ADR-0001 (this is a `contract-change`).
- **FR-009**: The feature MUST leave the Templates overlay drift gate (Templates#14) able to consume this package as its source of truth — i.e., the materialized content and its location are consumable by an external `git diff --exit-code`-style gate.

### Key Entities *(include if feature involves data)*

- **Reference Gate Set bundle**: the four governance config files (`governance.yml`, `policy.yml`, `capabilities.yml`, `tooling.yml`) under `samples/sdd-reference-gate-set/.fsgg/`; the validated, single-source set of build/test/evidence gates.
- **`FS.GG.Governance.ReferenceGateSet` content package**: the distributable artifact wrapping the bundle; version derived from the bundle's schema versions; content-only.
- **Schema versions**: the per-file `schemaVersion` declarations (caps = 2, rest = 1) that drive package versioning.
- **Registry contract entry**: the `dependencies.yml` record naming the package as a versioned cross-repo surface consumed by Templates.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A consumer can obtain the reference `.fsgg` gate set from a single published artifact rather than copying files — Templates#14's overlay drift gate has an authoritative source to compare against (the H3 blocker is removed).
- **SC-002**: 0 drift — the published package's contents are byte-identical to `samples/sdd-reference-gate-set/.fsgg/`, verifiable by comparison.
- **SC-003**: The package version is reproducibly derived from the contained schema versions; a simulated schema-version bump yields a distinguishable package version 100% of the time.
- **SC-004**: Installing the package yields exactly the four config files at the documented location, and re-running the G1–G7 invariants against the installed copy passes (the shipped artifact is the validated artifact).
- **SC-005**: A consumer can read the files with no governance runtime/assembly reference (content-only confirmed).
- **SC-006**: The registry and its compatibility projection name the package as a versioned contract; the entry links its registry PR (ADR-0001).

## Assumptions

- The four files under `samples/sdd-reference-gate-set/.fsgg/` are the canonical reference gate set, established by feature 079 and frozen by the G1–G7 guard in `tests/FS.GG.Governance.ReferenceGateSet.Tests/`. This feature publishes that set; it does not change its content or invariants.
- "Content package" means a code-free package whose payload is the config files (consistent with the cross-repo coordination protocol's "NuGet content package" language). No new public API/assembly surface is introduced — but the package is still **Tier 1** (see Change Classification above) because it is a new cross-repo *contract*. The 079 guard adds no API surface either; the absence of an `.fsi` surface here is a vacuous satisfaction of the Tier-1 `.fsi` obligation, not a downgrade to Tier 2.
- **Version-derivation default** (no reasonable single answer existed, informed guess documented rather than blocking): the package version is derived from the bundle's schema versions using the **highest-fidelity coherent scheme available** — concretely, the package's leading version components track the governance/config schema generation, and a bump to any contained `schemaVersion` produces a distinguishable package version. The exact numeric mapping is a planning-phase detail; the requirement (FR-006/SC-003) is determinism + distinguishability, not a specific numbering.
- **Distribution scope**: producing the packable artifact and registering the contract are in scope. Pushing to the **org GitHub Packages feed** depends on admin provisioning (.github#21, H4, currently `Blocked`); until that lands, a consumable artifact via CI/local pack is sufficient to unblock Templates#14. The org-feed push is a follow-on, not part of this feature's done-definition.
- The Templates-side overlay drift gate itself (Templates#14) is **out of scope** here — this feature delivers the source of truth it consumes, not the gate.
- Schema/loader re-typing onto `FS.GG.Contracts` (governance#14) is independent and out of scope; this feature does not depend on the Contracts package.
