# Feature Specification: CommandHost skeleton extraction

**Feature Branch**: `075-command-host-skeleton`

**Created**: 2026-06-27

**Status**: Draft

**Input**: User description: "next item in docs/reports/2026-06-26-203146-architecture-quality-deduplication-design.md" — Phase B (CommandHost skeleton extraction) of the architecture/quality/de-duplication roadmap. Phases A (JSON emit consolidation, feature 073) and D (shared test library, feature 074) are already DELIVERED; per the roadmap's suggested sequencing (`A`/`D` → `B` → `C` → `E`), Phase B is the next item.

## Overview

The MVU command hosts (`RouteCommand`, `ShipCommand`, `VerifyCommand`,
`RefreshCommand`, `CacheEligibilityCommand`, `ReleaseCommand`, `EvidenceCommand`)
each hand-roll a near-identical skeleton of host helpers inside their own
`Loop.fs`. The same small functions — a repo-relative path joiner, an exit-code
mapper, a failure-transition helper, an empty sensed-facts value, a base/head
revision resolver, a gate-classification type, a snapshot builder, a
`tryExecute` driver, and a ~75-LOC `executionPlan` — are copied across three to
six files apiece. A determinism or correctness fix to any of them today requires
synchronized edits across several command projects, and the copies have already
begun to drift.

This feature extracts the verbatim (and near-verbatim, parameterizable) skeleton
into one new pure leaf library that the command hosts reference, deleting the
local copies. It mirrors the already-delivered Phase A pattern (new pure leaves
below the existing layers, `.fsi`-first, byte-identical golden output as the
acceptance test) and the Phase D pattern (only genuinely-shared members move;
members whose types diverge per command stay local).

**Change classification:** Tier 1 — introduces a new project with a new public
`.fsi` surface and a new surface-area baseline, and adds inter-project dependency
edges from the command hosts to the new leaf. No observable behavior changes:
every command and projection golden/snapshot remains byte-identical.

## Clarifications

### Session 2026-06-27

- Q: Should the semi-radical single-parameterized-`GateRunHost` unification of route/ship/verify be attempted in this feature? → A: No — this feature is the helper/`executionPlan` extraction only (roadmap Phase B). The `GateRunHost` unification is explicitly deferred to Phase C and gated on this phase's golden diff staying byte-identical.
- Q: When a candidate helper is textually identical but its TYPE is parameterized by a per-command type (e.g. command-specific `Effect`/`Model`/`ArtifactKind`), should it be forced into the shared leaf via generics? → A: No — follow the delivered Phase A/D discipline: only genuinely-shareable members move; type-divergent members stay local and the divergence is recorded. Byte-identity is the gate that catches a wrong move.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One source of truth for the host skeleton (Priority: P1)

A maintainer needs to change a shared host behavior — for example, correcting the
exit-code mapping, the repo-relative path joining, or the gate-execution plan
ordering. Today they must find and edit the copy in every command host and keep
the copies in lockstep. After this feature, the shared helper lives in one leaf
library; the maintainer edits it once, and every command host picks up the change.

**Why this priority**: This is the core value of the feature — eliminating the
copy-paste maintenance liability identified as Finding 2 in the design report.
Everything else is in service of delivering this safely.

**Independent Test**: Can be tested by changing a shared helper in the leaf and
confirming all consuming command hosts compile against it and exhibit the changed
behavior, with no remaining local copies of that helper in the command `Loop.fs`
files.

**Acceptance Scenarios**:

1. **Given** the new `CommandHost` leaf with the extracted helpers, **When** the
   full test suite runs, **Then** every command (`route.json`, `audit.json`,
   `verify.json`, refresh, cache-eligibility, release, evidence) and projection
   golden/snapshot is byte-identical to the pre-feature baseline.
2. **Given** a command host that previously carried a local copy of a moved
   helper, **When** its `Loop.fs` is inspected, **Then** the local copy is gone
   and the helper is consumed from the shared leaf.
3. **Given** the shared `executionPlan`, **When** Route, Ship, and Verify each
   request a gate-execution plan, **Then** each receives the same plan it
   produced before the extraction (the per-command differences are expressed
   through the plan's parameters, not through divergent copies).

---

### User Story 2 - Boundaries and discipline are preserved (Priority: P1)

A reviewer needs assurance that the extraction strengthens rather than erodes the
repository's architecture: the new leaf must be pure (no host/I/O dependency), it
must sit below the command hosts in an acyclic graph, it must expose exactly its
shared surface through a curated `.fsi`, and it must carry a surface-area
baseline with a drift test, matching the discipline of the Phase A leaves.

**Why this priority**: The design report's central thesis is that the fix is to
*add the missing shared leaves the architecture already implies* without
collapsing any boundary. A leaf that took a host dependency, or that leaked extra
surface, would violate that thesis and the constitution's `.fsi`-first rule.

**Independent Test**: Can be tested by confirming the leaf's project references
contain no host/impure project, that the dependency graph remains acyclic, and
that the surface-drift test for the leaf passes against its baseline.

**Acceptance Scenarios**:

1. **Given** the new leaf, **When** its dependencies are inspected, **Then** it
   depends only on already-shared domain types and takes no host, filesystem,
   git, or process dependency.
2. **Given** the new leaf, **When** its `.fsi` is inspected, **Then** it exposes
   exactly the helpers being shared and nothing more, and a surface-area baseline
   plus drift test exist for it.
3. **Given** the dependency graph after the feature, **When** it is analyzed,
   **Then** it remains acyclic and the pure-core/impure-host split is intact.

---

### User Story 3 - Type-divergent helpers correctly stay local (Priority: P2)

A maintainer must be able to trust that a helper which *looks* identical across
commands but is actually parameterized by a command-specific type was correctly
left in place rather than forced into the shared leaf behind a leaky abstraction.

**Why this priority**: Phase A (local `dispositionToken`) and Phase D
(`CaptureHelpers`) both discovered members that were byte-identical in text but
type-divergent, and correctly kept them local. This feature must apply the same
discipline so the shared leaf stays honestly shared.

**Independent Test**: Can be tested by confirming that each helper retained
locally in a command host has a recorded reason (a per-command type it depends
on) and that the byte-identity gate stays green — i.e. no behavior moved.

**Acceptance Scenarios**:

1. **Given** a candidate helper whose signature references a command-specific
   type, **When** the extraction is performed, **Then** that helper stays local
   and the reason is recorded in the feature's design notes.
2. **Given** the completed extraction, **When** the golden/snapshot suite runs,
   **Then** it is byte-identical, demonstrating that only genuinely-shared,
   behavior-preserving members moved.

---

### Edge Cases

- **A helper is shared by some commands but diverges in others** (the design
  report cited `cacheReportOf` as appearing in four hosts with Route's
  cache-report shape potentially diverging; at the current working tree planning
  found only a single surviving site — research D6 — so `cacheReportOf` stays
  local). The general rule holds: where a genuinely-shared form exists it must
  capture only the common behavior; any command whose variant diverges keeps its
  local form, and the byte-identity gate confirms no command's output changed.
- **The `executionPlan` per-command differences** must be expressed as
  parameters (a per-command fold/option record) rather than as branches that
  special-case command identity inside the shared leaf.
- **A command host (e.g. `EvidenceCommand`, `RefreshCommand`) uses only a subset
  of the skeleton.** It references only the helpers it needs; it does not gain a
  dependency on unused surface.
- **The optional `Blocked`/`GovernedBlocking` exit path** present in some hosts
  but not others must be accommodated by the shared `exitCode` without changing
  any host's current exit codes.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST introduce one new pure leaf library (working name
  `FS.GG.Governance.CommandHost`) that holds the genuinely-shared command-host
  skeleton helpers, placed below the command hosts in the dependency graph.
- **FR-002**: The new leaf MUST be pure: it MUST NOT take a dependency on any
  host, filesystem, git, process, or other impure project; it depends only on
  already-shared domain types.
- **FR-003**: The new leaf MUST expose its shared surface through a curated
  `.fsi` signature file that declares exactly the shared helpers and nothing
  more, per the repository's `.fsi`-first discipline.
- **FR-004**: The new leaf MUST carry a surface-area baseline file and an
  automated surface-drift test, matching the convention of the Phase A leaves.
- **FR-005**: The verbatim/near-verbatim skeleton helpers MUST be moved into the
  leaf, including at least: the repo-relative path joiner (`under`), the
  exit-code mapper (`exitCode`, accommodating the optional blocked path), the
  failure-transition helper (`fail`) and invalid-diagnostics descriptor
  (`describeInvalid`), the empty sensed-facts value (`emptySensedFacts`), the
  base/head revision resolver (`baseHeadOf` / `revOfCommit`), the persisted-store
  helpers (`persistedContent`, `awaitingPersist`), the gate-classification type
  (`GateClassification`), the snapshot builder (`buildSnapshot`), the kinded-runs
  helpers (`kindedRunsOf`, `kindOf`), and the gate-execution driver
  (`tryExecute`). Final per-helper membership is decided in planning by whether a
  genuinely-shared form exists; the cache-report resolver (`cacheReportOf`) was a
  move candidate but planning (research D6) found it has only a **single surviving
  defining site** (`CacheEligibilityCommand`) at the current working tree, so it
  **stays local** — there is no duplication to remove.
- **FR-006**: The `executionPlan` helper MUST be parameterized (via a per-command
  record of the optional folds/inputs that differ between commands) and moved to
  the leaf; Route, Ship, and Verify MUST call the shared parameterized form and
  receive plans identical to those they produced before the extraction.
- **FR-007**: Each consuming command host MUST delete its local copies of the
  moved helpers and consume them from the leaf; no moved helper may remain
  duplicated in a command `Loop.fs`.
- **FR-008**: Any candidate helper whose type is parameterized by a
  command-specific type (so that a shared form would require a leaky abstraction)
  MUST stay local, and the reason MUST be recorded in the feature's design notes
  — mirroring the Phase A (`dispositionToken`) and Phase D (`CaptureHelpers`)
  precedents.
- **FR-009**: Every command and projection golden/snapshot fixture MUST remain
  byte-identical to the pre-feature baseline; byte-identity is the acceptance
  test for behavior preservation.
- **FR-010**: The full test suite MUST be green at every commit, and per-project
  test counts MUST match the pre-feature baseline except for the additive test
  project(s) introduced for the new leaf itself.
- **FR-011**: The dependency graph MUST remain acyclic and the pure-core/
  impure-host assembly split MUST be preserved after the new edges are added.
- **FR-012**: The semi-radical `GateRunHost` unification of route/ship/verify
  MUST NOT be attempted in this feature; it is deferred to roadmap Phase C and
  gated on this feature's golden diff staying byte-identical.
- **FR-013**: One concern (one helper or one cohesive helper group) SHOULD be
  moved per commit so that any golden drift is isolated to a single change.

### Key Entities

- **CommandHost leaf**: the new pure shared library holding the host skeleton
  helpers and the parameterized `executionPlan`; referenced by the command hosts,
  references only shared domain types.
- **Execution-plan parameters**: the per-command record describing the optional
  folds/inputs (cache-eligibility, gate execution, and the like) that distinguish
  one command's gate-execution plan from another's, supplied by each host so the
  shared `executionPlan` produces that host's exact plan.
- **Command hosts**: the MVU `Loop.fs` modules (`RouteCommand`, `ShipCommand`,
  `VerifyCommand`, `RefreshCommand`, `CacheEligibilityCommand`, `ReleaseCommand`,
  `EvidenceCommand`) that consume the leaf and shed their local copies.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Each moved helper exists in exactly one place (the leaf); the count
  of duplicate copies across command `Loop.fs` files for every moved helper drops
  to zero.
- **SC-002**: 100% of command and projection golden/snapshot fixtures are
  byte-identical to the pre-feature baseline.
- **SC-003**: The full test suite is green, with per-project test counts matching
  the pre-feature baseline except for the additive new-leaf test project(s).
- **SC-004**: Net source-line reduction across the command hosts is on the order
  of 400–500 LOC (the design report's estimate for this phase), after accounting
  for the new leaf body and `.fsi`.
- **SC-005**: The new leaf has a curated `.fsi`, a surface-area baseline, and a
  passing surface-drift test; it has zero host/impure project dependencies.
- **SC-006**: A maintainer can change any moved shared behavior by editing one
  file in the leaf rather than N command-host copies.

## Assumptions

- The "next item" in the design report is Phase B (CommandHost skeleton
  extraction), since Phases A (feature 073) and D (feature 074) are already marked
  DELIVERED and the report's suggested sequencing is `A`/`D` → `B` → `C` → `E`.
- The duplication counts in the design report remain materially accurate at the
  current working tree (spot-checked during specification: `under` appears in 6
  command hosts, `exitCode`/`fail` in 6, `executionPlan`/`tryExecute`/
  `GateClassification`/`emptySensedFacts` in 3, `baseHeadOf` in 4). The design
  report's "`cacheReportOf` in 4" count predates drift; planning's deeper audit
  (research D6) found only a single surviving site, so `cacheReportOf` stays
  local. Exact final move membership is a planning/implementation detail, decided
  per helper by whether a genuinely-shared form exists.
- The new leaf's working name is `FS.GG.Governance.CommandHost`; the final
  package/project name is confirmable during planning consistent with the
  `FS.GG.Governance.*` namespace convention.
- The god-module split of `VerifyCommand/Loop.fs` and `VerifyJson.fs` (Finding 2's
  second half) is **out of scope** here; it belongs to roadmap Phase C. This
  feature is the skeleton extraction only.
- The CLI decomposition (Phase E) and any further consolidation are out of scope.
