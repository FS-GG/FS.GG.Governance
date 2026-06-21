# Phase 0 Research: Golden Enforcement Truth-Table Fixtures

This row's spec deferred three HOW decisions to plan-time and they were resolved with the maintainer on
2026-06-21 (D1–D3 below). The remaining entries (D4–D7) record the dial-enumeration and core-reuse facts
the generator depends on, all read directly from the merged `.fsi` contracts so the fixtures pin what the
cores actually return — not a restatement.

## D1 — Generator home & change tier

**Decision**: The generator is **test-support code inside a new test project**
`tests/FS.GG.Governance.EnforcementFixtures.Tests`. No new `src/` module, no `.fsi`, no
`surface/*.surface.txt` baseline. This is a **Tier 2** change.

**Rationale**: The spec explicitly states this feature "adds no CLI, computes no new enforcement semantics,
and changes no merged core" — it only composes existing cores over the dial cross-product and renders the
result. Constitution Principle III (idiomatic simplicity; the plainest thing that solves the problem)
argues against creating a public module + `.fsi` + surface baseline that **nothing in the product
consumes**. FR-011 already anticipates this: a helper is "covered by the constitution's `.fsi`/surface
rules *if it is public*" — leaving non-public realization valid. Treating it as test-support keeps the
public surface (and its drift baselines) exactly as the merged cores left it.

**Alternatives considered**: A new pure core `FS.GG.Governance.EnforcementFixtures` with curated `.fsi` +
surface baseline (Tier 1). Rejected: it would add public API surface for a generator no other code calls,
contradicting the spec's coverage/evidence framing and Principle III. The repo's per-feature pure-core
rhythm exists because prior rows computed **new semantics**; this row computes none.

## D2 — Committed artifact home

**Decision**: Goldens live under the repo's existing top-level `fixtures/` directory at
`fixtures/enforcement/` — `truth-table.md` plus an `audit-snapshots/` directory of `*.audit.json` files.

**Rationale**: `fixtures/` already exists (`fixtures/008-effects/`), so this row extends an established
home. A durable, build-guarding artifact should not be tied to a spec folder's lifecycle; a top-level
`fixtures/` path is stable for the drift guard's `findRepoRoot`-relative read and obvious to a reviewer.

**Alternatives considered**: Committing under `specs/028-…/fixtures/`. Rejected: it couples a permanent
build-failing guard to a per-feature spec directory and is less discoverable as a repo-wide enforcement
record.

## D3 — Truth-table text format

**Decision**: A GitHub-renderable **Markdown pipe table** (`| … |` with a `|---|` header rule), `\n`
newlines, no trailing whitespace, UTF-8 without BOM.

**Rationale**: SC-005 requires a maintainer to determine any combination's outcome **by reading the
committed table alone**; a Markdown table renders directly on GitHub and in any viewer and diffs cleanly
per-row. The byte overhead of padding/separators is irrelevant to determinism, which is guaranteed by fixed
column order, fixed row order, and fixed newline/encoding rules (see contracts/truth-table-format.md).

**Alternatives considered**: TSV (more compact, less human-pretty) and fixed-width aligned text (pretty but
fragile to column-width recomputation). Both are byte-stable; Markdown wins on direct human auditability,
which is the artifact's primary purpose.

## D4 — The dial enumerations (from the merged `.fsi`)

**Decision**: The generator iterates these closed, ordered lists in this fixed order (least → most
protective/strict), exactly as `FS.GG.Governance.AuditJson.Tests/Support.fs` already enumerates them:

- **Base severity** (`Enforcement.Severity`, 2): `Advisory`, `Blocking`.
- **Maturity** (`Config.Model.Maturity`, 5): `Observe`, `Warn`, `BlockOnPr`, `BlockOnShip`,
  `BlockOnRelease`.
- **Run mode** (`Enforcement.RunMode`, 6): `Sandbox`, `Inner`, `Focused`, `Verify`, `Gate`, `Release`.
- **Profile** (`Enforcement.Profile`, 4): `Light`, `Standard`, `Strict`, `Release`.

Primary cross-product cardinality = 2 × 5 × 6 × 4 = **240 rows** (SC-001 count check). `RunMode.Release`
and `Profile.Release` are name-qualified in the iteration lists because both DUs define a `Release` case
(the Support.fs precedent).

**Rationale**: These are the four dials `deriveEffectiveSeverity` takes (`EnforcementInput`); enumerating
their closed value sets is the complete, total cross-product the spec requires. Fixed least→most order
makes the table read as a monotone progression and pins row order for the guard.

## D5 — Effective severity + reason come verbatim from F023

**Decision**: Each primary row's `effective severity` and `reason` are taken **directly** from
`Enforcement.deriveEffectiveSeverity { BaseSeverity; Maturity; Mode; Profile }` → `EnforcementDecision`
(fields `EffectiveSeverity`, `Reason`). The generator reformats nothing about the reason and never
re-derives severity (FR-002). `runModeOrdinal` is available if the table chooses to show the clamped
ordinal, but the pinned outcome is the decision's own fields.

**Rationale**: FR-002/SC-003 require the table to equal exactly what the core returns; reading the
`EnforcementDecision` record is the only honest source. The reason string is treated as part of the pinned
outcome (Edge: reason-text stability) so a reworded reason trips the guard.

## D6 — The route-class dimension comes from F015/F017

**Decision**: A small, separate **route-class section** in the same `truth-table.md` shows three rows
sourced from the genuine cores over minimal real `TypedFacts`:

- **Routine** (out-of-scope / unmatched-not-in-root): a path **not** under the governed root →
  `Routing.route` yields `OutOfScope` → `Findings.findUnknownGovernedPaths` yields **no finding** (selects
  nothing, never default-deny) — shown true even under the strictest mode/profile (Edge: routine under
  strictest dials).
- **Fenced**: a path matching a path-map glob → `Routing.route` yields `Routed (domain, glob, reason)` →
  routes into that domain's gates, **no** unknown-path finding.
- **Unknown governed path**: a path under the governed root matching no glob → `Routing.route` yields
  `UnmatchedInRoot` → `Findings.findUnknownGovernedPaths` yields an explicit finding
  (`UnknownGovernedPath` / `GovernedRootUnknown`, or `UnknownProtectedBoundaryPath` /
  `ProtectedBoundaryUnknown` on a declared protected surface).

**Rationale**: FR-003 requires the route-class dimension sourced from F015/F017, demonstrating routine
selects nothing, fenced routes into domain gates, and unknown paths produce findings. This is a separate
section (not part of the 4-dial cross-product) because routing/finding classification is a distinct axis
from the per-finding severity derivation; combining them into one 240×N table would obscure both. The
section uses real, literally-constructible `TypedFacts` (the Routing/Findings test precedent), no mocks.

## D7 — Snapshots come verbatim from F024 → F025

**Decision**: Each `audit.json` snapshot is `AuditJson.ofShipDecision (Ship.rollup route mode profile)` —
a real F019 `RouteResult` (built with the F025 `Support.fs` builders: `mkGate`/`mkSelectedGate`/`mkFinding`/
`mkRoute`) rolled up by the genuine F024 `Ship.rollup`, then projected by the genuine F025
`ofShipDecision`. The feature introduces **no** new or altered audit schema (FR-008): the bytes are exactly
the merged `fsgg.audit/v1` document. The set of scenarios is fixed in contracts/audit-snapshot-set.md, one
per dial that can flip blocking status (maturity withholds; base-advisory stays advisory; profile
relaxes/tightens; mode below/at the floor), satisfying FR-007/FR-010 and the no-hide rule FR-009.

**Rationale**: FR-008 mandates the snapshots be produced by the merged projection byte-for-byte; reusing
the existing real-chain builders means every snapshot is the value a real `fsgg ship`/CI/agent caller
holds, and the determinism the F025 tests already prove carries through. The blocking-altering scenarios
are exactly the rows where the truth table's effective severity differs from the base severity (P2 = the
JSON view of the truth table's flipping rows).

## D8 — Determinism & the re-bless path

**Decision**: Generation is a pure fold over the closed enumerations with no clock/host/env/order input;
output uses `\n` newlines, no trailing whitespace, UTF-8 (no BOM). The drift guard reads the committed file
via `findRepoRoot (DirectoryInfo AppContext.BaseDirectory)` (the `AuditJson.Tests/Support.fs` precedent)
and asserts byte-equality, with an intentional re-bless path gated on `BLESS_FIXTURES=1` that rewrites the
committed file — mirroring the repo's existing `BLESS_SURFACE=1` surface-baseline idiom. Determinism is
independently asserted by generating twice in one test run and comparing (SC-002).

**Rationale**: This reuses the exact patterns already trusted in the repo for surface baselines, so the
guard's failure/bless ergonomics are familiar; FR-004/FR-006/FR-012 and SC-002/SC-003 are all served by
byte-equality plus the count check, and a new dial value grows the table and fails the guard until
re-blessed (Edge: cross-product growth).
