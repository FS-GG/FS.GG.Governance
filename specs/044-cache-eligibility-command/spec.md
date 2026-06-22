# Feature Specification: Cache-Eligibility Host Command (Sense → Resolve → Evaluate → Emit)

**Feature Branch**: `044-cache-eligibility-command`

**Created**: 2026-06-22

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved to the one
remaining piece of the in-progress route/audit cache-eligibility row in
`docs/initial-implementation-plan.md` (lines 437–440): the **host wiring** — "the CLI edge that actually
*senses* each gate's facts from the real repo (git/filesystem), supplies them as `SensedFacts` to F043
`resolve`, runs F041 `evaluate` over the resolved candidates, and emits/embeds the verdict into the
route/audit artifacts." Scoped (maintainer-confirmed at specify time) to the **full host pipeline that
emits a standalone artifact** — sense → F043 `resolve` → F041 `evaluate` → emit a deterministic,
versioned `cache-eligibility.json` (F042's projection, reused verbatim) — deferring only the embed of the
verdict *into* `route.json` / `audit.json` to a later row.

## Overview

Every pure core the cache-eligibility thread needs now exists and is merged: the freshness-key value and
reuse predicate (F029), the evidence-reuse decision (F030), the per-gate cache-eligibility roll-up
(F041 `CacheEligibility.evaluate`), the deterministic `cache-eligibility.json` projection
(F042 `CacheEligibilityJson.ofReport`), and — most recently — the per-gate freshness-inputs resolution
join (F043 `FreshnessResolution.resolve`), which turns each selected gate's carried five-field
`FreshnessKey` identity plus a bundle of **already-sensed** repository facts into a complete
`FreshnessInputs` (or a no-hide `Unresolved` outcome) and whose `candidate` bridge feeds resolved gates
straight into F041 without adaptation.

What no command yet does is **sense those facts from a real repository and run the pipeline end to end**.
Every cache-eligibility row so far has been a pure value or an isolated join over *supplied* inputs;
nothing has ever read a working tree, hashed a rule pack, sensed a generator version, hashed a gate's
covered artifacts, established the change's base/head revisions, assembled them into the
`FreshnessResolution.SensedFacts` bundle F043 expects, resolved each selected gate, evaluated cache
eligibility over the resolved candidates, and written the verdict where a person or CI can read it.

This feature is that first composition for the cache-eligibility thread: the **cache-eligibility host
command**. Pointed at a repository for a routed change, it (1) senses which gates the change selects —
reusing the exact scope-sensing, catalog-loading, routing, and gate-selection the `fsgg route` command
(F022) already established; (2) **senses each selected gate's freshness facts** from the real repo at the
effects boundary — base/head revisions, the rule-pack hash, the generator version, each gate's
covered-artifact hashes, and each declared command's command version; (3) assembles them into a
`SensedFacts` bundle and calls F043 `resolve` to get, per selected gate, a complete `FreshnessInputs` or
a no-hide `Unresolved` outcome; (4) runs F041 `evaluate` over the resolved candidates against the loaded
evidence-reuse store; (5) projects the result through F042 to a deterministic, versioned **standalone
`cache-eligibility.json`** document and persists it to disk; and (6) prints a deterministic human or JSON
summary of which gates may reuse evidence and which must recompute (and why), including any gate whose
inputs could not be resolved.

It composes already-typed, already-tested values; it **re-derives, re-sorts, re-classifies, and
re-evaluates nothing** that a merged pure core already owns. Crucially, cache eligibility is
**information, not a verdict**: the command reports which gates may reuse prior evidence, but it does not
decide whether a change may merge, assign severity / profile / mode / enforcement, set an exit-code basis
from blockers, or write any provenance. Those are `fsgg ship` / `audit.json` / Phase 5 / the Release
phase. The command succeeds (exit 0) whenever it can sense the repo, load a valid catalog, and write the
artifact — a gate that "must recompute" or whose inputs are "unresolved" is information, never a tool
failure.

It is deliberately **standalone**. The eventual home of the cache-eligibility verdict is *inside*
`route.json` and `audit.json` (the design says `route.json` carries "cache eligibility"), but embedding
it there means editing the merged F020 / F025 projection cores and their committed surface baselines.
This row leaves those untouched and emits a separate `cache-eligibility.json` sibling, exactly as F042
delivered the projection standalone rather than as an edit to route/audit. The embed is the next row.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Emit the cache-eligibility verdict for the current change (Priority: P1)

A developer (or a CI step) runs the cache-eligibility command at the root of a repository that has a
declared `.fsgg` catalog and some changed files. They get back, written to disk, a `cache-eligibility.json`
that names — for every gate this change selects — whether that gate's prior evidence may be **reused** (and
the opaque evidence reference) or **must be recomputed** (and why: no prior evidence, or exactly which
freshness inputs changed), plus a readable summary on the terminal.

**Why this priority**: This is the MVP and the whole point of the row — the first time the cache-eligibility
pure cores produce a real, on-disk, machine-readable verdict for an actual change. Without it the cores are
inert over real repositories; with it CI can see which gates a change lets it skip and which it must rerun.

**Independent Test**: In a temporary git repository with a minimal valid `.fsgg` catalog, a small change,
and an evidence-reuse store that makes at least one selected gate reusable, run the command and confirm the
written `cache-eligibility.json` validates against `fsgg.cache-eligibility/v1`, lists exactly the selected
gates in `GateId` order, and marks the prepared gate `reusable` and the others `mustRecompute` with the
correct cause — all without consulting any other artifact.

**Acceptance Scenarios**:

1. **Given** a repo with a valid catalog, a change selecting gates G1 and G2, sensable freshness facts for
   both, and a reuse store whose newest matching entry covers G1, **When** the command runs, **Then** it
   writes a `cache-eligibility.json` marking G1 `reusable` (with its evidence reference) and G2
   `mustRecompute` with a no-hide cause, in `GateId` order, and exits 0.
2. **Given** the same repo with **no** evidence-reuse store present, **When** the command runs, **Then**
   every selected gate whose inputs resolve is `mustRecompute` with cause `noPriorEvidence` (recompute by
   default), and the command still exits 0.
3. **Given** a change that selects **no** gates (e.g. only unclassified or out-of-scope paths changed),
   **When** the command runs, **Then** it writes a valid, empty-entry `cache-eligibility.json` and exits 0.

---

### User Story 2 - Honest unresolved attribution, recompute by default (Priority: P2)

When a freshness fact required to resolve a selected gate cannot be sensed from the repository — the
rule-pack hash is unavailable, a covered artifact is missing, a command's version cannot be sensed, the
generator version is unknown, or base/head cannot be established — the command never fabricates, defaults,
or zero-fills a freshness input to claim reusability. The affected gate is reported as **recompute by
default**, naming exactly and only the facts that were missing (no-hide), and is never shown as reusable.

**Why this priority**: Safety. The entire cache-eligibility thread is recompute-by-default and no-hide; a
host edge that quietly invented a missing hash to mark a gate reusable would let CI skip a check it should
have rerun. This story guarantees the host carries F043's honesty contract all the way to the artifact.

**Independent Test**: In a temporary repo where one selected gate's covered artifact is absent (so its
covered-artifact hash cannot be sensed), run the command and confirm that gate is reported recompute-by-
default with the missing fact named, the other gates are unaffected, and no gate is marked reusable on the
strength of a fabricated input.

**Acceptance Scenarios**:

1. **Given** a selected gate whose required sensed fact is unavailable, **When** the command runs, **Then**
   the gate is reported recompute-by-default with the exact missing fact(s) named and is absent from the
   reusable set.
2. **Given** a gate that declares **no** command, **When** the command runs, **Then** the gate resolves
   with absent command and absent command version (consistent absence) and is evaluated for reuse normally —
   never reported unresolved on the basis of the absent command.
3. **Given** a gate whose covered-artifact set is sensed but **empty**, **When** the command runs, **Then**
   the gate resolves (sensed-empty is not unsensed) and is evaluated for reuse, distinct from a gate whose
   covered artifacts were never sensed.

---

### User Story 3 - Deterministic, reproducible artifact (Priority: P3)

Running the command twice over the same repository state — from a different working directory, in a
different process, at a different wall-clock time — produces a byte-identical `cache-eligibility.json`. The
document depends only on the sensed repository facts and the loaded reuse store, never on ambient order,
location, or clock.

**Why this priority**: The artifact is meant for CI diffing, caching, and agent consumption; non-determinism
would make it useless as a stable signal and would violate the byte-stability contract every projection in
this repo upholds.

**Independent Test**: Run the command twice over the same fixed repo state from two different working
directories and confirm the two `cache-eligibility.json` files are byte-identical.

**Acceptance Scenarios**:

1. **Given** a fixed repository state, **When** the command runs twice from different working directories,
   **Then** the two written `cache-eligibility.json` files are byte-for-byte identical.
2. **Given** the same selected gates supplied to routing in a different discovery order, **When** the
   command runs, **Then** the report's per-gate entries appear in the same `GateId` order (order-independent).

---

### Edge Cases

- **No catalog / invalid catalog**: the command reports the catalog error clearly and writes **no partial
  artifact**, exiting with a non-zero tool-failure code (distinct from "must recompute", which is exit 0).
- **Not a git repository / scope cannot be sensed**: the command reports the sensing failure and writes no
  artifact, exiting non-zero — it never emits a fabricated or empty-by-accident document.
- **No prior evidence store**: treated as an empty store — every resolvable gate is `mustRecompute`
  (`noPriorEvidence`); this is a success, not an error.
- **Duplicate selected gates** (same `GateId` selected via multiple paths): preserved as duplicate entries
  in the report's `GateId` order, never merged or dropped (the F041/F043 completeness contract).
- **A gate selected but with no resolvable inputs**: surfaced as recompute-by-default with named missing
  facts; never silently dropped and never marked reusable.
- **Existing `cache-eligibility.json` on disk**: overwritten atomically with the freshly computed document
  (no merge with stale content).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The command MUST sense the routed change for a repository — reusing the established scope
  sensing, `.fsgg` catalog loading and validation, path→capability routing, gate-registry assembly, and
  per-change gate selection that the `fsgg route` host edge (F022) already provides — to obtain the set of
  selected gates for the change.
- **FR-002**: The command MUST sense, at the host effects boundary (git / filesystem / build metadata,
  behind an injected port), each freshness fact F043 requires that is not carried on the gate's
  `FreshnessKey`: the change's base and head revisions, the rule-pack hash, the generator version, each
  selected gate's covered-artifact hashes, and each declared command's command version.
- **FR-003**: The command MUST assemble the sensed facts into a `FreshnessResolution.SensedFacts` bundle and
  call F043 `resolve` over the selected gates — fabricating, defaulting, or zero-filling **no** freshness
  input. A fact that was sensed-but-empty (e.g. an empty covered-artifact set) MUST be distinguished from a
  fact that was never sensed.
- **FR-004**: For every gate F043 resolves to a complete `FreshnessInputs`, the command MUST form the F041
  candidate (via F043's `candidate` bridge) and run F041 `evaluate` over the resolved candidates against
  the loaded evidence-reuse store, producing a per-gate `Reusable` / `MustRecompute` verdict.
- **FR-005**: For every gate F043 reports `Unresolved`, the command MUST surface it as **recompute by
  default**, naming exactly and only the missing freshness facts (no-hide), and MUST NOT mark it reusable or
  silently drop it.
- **FR-006**: The command MUST load the evidence-reuse store as a **read-only** input sensed from a declared
  on-disk location, treating an absent store as the empty store (so resolvable gates default to
  `mustRecompute` / `noPriorEvidence`). Recording new evidence after a run, and evidence eviction/expiry,
  are explicitly **out of scope** for this row.
- **FR-007**: The command MUST render the resolved gates' result through the F042 projection
  (`CacheEligibilityJson`, reused verbatim) to a deterministic, versioned `cache-eligibility.json` document
  (schema `fsgg.cache-eligibility/v1`) and persist it to a declared on-disk location, plus print a
  deterministic human-or-JSON summary distinguishing reusable, must-recompute, and
  recompute-by-default-unresolved gates.
- **FR-008**: The written artifact and printed summary MUST be **deterministic and byte-stable**: identical
  repository state and reuse store produce byte-identical output regardless of working directory, process,
  ambient ordering, or wall-clock time. Any sensed wall-clock value that must appear MUST be marked as
  sensed metadata (F034) and excluded from the document's reproducible content.
- **FR-009**: The command MUST treat cache eligibility as **information**, not a merge verdict: it exits 0
  whenever it can sense the repo, load a valid catalog, and write the artifact. A gate that must recompute,
  or whose inputs are unresolved, is never a tool failure. The command assigns **no** severity, profile,
  mode, enforcement, ship verdict, exit-code-from-blockers, or provenance.
- **FR-010**: On a sensing or catalog failure (not a git repo, missing/invalid catalog, unwritable output
  path), the command MUST report the failure clearly, write **no partial artifact**, and exit with a
  non-zero tool-failure code kept distinct from the (exit-0) "must recompute" outcome.
- **FR-011**: The command MUST NOT embed the cache-eligibility verdict into `route.json` or `audit.json`,
  and MUST NOT modify the merged F020 (`route.json`) or F025 (`audit.json`) projection cores or their
  committed surface baselines. The embed is a later row.
- **FR-012**: The command MUST reuse the merged pure cores (F018 gates/selection, F019 route, F029
  freshness inputs, F030 evidence reuse, F041 evaluate, F042 projection, F043 resolve) **verbatim**,
  computing no freshness key, no hash, and no cache decision of its own outside those cores; the work is
  added additively (a new host edge + its tests) without altering existing `src/`, `surface/`, or merged
  test projects.
- **FR-013**: The command MUST compute **no hash, freshness key, or digest itself** — hashing/sensing
  happens only at the injected effects boundary that supplies already-sensed values; the pure resolution
  and evaluation consume those opaque values without re-deriving them.

### Key Entities *(include if feature involves data)*

- **Selected gates (for the change)**: the gates the routed change reaches (F019 / F018), each carrying its
  five-field `FreshnessKey` identity (check, domain, cost, environment, command). The host obtains these by
  reusing the F022 route composition; this row adds no new selection logic.
- **Sensed freshness facts**: the repository facts sensed at the host boundary to complete each gate's
  freshness inputs — base/head revisions, rule-pack hash, generator version, per-gate covered-artifact
  hashes, per-command command versions — assembled into F043's `SensedFacts` bundle (option/Map shape where
  key-present means *sensed even if empty* and key-absent means *not sensed*).
- **Evidence-reuse store**: the read-only F030 store of prior recorded evidence, loaded from a declared
  on-disk location (empty when absent); the input F041 `evaluate` consults to decide reuse.
- **Cache-eligibility artifact (`cache-eligibility.json`)**: the standalone, versioned
  (`fsgg.cache-eligibility/v1`) document produced by the F042 projection — one entry per selected resolved
  gate in `GateId` order, each `reusable` (opaque evidence reference) or `mustRecompute` (no-hide cause).
- **Unresolved attribution**: the per-gate record of gates whose inputs could not be sensed — surfaced as
  recompute-by-default with the exact missing facts named, never as reusable.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Pointed at a repository with a valid catalog and a change selecting gates, the command writes
  a `cache-eligibility.json` that validates against `fsgg.cache-eligibility/v1` and contains exactly one
  entry per selected resolved gate in `GateId` order, with no selected gate dropped or merged.
- **SC-002**: When a reuse store makes a selected gate reusable, that gate is reported `reusable` with its
  evidence reference; every other resolvable selected gate is reported `mustRecompute` with the correct
  no-hide cause (`noPriorEvidence` or exactly the changed input categories).
- **SC-003**: When a required freshness fact cannot be sensed for a selected gate, that gate is reported
  recompute-by-default with exactly and only the missing fact(s) named, and is never reported reusable —
  in 100% of such cases (no fabricated input ever yields a reusable verdict).
- **SC-004**: Running the command twice over the same repository state from different working directories,
  processes, and wall-clock times yields byte-identical `cache-eligibility.json` files.
- **SC-005**: A gate that declares no command resolves and is evaluated with absent command and absent
  command version, never reported unresolved on that basis; a sensed-empty covered-artifact set resolves,
  distinct from an unsensed one.
- **SC-006**: The command exits 0 whenever it senses the repo, loads a valid catalog, and writes the
  artifact — including when every gate must recompute or some gates are unresolved; it exits non-zero only
  on a genuine sensing/catalog/write failure, writing no partial artifact in that case.
- **SC-007**: No existing `src/`, `surface/`, or merged test project is modified; the full solution builds
  clean and all previously-green tests stay green (additive-only).
- **SC-008**: The merged F020 `route.json` and F025 `audit.json` cores, their projections, and their
  surface baselines are unchanged — the cache-eligibility verdict is emitted only as the standalone
  `cache-eligibility.json` sibling.

## Assumptions

- **CLI surface**: the host edge is delivered as a dedicated cache-eligibility command (a new host project
  mirroring the merged `RouteCommand` / `ShipCommand` pure-MVU-core + injected-ports shape), rather than by
  modifying the merged `fsgg route` command. The exact command verb and flag names (e.g. paths / since-rev /
  base-head scope selection, and the output/store paths) are a plan-time decision; the spec fixes the
  behavior, not the spelling.
- **Scope sensing reuse**: the change scope, catalog loading/validation, routing, registry assembly, and
  gate selection are reused from the F022 route composition; this row adds only the freshness-fact sensing,
  the F043 resolve call, the F041 evaluate call, the read-only store load, and the F042 emission.
- **Unresolved representation**: gates F043 reports `Unresolved` are not representable in F041's
  `CacheEligibilityReport` (they have no `FreshnessInputs`), so they are surfaced as recompute-by-default
  with their named missing facts in the command's summary and (if needed) a companion section/field of the
  output; the exact on-disk representation of unresolved gates is a plan/clarify decision, but they MUST be
  surfaced honestly and never marked reusable. The F042-rendered `cache-eligibility.json` itself carries
  only the resolved gates' verdicts (its existing `fsgg.cache-eligibility/v1` schema, unchanged). *(Plan-time
  resolution: unresolved gates are surfaced in a companion sidecar file `cache-eligibility.unresolved.json`,
  schema `fsgg.cache-eligibility.unresolved/v1`, written next to `cache-eligibility.json` — see
  `contracts/cache-eligibility-artifacts.md §A2`. This is additive and does not touch the F042 schema/core.)*
- **Command-version sensing**: where a declared command's version cannot be sensed cheaply at the host
  boundary, the corresponding fact is left unsensed and the gate resolves unresolved on that basis (no-hide)
  rather than the command invoking arbitrary tools; richer command-version sensing can be a later refinement.
- **Reuse store location & format**: the read-only evidence-reuse store is loaded from a declared on-disk
  location in a format the host can deserialize into F030's `ReuseStore`; defining/persisting that store
  (writing evidence, eviction, expiry) is out of scope and deferred to the cache-storage row. *(Plan-time
  resolution: default location `<repo>/readiness/evidence-reuse.json` via `--store`; read format fixed as
  `fsgg.evidence-reuse-store/v1` — see `contracts/cache-eligibility-artifacts.md §A5`. The store **writer** is
  the deferred cache-storage row.)*
- **Standalone, not embedded**: the verdict is emitted only as `cache-eligibility.json`; wiring it into
  `route.json` / `audit.json` is the explicitly-deferred next row, and `fsgg verify` / `fsgg release`
  (Phase 13) remain out of scope.
</content>
