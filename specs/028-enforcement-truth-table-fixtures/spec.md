# Feature Specification: Golden Enforcement Truth-Table Fixtures

**Feature Branch**: `028-enforcement-truth-table-fixtures`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved against
`docs/initial-implementation-plan.md`. Phase 2 (the Governance Ship Walking Skeleton & Catalog MVP)
closed when F027 (GitHub Actions branch-protection guidance) merged. Phases 3–4 are `FS.GG.SDD`-owned and
out of scope for this repository's backlog. The next **Governance-owned** open work is the two remaining
`🟡` rows of **Phase 5: Route Parity, Profiles, and Enforcement Fixtures**:

1. *"Generate golden enforcement truth-table fixtures covering routine versus fenced routes, base
   severity, rule tier, all modes, all profiles, all maturity levels, and unknown governed paths."*
2. *"Add representative JSON snapshots for combinations that alter blocking."*

These two rows close Phase 5's exit criteria **"Every enforcement dial has fixture coverage"** and
**"Profile-adjusted blocking is explained without changing rule truth."** They are paired — the snapshots
in row 2 are the JSON view of the blocking-altering rows of the truth table in row 1 — so they are sliced
as one feature here, with row 1 as the P1 MVP and row 2 as the P2 follow-on.

## Overview

The enforcement decision surface already exists as merged pure cores: F023 `Enforcement` derives a
finding's *effective severity* and its *reason* from the four dials `(base severity, maturity, run mode,
profile)`; F017 `Findings` classifies unknown governed paths; F015 `Routing` classifies a path as routine
(out-of-scope / unmatched), routed (fenced into a domain), or on a protected surface; F024 `Ship` rolls a
routed change up into a `Pass`/`Fail` verdict with the no-hide three-way partition; and F025 `AuditJson`
projects that verdict to the versioned `audit.json` document.

What does **not** yet exist is a *durable, human-auditable, regenerable record* of what every dial
combination actually produces. Today the behavior of the dials is asserted by scattered example-based and
property tests, but there is no single committed artifact a maintainer (or reviewer, or external consumer)
can open to answer "what does `block-on-ship` maturity do under `verify` mode at the `strict` profile when
the base severity is blocking?" — and, critically, no artifact whose byte-for-byte stability *fails the
build* the moment any dial's behavior changes by accident.

This feature produces that record: a **golden enforcement truth table** — a deterministic, byte-stable,
committed fixture that enumerates the full cross-product of the enforcement dials and pins each
combination's effective-enforcement outcome and explanation, plus a small set of **representative
`audit.json` snapshots** for the combinations where a dial flips a finding between blocking and
non-blocking. The truth table is *generated* (so it is exhaustive and never hand-maintained), *committed*
(so it is reviewable in a diff), and *drift-guarded* by a test that regenerates it and asserts byte
equality with the committed copy — making any unintended change to the enforcement semantics impossible to
merge silently.

The enforcement dials this feature must cover, all already typed by merged cores:

- **Base severity** — `advisory` | `blocking` (F023 `Severity`).
- **Rule maturity** — `observe` | `warn` | `block-on-pr` | `block-on-ship` | `block-on-release`
  (F014 `Maturity`, surfaced through F023).
- **Run mode** — `sandbox` | `inner` | `focused` | `verify` | `gate` | `release` (F023 `RunMode`).
- **Governance profile** — `light` | `standard` | `strict` | `release` (F023 `Profile`).
- **Route class** — *routine* (out-of-scope / unmatched-in-root, selects nothing, never default-deny)
  versus *fenced* (routed into a domain / protected surface) versus *unknown governed path* (F017
  `UnmatchedInRoot` finding inside a governed root). This is the "routine versus fenced routes" and
  "unknown governed paths" dimension of the plan row.

This feature **adds no CLI, computes no new enforcement semantics, and changes no merged core**. It is a
*coverage and evidence* deliverable: it enumerates and renders the outcomes the existing cores already
produce, deterministically, into committed golden artifacts and the test that guards them. If producing the
table requires a small, pure enumeration/rendering helper, that helper only *composes* the existing cores
(F023/F017/F024/F025) over the dial cross-product — it introduces no new decision, floor, or token.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Pin every enforcement dial combination in a golden truth table (Priority: P1)

A Governance maintainer needs to see, in one reviewable artifact, the effective-enforcement outcome and
the explanation for **every** combination of the enforcement dials, and needs the build to fail if any of
those outcomes ever changes without an intentional, reviewed edit to that artifact.

**Why this priority**: This is the core of the plan row and the Phase-5 exit criterion "every enforcement
dial has fixture coverage." Without it, the enforcement semantics are only implicitly covered by scattered
tests, and a silent regression (a flipped floor, a reworded reason, a dropped maturity case) can merge
undetected. The committed table is the MVP: on its own it makes the enforcement surface auditable and
drift-proof.

**Independent Test**: Generate the truth table from the existing cores, commit it, then run the
drift-guard test — it regenerates the table and asserts byte equality with the committed file. Mutating any
dial mapping in F023 (or deleting a row) makes the test fail with a readable diff; reverting makes it pass.
The table can be opened and read to confirm every documented dial value appears.

**Acceptance Scenarios**:

1. **Given** the four primary enforcement dials (base severity × maturity × run mode × profile),
   **When** the truth table is generated, **Then** it contains exactly one row per combination of the
   full cross-product (every base severity, every maturity, every run mode, every profile), with no
   combination missing and no combination duplicated.
2. **Given** a generated truth-table row, **When** it is read, **Then** it shows the input dial values,
   the derived effective severity, and the human-readable reason that names which levers produced the
   outcome — matching exactly what F023 `deriveEffectiveSeverity` returns for those inputs.
3. **Given** the committed golden truth table, **When** the drift-guard test regenerates the table from
   the current cores, **Then** the regenerated bytes equal the committed bytes exactly; identical inputs
   always produce an identical table (deterministic, no clock / host path / ordering influence).
4. **Given** an intentional change to an enforcement dial mapping, **When** the truth table is regenerated
   without updating the committed copy, **Then** the drift-guard test fails and the diff shows precisely
   which rows changed.
5. **Given** the route-class dimension (routine vs fenced vs unknown governed path), **When** the
   fixtures are read, **Then** they show that a routine (out-of-scope / unmatched) path selects no gates
   and triggers no default-deny, a fenced path routes into its domain's gates, and an unknown path inside
   a governed root produces an explicit finding — covering the "routine versus fenced routes" and
   "unknown governed paths" coverage the plan row requires.

---

### User Story 2 - Snapshot the audit.json view of every blocking-altering combination (Priority: P2)

A reviewer reading a `ship` decision in CI needs confidence that the `audit.json` document faithfully and
stably renders the combinations where a dial actually flips a finding's blocking status — and that a
profile relaxing a blocker never hides the underlying verdict.

**Why this priority**: This closes the second Phase-5 row and the exit criterion "profile-adjusted blocking
is explained without changing rule truth." It depends on the truth table (P1) to identify *which*
combinations alter blocking, so it is sliced second. It is independently valuable: even without the full
truth table, representative `audit.json` snapshots make the merge-boundary contract reviewable.

**Independent Test**: For a small, named set of scenarios that each flip blocking via a different dial
(maturity withholds; profile tightens the floor; mode reaches/does-not-reach the boundary; base-advisory
stays advisory), project a `ShipDecision` to `audit.json` (F025) and assert byte equality with a committed
snapshot. Changing the projection or the verdict shape fails the matching snapshot.

**Acceptance Scenarios**:

1. **Given** a combination where maturity (`observe`/`warn`) withholds blocking, **When** its `audit.json`
   snapshot is produced, **Then** the snapshot shows the finding as non-blocking and carries both the base
   and effective severity plus the withholding reason (the no-hide rule).
2. **Given** a combination where a stricter profile pulls the blocking floor down so a finding that was
   relaxed now blocks (and the inverse), **When** the two `audit.json` snapshots are produced, **Then**
   each renders the finding in the correct partition (blockers vs warnings) and each carries the six-field
   enforcement detail explaining the flip.
3. **Given** any blocking-altering snapshot, **When** it is regenerated from the current cores, **Then**
   the regenerated bytes equal the committed bytes exactly (deterministic, versioned `audit.json` schema).
4. **Given** the set of snapshots, **When** they are read together, **Then** every dial that can flip
   blocking status is represented by at least one snapshot, so no blocking-altering lever is uncovered.

---

### Edge Cases

- **Unreachable / saturated combinations**: maturity floors interact with the profile tighten amount and
  the clamped run-mode ordinal (`0..5`). The table MUST still enumerate these combinations and show their
  actual outcome (e.g. `block-on-release` under `release` profile saturating at the top of the mode
  ordinal), rather than omitting them as "can't happen."
- **Base-advisory never escalates**: a base-advisory finding stays advisory under every mode/profile/
  maturity; the table MUST show this explicitly so the "profiles never escalate truth" property is visible,
  not assumed.
- **Routine path under the strictest dials**: an out-of-scope / unmatched-in-root routine path MUST select
  nothing and never default-deny even under `release` mode + `release` profile — the table MUST make this
  visible to guard against a future default-deny regression.
- **Reason-text stability**: a reworded reason string is a behavior change for consumers that read it; the
  drift guard MUST treat reason text as part of the pinned outcome, not as free-form commentary.
- **Cross-product size growth**: if a future feature adds a dial value (a new mode, profile, or maturity),
  the generated table grows automatically and the drift guard fails until the committed copy is regenerated
  — the artifact MUST not silently under-cover a newly added dial value.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST produce a golden enforcement truth table that enumerates the complete
  cross-product of the four primary enforcement dials — base severity (2) × maturity (5) × run mode (6) ×
  profile (4) — with exactly one row per combination, no combination missing and none duplicated.
- **FR-002**: Each truth-table row MUST record the input dial values, the derived effective severity, and
  the reason text, and these MUST equal exactly what the merged F023 `Enforcement.deriveEffectiveSeverity`
  returns for those inputs (the feature reuses the core; it MUST NOT re-derive or restate the semantics).
- **FR-003**: The truth table MUST additionally cover the route-class dimension required by the plan row —
  routine (out-of-scope / unmatched-in-root) versus fenced (routed into a domain / protected surface)
  versus unknown governed path — sourced from the merged F015 `Routing` / F017 `Findings` cores, showing
  that routine paths select nothing and never default-deny, fenced paths route into domain gates, and
  unknown governed paths produce explicit findings.
- **FR-004**: Generation MUST be pure, total, and deterministic: identical inputs MUST yield a
  byte-for-byte identical table, with no influence from the clock, host paths, environment, or
  enumeration order; the generator MUST never throw over the full cross-product.
- **FR-005**: The generated truth table MUST be committed to the repository as a reviewable artifact (a
  human-readable, diff-friendly tabular text document) so a reader can audit every dial combination
  without running code.
- **FR-006**: A drift-guard test MUST regenerate the truth table from the current cores and assert
  byte-equality with the committed artifact, failing with a readable diff when any row's inputs, effective
  severity, or reason text changes, and passing when the committed copy matches.
- **FR-007**: The feature MUST produce representative `audit.json` snapshots for the combinations that
  alter blocking status — at minimum: maturity withholds (`observe`/`warn`); base-advisory stays advisory;
  profile tightening flips a finding into/out of blocking; and run mode reaching versus not reaching the
  blocking floor.
- **FR-008**: Each `audit.json` snapshot MUST be produced by projecting a `ShipDecision` through the merged
  F025 `AuditJson.ofShipDecision` (byte-for-byte, the existing versioned `fsgg.audit/v1` schema) and MUST
  be committed and guarded by a byte-equality snapshot test; the feature MUST NOT introduce a new or
  altered audit schema.
- **FR-009**: Every committed `audit.json` snapshot for a relaxed-blocker case MUST show the finding in the
  correct partition and carry both base and effective severity plus the reason (the design's no-hide rule),
  demonstrating that profile adjustment never hides the underlying verdict or alters rule identity.
- **FR-010**: The set of blocking-altering snapshots MUST collectively cover every dial that can flip a
  finding's blocking status, so no blocking-altering lever is left without a representative snapshot.
- **FR-011**: The feature MUST add no CLI command, no new third-party dependency, and MUST NOT modify the
  merged F014/F015/F017/F023/F024/F025 cores; any helper it adds only composes those cores over the dial
  cross-product and is covered by the constitution's `.fsi`/surface-baseline rules if it is public.
- **FR-012**: When a future change adds a dial value (a new mode, profile, or maturity), the generated
  table MUST grow to include it automatically and the drift guard MUST fail until the committed copy is
  regenerated, so the artifact can never silently under-cover an enforcement dial.

### Key Entities

- **Enforcement truth table**: the committed, deterministic, human-readable record of the full enforcement
  dial cross-product. Each entry pairs the input dials (base severity, maturity, run mode, profile, and the
  route-class dimension) with the derived effective severity and the reason that explains it.
- **Truth-table row**: one combination of dial values and its pinned outcome (effective severity + reason).
  The unit the drift guard compares byte-for-byte.
- **Blocking-altering scenario**: a named combination where a single dial flips a finding between blocking
  and non-blocking, used to seed a representative `audit.json` snapshot.
- **audit.json snapshot**: the committed, versioned `fsgg.audit/v1` document for a blocking-altering
  scenario, carrying the no-hide three-way partition with full per-item enforcement detail.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The committed truth table contains exactly one row for every combination of base severity ×
  maturity × run mode × profile (the complete primary cross-product), with zero missing and zero duplicate
  combinations — verifiable by counting rows against the product of the dial cardinalities.
- **SC-002**: Regenerating the truth table from the current cores yields bytes identical to the committed
  artifact on every run (100% reproducible); two independent regenerations never differ.
- **SC-003**: Any unintended change to an enforcement dial's behavior — a flipped effective severity or a
  reworded reason for any single combination — is caught by the drift-guard test (the build fails) rather
  than merging silently.
- **SC-004**: Every dial that can flip a finding's blocking status is represented by at least one committed
  `audit.json` snapshot, and each relaxed-blocker snapshot shows both base and effective severity (the
  no-hide rule is demonstrated, not assumed).
- **SC-005**: A maintainer can determine the effective-enforcement outcome and the explaining reason for
  any dial combination by reading the committed table alone, without executing any code.
- **SC-006**: The Phase-5 exit criteria "every enforcement dial has fixture coverage" and "profile-adjusted
  blocking is explained without changing rule truth" are both demonstrably met by the committed artifacts.

## Assumptions

- **Scope = both remaining Phase-5 rows, fixtures only.** This feature delivers the golden truth table
  (row 1, P1) and the representative `audit.json` snapshots (row 2, P2) as a single paired slice, because
  the snapshots are the JSON view of the truth table's blocking-altering rows. It adds no new enforcement
  semantics, no CLI, and no schema.
- **Reuse-only over the merged cores.** The enforcement outcomes come from the already-merged F023
  derivation, F015 routing, F017 findings, F024 ship rollup, and F025 audit projection — verbatim. This
  feature enumerates and renders what those cores produce; it does not re-implement, re-derive, or alter
  any of them. (Consistent with the repo's pure-core-first rhythm: this is the *fixtures* row that consumes
  the cores those rows already landed.)
- **Implementation slicing is a plan-time reconciliation.** Whether the generator is a small new pure core
  (with its own `.fsi`/surface baseline) or lives inside the relevant test project, the exact committed
  file homes (e.g. under the feature's `readiness/` evidence vs a `fixtures/` directory vs both), and the
  precise tabular text format are HOW decisions deferred to `/speckit-plan` — as every prior feature in
  this repo settled them there. The spec fixes only the WHAT: exhaustive, deterministic, committed,
  drift-guarded.
- **Change classification.** Expected **Tier 1** *iff* the feature introduces a public helper module (it
  would then require an `.fsi` and a surface-area baseline per the constitution); **Tier 2** if it is
  realized purely as committed fixtures plus tests with no new public surface. The plan resolves which.
- **Per-class policy dials remain out of scope.** Project-authored per-class profile overrides from
  `.fsgg/policy.yml` (the `unknownPaths`/`staleEvidence`/… strictness map) were explicitly deferred by
  F023 and remain out of scope here; the four canonical profiles carry the design's documented strictness.
- **Cost / cache / freshness remain Phase 11.** The truth table pins effective severity and the reason; it
  does not evaluate cost tiers or cache/freshness eligibility, which the design and prior features defer to
  Phase 11.
- **No dogfooding of `fsgg ship` on this repo.** Consistent with F027's maintainer-confirmed boundary,
  this feature ships fixtures and tests; it does not wire a live enforcement gate onto this repository's
  own `main`.
