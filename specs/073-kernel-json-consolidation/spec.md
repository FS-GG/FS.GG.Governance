# Feature Specification: Kernel JSON consolidation

**Feature Branch**: `073-kernel-json-consolidation`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "next item in docs/reports/2026-06-26-203146-architecture-quality-deduplication-design.md" — Phase A (Kernel JSON consolidation) of the architecture / quality / de-duplication roadmap.

## Overview

The repository emits deterministic JSON from twelve `*Json` projection projects. The
canonical deterministic-emit helper (`writeToString`) already exists in `Kernel` but is
not exported, so it has been copied **14 times** across `src`. On top of that, the
projection modules copy closed-enum token helpers (e.g. `costToken`, `severityToken`)
and sub-object writers (e.g. `writeCause`, `verdictByGate`). This feature removes that
duplication by exposing the canonical emit helper and adding two small, pure shared leaf
libraries the projections reference instead of re-implementing. The machine-readable
output contract is pinned by golden/snapshot tests, so the consolidation is verifiable
**byte-for-byte**: no projection output may change.

**Change classification: Tier 1 (contracted change).** It adds public API surface
(`writeToString` exported from `Kernel/Json.fsi`; two new projects with `.fsi` surfaces),
adds inter-project dependency edges, and requires surface-area baseline updates. It does
**not** change any observable JSON output.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Single source of truth for deterministic JSON emit (Priority: P1)

A maintainer needs to change how deterministic JSON is serialized (for example, a
flush/encoding detail that affects emit determinism). Today that fix must be applied in
14 separate copies of `writeToString`; one missed copy silently diverges. After this
story, the canonical `Kernel` helper is the only implementation, referenced by every
projection.

**Why this priority**: This is the highest-value, lowest-risk slice. It removes the
largest single source of copy-paste, establishes the `Kernel` dependency edge the other
stories build on, and is independently shippable. A determinism fix becomes a one-line
change instead of a 14-site sweep.

**Independent Test**: Export `writeToString` from `Kernel/Json.fsi`, wire the `Kernel`
reference into the projection projects that lack it, delete the local copies, and confirm
every `*Json.Tests` golden snapshot is byte-identical and the full suite stays green.
Delivers a single source of emit determinism on its own.

**Acceptance Scenarios**:

1. **Given** the twelve `*Json` projections and the `Host`/command projects that
   currently define a local `writeToString`, **When** the canonical `Kernel`
   `writeToString` is exported and referenced, **Then** no `src` file outside `Kernel`
   defines its own `writeToString`.
2. **Given** the existing `*Json.Tests` golden and snapshot fixtures, **When** the
   projections call the shared `writeToString`, **Then** every golden fixture is
   byte-identical to its pre-change content (no fixture is regenerated or edited).
3. **Given** a projection project that did not previously reference `Kernel`, **When** the
   reference is added, **Then** the dependency graph remains acyclic and the project still
   builds.

---

### User Story 2 - Shared closed-enum token helpers (Priority: P2)

A maintainer reads or edits a closed-enum-to-string mapping (cost, maturity, severity,
environment, disposition, basis, profile). Today these token helpers are copied across
the projection modules — `severityToken` and `maturityToken` appear four times each,
others two to three times. After this story there is one pure helper per closed enum.

**Why this priority**: Second-largest duplication cluster and a frequent edit site
(adding an enum case touches every copy today). Depends on the `Kernel` edge established
in Story 1, so it follows P1, but is independently shippable once that edge exists.

**Independent Test**: Add a pure `Kernel.JsonTokens` leaf (`.fsi` + `.fs`) exposing the
seven token helpers, replace the in-module copies across the projections, and confirm
token-emitting goldens are byte-identical and the suite is green.

**Acceptance Scenarios**:

1. **Given** the seven closed-enum token helpers currently copied across projections,
   **When** `Kernel.JsonTokens` is introduced, **Then** each token helper is defined once
   in the leaf and no projection redefines it locally.
2. **Given** the `Verdict` token (`verdictToken`/`rrVerdictToken`) whose copies emit
   *different* strings across projections (`Fail` → `blocked` in `VerifyJson.verdictToken`
   vs `fail` in `ReleaseJson.verdictToken` and `VerifyJson`'s `rr`-prefixed `rrVerdictToken`),
   **When** the seven shared helpers are adopted, **Then** the `Verdict` token is left local
   and untouched — it is not one of the seven enums and its copies cannot be unified without
   changing bytes (spec Edge Cases).
3. **Given** every golden that contains a closed-enum token string, **When** the shared
   helpers are used, **Then** all such goldens are byte-identical.

---

### User Story 3 - Shared sub-object writers (Priority: P3)

A maintainer edits how a recurring JSON sub-object is written (a cause object, a
per-gate verdict/outcome map, an execution block, an enforcement block). Today `writeCause`
is copied six times and several others two to three times. After this story these writers
live once in a pure shared leaf.

**Why this priority**: Smallest of the three clusters and the most intricate to extract
(sub-object writers carry more shape), so it is sequenced last. Depends on the `Kernel`
edge and benefits from `Kernel.JsonTokens` already existing, but is independently
shippable.

**Independent Test**: Add a pure `Kernel.JsonWriters` leaf (`.fsi` + `.fs`) exposing the
shared sub-object writers, replace the copies, and confirm all affected goldens are
byte-identical and the suite is green.

**Acceptance Scenarios**:

1. **Given** the sub-object writers currently copied across projections (`writeCause`,
   `verdictByGate`, `outcomeByGate`, `writeExecution`, `writeEnforcement`), **When**
   `Kernel.JsonWriters` is introduced, **Then** each writer is defined once and no projection
   redefines it locally.
2. **Given** the goldens that include these sub-objects, **When** the shared writers are
   used, **Then** every such golden is byte-identical.
3. **Given** the new leaf, **When** its surface is inspected, **Then** it takes no host
   dependency and depends only on already-shared domain types (it stays a pure leaf).

---

### Edge Cases

- **Golden drift = behaviour change.** If any golden or snapshot fixture would change,
  the extraction has altered behaviour and MUST be revisited rather than re-baselining the
  fixture. A moved golden is a failure signal, not an update target.
- **Missing dependency edge.** A projection that did not reference `Kernel` (or the new
  leaves) must gain the reference without introducing a dependency cycle; if any addition
  would create a cycle, that projection is handled separately and the cycle is reported.
- **Near-identical-but-not-identical copies.** Some copies differ subtly from one another
  (e.g. the `Verdict` token: `VerifyJson.verdictToken` emits `Fail` → `blocked`, while
  `ReleaseJson.verdictToken` and `VerifyJson`'s `rr`-prefixed `rrVerdictToken` emit
  `Fail` → `fail`). Each such copy MUST be confirmed byte-identical before any unification;
  a genuine behavioural difference is surfaced and the copy left local, never silently
  unified. (The `Verdict` token is therefore out of scope here — see Assumptions.)
- **Surface-area baselines.** Adding exported symbols (the `writeToString` export and the
  two new leaf surfaces) MUST be reflected in the per-module surface-area baselines;
  leaving them stale is a defect even if tests pass.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The canonical deterministic-emit helper `writeToString` MUST be exported
  from `Kernel/Json.fsi` so other projects can reference it.
- **FR-002**: Every `src` project that currently defines a local `writeToString` MUST be
  changed to reference and use the `Kernel` helper, and its local copy MUST be deleted, so
  that `Kernel` holds the only definition in `src`.
- **FR-003**: Each projection project that uses the shared helper but does not currently
  reference `Kernel` MUST gain that project reference, without introducing a dependency
  cycle.
- **FR-004**: A new pure leaf library `Kernel.JsonTokens` MUST provide the seven shared
  closed-enum token helpers (cost, maturity, severity, environment, disposition, basis,
  profile), each defined exactly once, with a curated `.fsi`.
- **FR-005**: All in-module copies of those seven token helpers MUST be replaced by the
  `Kernel.JsonTokens` helpers and the local copies deleted. The `Verdict` token
  (`verdictToken`/`rrVerdictToken`) is out of scope: its copies emit different strings and
  cannot be unified without changing bytes, so each stays local (see Edge Cases).
- **FR-006**: A new pure leaf library `Kernel.JsonWriters` MUST provide the shared
  sub-object writers that are duplicated across projections (`writeCause`, `verdictByGate`,
  `outcomeByGate`, `writeExecution`, `writeEnforcement`), each defined exactly once, with a
  curated `.fsi`.
- **FR-007**: All in-module copies of those sub-object writers MUST be replaced by the
  `Kernel.JsonWriters` helpers and the local copies deleted.
- **FR-008**: The new leaf libraries MUST be pure — they MUST take no host (impure)
  dependency and depend only on `Kernel`/already-shared domain types — preserving the
  pure-core/impure-host split.
- **FR-009**: Every machine-readable JSON output (every `*Json.Tests` golden and snapshot
  fixture) MUST remain byte-identical before and after each story; no fixture may be
  regenerated or edited to accommodate the change.
- **FR-010**: The full test suite MUST pass at every shippable increment, with no change
  in test count (no tests removed or silently skipped to achieve green).
- **FR-011**: Each newly exported surface (the `writeToString` export and the two leaf
  `.fsi` surfaces) MUST be reflected in the corresponding surface-area baseline.
- **FR-012**: Each leaf MUST be introduced `.fsi`-first, exposing exactly the helpers
  being shared and nothing more, matching the repository's signature-first discipline.

### Key Entities *(include if feature involves data)*

- **`writeToString` (canonical emit helper)**: The single deterministic
  `Utf8JsonWriter`-to-`string` serializer, owned by `Kernel/Json`, consumed by all
  projections.
- **`Kernel.JsonTokens` (pure leaf)**: One closed-enum-to-token-string helper per closed
  enum (cost, maturity, severity, environment, disposition, basis, profile).
- **`Kernel.JsonWriters` (pure leaf)**: Shared sub-object writers for recurring JSON
  shapes (cause, per-gate verdict/outcome maps, execution, enforcement).
- **`*Json` projection projects**: The twelve deterministic-JSON projections that consume
  the above instead of holding local copies.
- **Golden / snapshot fixtures**: The pinned machine-contract outputs that serve as the
  byte-for-byte acceptance test for the consolidation.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Exactly one definition of `writeToString` exists in `src` (in `Kernel`) — the
  14 hand-copied non-`Kernel` definitions removed (15 total today → 1).
- **SC-002**: Each of the seven closed-enum token helpers has exactly one definition in the
  JSON layer (`Kernel.JsonTokens`); no projection redefines them locally. (The `Verdict`
  token is out of scope and intentionally remains local — FR-005.)
- **SC-003**: Each shared sub-object writer has exactly one definition; no projection
  redefines `writeCause`, `verdictByGate`, `outcomeByGate`, `writeExecution`, or
  `writeEnforcement` locally.
- **SC-004**: 100% of `*Json.Tests` golden and snapshot fixtures are byte-identical to
  their pre-change content.
- **SC-005**: The full test suite passes with an unchanged test count after each of the
  three stories.
- **SC-006**: Net `src` line reduction is on the order of ~300 lines once all three
  stories land.
- **SC-007**: The dependency graph remains acyclic, and the new leaves carry no host
  dependency (pure-core/impure-host split preserved).

## Assumptions

- "Next item" in the referenced report resolves to **Phase A — Kernel JSON
  consolidation**, the first phase in the roadmap and the reduction-summary table, and one
  of the two phases the report names as first to run.
- The report's verified counts (14 `writeToString` copies; token-helper and sub-object-
  writer copy counts) are accurate as of commit `fc845ae`; spot-checks at the working tree
  confirm 14 copies, that `writeToString` is absent from `Json.fsi`, and that twelve
  `*Json` projects exist. Exact copy counts are confirmed during implementation.
- The `Verdict` token (`verdictToken`/`rrVerdictToken`) and the single-use
  `writeNullableString`/`writeNullableInt` writers are **out of scope**: the `Verdict`
  copies emit divergent strings (so unifying would change bytes) and the nullable writers
  are single-use (ReleaseJson only), so de-duplicating either would add a dependency edge
  for no reduction. Only helpers actually copied across projections *and* byte-identical
  are consolidated.
- Existing golden/snapshot tests already cover every JSON shape affected, so byte-identity
  is a sufficient acceptance test; no new behavioural tests are required beyond confirming
  the goldens hold.
- The three stories are landed in priority order (P1 → P2 → P3), each as an independently
  shippable increment that keeps the full suite green, per the report's
  "one concern moved at a time" rule.
- This feature is scoped to Phase A only. The CommandHost extraction (Phase B), god-module
  split (Phase C), shared test library (Phase D), and CLI decomposition (Phase E) are
  separate features and are out of scope here.
- `net10.0`, F#-only, `.fsi`-first, and surface-area-baseline conventions from the
  constitution apply unchanged.
