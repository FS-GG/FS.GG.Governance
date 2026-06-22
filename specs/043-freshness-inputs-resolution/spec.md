# Feature Specification: Per-Gate Freshness-Inputs Resolution Core

**Feature Branch**: `043-freshness-inputs-resolution`

**Created**: 2026-06-22

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan" — resolved against
`docs/initial-implementation-plan.md`. Phases 2, 5, 11, and 12 are complete, and the cache-eligibility thread
has landed its per-gate verdict core (F041) and its deterministic `cache-eligibility.json` projection (F042).
The one remaining piece of the route/audit cache-eligibility emission row is the **host wiring**:
*"the CLI edge that resolves each gate's `FreshnessInputs` from the real repo, runs F041 `evaluate`, and
emits/embeds the verdict into the route/audit artifacts."* That wiring is blocked by a **KEY GAP** — a routed
change (F019) carries, per selected gate, only the gate's five-field freshness-**key identity**
(check, domain, cost, environment, command), **not** the full ten-field F029 `FreshnessInputs` that the
evidence-reuse (F030) and cache-eligibility (F041) cores consume. Continuing this repo's maintainer-confirmed
**pure-core-first** rhythm (every prior row landed a pure, total, deterministic core before any host edge or
projection consumed it), this row delivers the missing **pure join** that closes that gap: given a routed
change's selected gates and the repository facts **already sensed elsewhere**, it assembles a complete set of
freshness inputs per selected gate — fabricating nothing — so the later host row can run F041 over the result
and the later projection rows can emit it.

## Overview

F029 defined the ten-field freshness-input value that fingerprints "the world a gate ran in" (check, domain,
command, environment, rule hash, covered-artifact hashes, command version, generator version, base revision,
head revision). F030 decided reuse from those inputs, and F041 rolled that decision up per selected gate into a
cache-eligibility report. But none of those cores can run yet against a real routed change, because the gate a
route selects (F018 / F019) carries only a **five-field freshness-key identity** — check, domain, **cost**,
environment, command — which is the gate's stable identity, **not** the full freshness inputs. Four of those
fields (check, domain, environment, command) are exactly what F029 needs; the gate's **cost** is deliberately
*not* a freshness input (F029 research D5); and the remaining **six** F029 fields — rule hash, covered-artifact
hashes, command version, generator version, base revision, head revision — are facts about the actual
repository state that nothing has supplied to the gate.

This row delivers exactly the **join** that fills that gap, as a pure core:

- It consumes a routed change's **selected gates** (each carrying its stable gate identity and its five-field
  freshness-key identity) together with a bundle of **repository facts already sensed elsewhere** — the
  base/head revisions, rule hash, generator version, and the per-gate covered-artifact hashes and per-command
  command version.
- For each selected gate it **assembles a complete set of freshness inputs** by taking the four identity fields
  from the gate's own carried freshness-key identity (dropping cost) and the six remaining fields from the
  supplied sensed facts — producing a value shaped to feed the cache-eligibility evaluation (F041) per gate.
- When a required sensed fact is **not available** for a gate, it never fabricates, zero-fills, or guesses a
  value; instead it yields a **no-hide "unresolved" outcome** naming exactly which sensed fact(s) are missing —
  an outcome that is recompute-safe by construction, because it cannot be mistaken for a resolved input set.
- It rolls these up into a **deterministic, gate-attributable report**: one resolution outcome per selected
  gate, ordered by gate identity, with every gate preserved.

The contract is **honest resolution (fabricate nothing), totality, determinism, and gate-attributable no-hide
attribution** — never an invented hash or revision, never an unexplained gap, never a dropped or merged gate.
Resolving a gate's inputs is **necessary-not-sufficient**: it authorizes no reuse, no skip, and carries no
enforcement meaning by itself; it merely supplies the inputs a later host step feeds to F041 before any gate is
actually skipped.

This core performs **no sensing of its own** — it reads no git, filesystem, clock, environment, or network, and
runs no command. It **computes no hash, freshness key, or digest**, **evaluates no cache eligibility** (it does
not compare against any evidence store — that is F030/F041), **renders no JSON** (that is F042 and the
projection rows), **persists nothing**, **maps no exit code**, and **adds no CLI**. Its sole output is the typed
per-gate freshness-resolution report value.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Assemble complete freshness inputs per selected gate (Priority: P1)

A reviewer routes a change to a set of gates and, for the cache-eligibility step to run at all, each selected
gate must be lifted from its carried freshness-key identity into the full set of freshness inputs F029/F030/F041
consume. Given the gate's carried identity (check, domain, environment, command) and the repository facts
already sensed (base/head revisions, rule hash, generator version, the gate's covered-artifact hashes, and its
command version), the core assembles the complete ten-field freshness inputs for that gate — ready to be
evaluated for cache eligibility.

**Why this priority**: This join is the entire reason the row exists — it is the bridge that lets the already-built
evidence-reuse and cache-eligibility cores run against a real routed change. Without it, those cores have no
inputs. It is the minimum viable, independently demonstrable slice.

**Independent Test**: Supply a selected gate with a known carried identity and a bundle of known sensed facts;
confirm the resolved freshness inputs carry the gate's four identity fields verbatim and the six sensed fields
verbatim, with the gate's cost dropped, and that the resolved value is shaped to feed the cache-eligibility
candidate evaluation unchanged.

**Acceptance Scenarios**:

1. **Given** a selected gate carrying a freshness-key identity and a bundle of fully-sensed repository facts,
   **When** freshness inputs are resolved, **Then** the gate yields a complete freshness-input set whose check,
   domain, environment, and command equal the gate's carried identity and whose rule hash, covered-artifact
   hashes, command version, generator version, base revision, and head revision equal the supplied sensed facts.
2. **Given** a gate whose carried identity includes a cost, **When** its inputs are resolved, **Then** the cost
   does not appear in the resolved freshness inputs (cost is not a freshness input).
3. **Given** a resolved gate, **When** its outcome is handed to the per-gate cache-eligibility evaluation, **Then**
   it is accepted as a cache-eligibility candidate without adaptation.

---

### User Story 2 - Never fabricate: name the missing sensed fact instead (Priority: P2)

When a sensed fact required to complete a gate's freshness inputs is unavailable — the rule hash, generator
version, base/head revision, the gate's covered-artifact hashes, or (for a gate that declares a command) its
command version — the core must not invent, default, or zero-fill that value. Instead it yields an *unresolved*
outcome that names exactly which sensed fact(s) are missing, and that outcome must be impossible to mistake for a
resolved input set, so a later step can never treat an unresolved gate as cache-reusable.

**Why this priority**: This is the honesty/safety property the join must protect. A fabricated or zero-filled
hash would let an unresolved gate fingerprint to a false "world" and risk a falsely-claimed cache hit. Refusing to
fabricate, and making the gap explicit and recompute-safe, is what keeps the resolution trustworthy. It depends on
the resolved path (US1) existing first.

**Independent Test**: Supply a gate whose sensed-facts bundle is missing one or more required values; confirm the
gate yields an *unresolved* outcome naming exactly the missing fact(s), that no resolved freshness-input set is
produced for it, and that no value was fabricated, defaulted, or zero-filled.

**Acceptance Scenarios**:

1. **Given** a selected gate for which a required sensed fact (rule hash, generator version, base or head
   revision, covered-artifact hashes, or — when a command is declared — command version) is unavailable, **When**
   inputs are resolved, **Then** that gate yields an *unresolved* outcome naming exactly the missing fact(s) and
   no others.
2. **Given** an *unresolved* gate, **When** its outcome is inspected, **Then** it carries no resolved
   freshness-input set and exposes no fabricated, defaulted, or zero-filled hash, version, or revision.
3. **Given** a gate missing several required sensed facts, **When** resolved, **Then** the outcome names every
   missing fact (the no-hide rule), never truncated to the first gap.

---

### User Story 3 - One attributable outcome per gate, deterministic and total (Priority: P3)

A reviewer resolves all gates a change selected and receives **one** resolution outcome **per gate**, each
attributed to its gate identity, in a stable order, with no gate dropped, merged, or duplicated — and identical
inputs always yield an identical report, regardless of working directory, clock, or filesystem state.

**Why this priority**: The host runs F041 per gate and the projection places each verdict under its gate, so the
resolution report must be gate-attributable, complete, deterministically ordered, and reproducible. This makes it
composable and diff-stable, but it depends on the per-gate outcome (US1/US2) existing first.

**Independent Test**: Resolve a set of selected gates supplied in arbitrary order against a fixed sensed-facts
bundle; confirm exactly one outcome per gate, ordered by gate identity, every gate preserved, and that
re-resolving the same inputs under a changed working directory, clock, or filesystem yields a byte-identical
report.

**Acceptance Scenarios**:

1. **Given** N selected gates, **When** resolved, **Then** the report contains exactly N outcomes — one per gate
   — each carrying its originating gate identity, with no gate dropped, merged, or duplicated.
2. **Given** the same gates supplied in two different orders, **When** resolved, **Then** both reports are
   identical and ordered by gate identity.
3. **Given** identical gates and sensed facts, **When** resolved under a different working directory / clock /
   filesystem state, **Then** the report is identical (no I/O is performed).

---

### Edge Cases

- **No selected gates** (a change that routed to nothing cacheable): the report is empty — a total, valid result,
  not an error.
- **One selected gate**: a single-element report; the single-gate path behaves like the many-gate path.
- **A gate that declares no command**: it resolves with **no command** and **no command version** — a consistent
  absence, *not* a missing-fact / unresolved condition (a command version is only required when a command is
  declared).
- **A gate that declares a command but whose command version was not sensed**: *unresolved*, naming the missing
  command version.
- **A gate whose covered-artifact set was sensed as empty** versus **not sensed at all**: an explicitly-supplied
  empty covered-artifact set is a legitimate resolved value (the core renders what was sensed); a covered-artifact
  set that was never sensed is *unresolved* — the two are distinct and never conflated.
- **A repository-wide sensed fact missing** (rule hash, generator version, base or head revision): every gate that
  needs it is *unresolved* on that fact; the core still returns one well-formed outcome per gate.
- **Duplicate gate identities among selected gates**: each is resolved independently and ordered deterministically
  by gate identity; the core neither merges nor drops duplicates (see Assumptions).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The core MUST resolve, for each selected gate of a routed change, the gate's complete set of
  freshness inputs by **joining** the gate's carried freshness-key identity with the supplied
  already-sensed repository facts.
- **FR-002**: The core MUST source the **check, domain, environment, and command** of the resolved freshness
  inputs from the gate's own carried freshness-key identity, and the **rule hash, covered-artifact hashes,
  command version, generator version, base revision, and head revision** from the supplied sensed facts; it MUST
  drop the gate's **cost**, which is not a freshness input.
- **FR-003**: The core MUST NOT fabricate, default, zero-fill, guess, or otherwise invent any hash, version, or
  revision; when a sensed fact required to complete a gate's freshness inputs is unavailable, it MUST yield a
  **no-hide *unresolved* outcome** naming exactly the missing sensed fact(s) and no others, rather than a
  placeholder freshness-input value.
- **FR-004**: An *unresolved* outcome MUST be **recompute-safe by construction** — it MUST NOT be representable as,
  or convertible by a consumer into, a resolved freshness-input set, so a downstream step can never mistake an
  unresolved gate for one whose evidence is cache-reusable.
- **FR-005**: A gate that declares **no command** MUST resolve with no command and no command version (a consistent
  absence); this MUST NOT be treated as a missing sensed fact or yield an *unresolved* outcome.
- **FR-006**: The core MUST attribute every outcome — resolved or unresolved — to its originating **gate
  identity**, so the host can run cache eligibility and a later projection can place each result under the correct
  gate.
- **FR-007**: The core MUST return **exactly one outcome per selected gate**, preserving every gate — no gate
  dropped, merged into another, or silently duplicated — and MUST emit outcomes in a **deterministic order by gate
  identity**, independent of the order gates were supplied.
- **FR-008**: The core MUST be **total**: it returns a well-formed report for every input — including no gates, a
  single gate, a fully-resolved gate, and a gate missing every required sensed fact — and never throws, swallows a
  failure, or silently drops a gate.
- **FR-009**: The core MUST be **pure and deterministic**: identical gates and sensed facts always yield an
  identical report; it senses nothing itself — reading no git, filesystem, clock, environment, or network and
  running no command — and computes no hash, freshness key, or digest.
- **FR-010**: Each **resolved** outcome MUST be shaped to feed the existing per-gate cache-eligibility evaluation
  verbatim — pairing the gate identity with the resolved freshness inputs — **without** this core itself evaluating
  eligibility, comparing against any evidence store, or ranking entries (those belong to F030/F041).
- **FR-011**: Resolving a gate's inputs MUST be **necessary-not-sufficient**: it carries no reuse decision, no skip
  action, no enforcement severity, no ship verdict, and no exit-code basis — it asserts only "these are this gate's
  freshness inputs (or this gate could not be resolved)", which a later host step composes with the
  cache-eligibility decision before any gate is actually skipped.
- **FR-012**: The core MUST reuse the existing **freshness-input**, **gate-identity**, **gate freshness-key**,
  **revision**, **hash**, and **version** vocabulary verbatim rather than redefining it, introducing only the
  minimal new vocabulary this row needs (the supplied sensed-facts bundle, the two-outcome per-gate resolution
  result, and the per-change resolution report).
- **FR-013**: The core MUST add **no new third-party dependency**; its only couplings are to the sibling pure cores
  that already own the reused vocabulary.
- **FR-014**: The change MUST be **purely additive**: it modifies no existing merged core, public-surface
  baseline, or projection, and leaves existing build and test runs unchanged.

### Key Entities *(include if feature involves data)*

- **Selected gate**: one gate a routed change selected, carrying its stable **gate identity** and its **five-field
  freshness-key identity** (check, domain, cost, environment, command). The unit of input the core resolves; its
  cost is intentionally ignored.
- **Sensed-facts bundle**: the repository facts **already sensed elsewhere** and supplied to the core — the
  base/head revisions, rule hash, generator version, plus the per-gate covered-artifact hashes and per-command
  command version. Supplied verbatim; the core senses none of it.
- **Resolved freshness inputs**: the complete F029 freshness-input value for a gate — its four carried identity
  fields joined with the six supplied sensed fields. Shaped to feed cache-eligibility candidate evaluation.
- **Per-gate resolution outcome**: the two-outcome result for a gate — *resolved* (carrying the freshness inputs,
  ready as a cache-eligibility candidate) or *unresolved* (carrying the named missing sensed fact(s)).
- **Freshness-resolution report**: the per-change roll-up — one resolution outcome per selected gate, each
  attributed to its gate identity, in deterministic gate-identity order, with every gate preserved.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every selected gate whose required sensed facts are all present, the resolved freshness inputs
  carry the gate's four identity fields and the six supplied sensed fields verbatim, with cost dropped — 100% of
  such cases, with zero fabricated, defaulted, or zero-filled values (US1, FR-001/FR-002).
- **SC-002**: For every gate missing one or more required sensed facts, the outcome is *unresolved* naming exactly
  the missing fact(s) — no resolved input set produced and no placeholder value exposed — across all combinations
  of missing facts (US2, FR-003).
- **SC-003**: A gate that declares no command resolves with absent command and absent command version and is never
  flagged unresolved on that basis — 100% of such cases (US1 edge, FR-005).
- **SC-004**: The core returns a well-formed report and never throws across the full cross-product of gate counts
  (zero, one, many) and sensed-facts states (all present, partially present, all absent) (US3, FR-008).
- **SC-005**: Identical gates and sensed facts yield a byte-identical report under changed working directory,
  clock, and filesystem state, with no I/O performed (US3, FR-009).
- **SC-006**: Every report contains exactly one outcome per selected gate, attributed to its gate identity, in
  deterministic gate-identity order, with no gate dropped, merged, or duplicated — independent of input order
  (US3, FR-006/FR-007).
- **SC-007**: Every *resolved* outcome is accepted by the existing per-gate cache-eligibility evaluation as a
  candidate without adaptation, and a *resolved* outcome carries no reuse decision, skip action, enforcement
  severity, ship verdict, or exit-code basis (FR-010/FR-011).
- **SC-008**: The feature is additive: existing cores, public-surface baselines, and projections are unchanged, and
  existing build/test runs pass unchanged (FR-014).

## Assumptions

- **Sensing happens upstream, not here.** The base/head revisions, rule hash, generator version, covered-artifact
  hashes, and command versions arrive **already sensed** in the supplied bundle. The host edge that actually reads
  git, hashes artifacts and rule packs, and reads command/generator versions from the real repository is a later
  row, explicitly out of this scope; this core only joins what it is given.
- **Cost is intentionally not a freshness input.** The gate's carried freshness-key identity includes a cost
  (F018), but F029 deliberately excludes cost (research D5); resolution drops it.
- **Base/head revisions arrive in freshness-key revision form.** The host maps the snapshot's commit identifiers
  into the freshness-key revision vocabulary before supplying them; this core consumes them as opaque revision
  values and does not re-derive them from a snapshot.
- **"Unresolved" is recompute-safe and composed downstream.** Producing an *unresolved* outcome authorizes nothing
  on its own; how the host treats it (as must-recompute when it feeds the cache-eligibility step) is the host's
  row, consistent with the necessary-not-sufficient discipline established by F039/F040/F041.
- **An explicitly-empty covered-artifact set is a resolved value; an unsensed one is unresolved.** The core renders
  what was sensed and does not invent artifacts; it distinguishes "sensed as empty" from "not sensed".
- **Duplicate selected-gate identities** are resolved independently and ordered deterministically by gate identity;
  the row does not assume the caller pre-deduplicates, and it neither merges nor drops duplicates. (If planning
  surfaces a strong reason to forbid duplicates outright, that becomes an input precondition rather than a silent
  collapse.)
- **Determinism is the contract, not performance.** The resolution is a small per-gate join over a handful of
  supplied facts; latency is not a success criterion.
- **Assumed module name** `FS.GG.Governance.FreshnessResolution` with a pure entry point `resolve`, mirroring the
  sibling pure cores; the exact name is confirmed at planning time and does not affect this spec.

## Out of Scope

- **Sensing the inputs** — reading git for revisions, hashing rule packs and covered artifacts, and reading
  command/generator versions from the real repository is the later **host** row, not this one.
- **Evaluating cache eligibility** — comparing resolved inputs against an evidence store and deciding
  reuse/recompute is owned by F030/F041; this core only supplies the candidate inputs.
- **Rendering JSON** — emitting resolved inputs or eligibility into `cache-eligibility.json`, route.json, or
  audit.json is owned by F042 and the projection rows.
- **Cache storage, lookup against a real store, eviction, or expiry** — no persistence or on-disk/networked cache
  is touched.
- **Mapping the snapshot into revision values** — the host supplies revisions already in freshness-key form; this
  core does not consume a raw git snapshot.
- **Enforcement, ship verdict, exit-code mapping, and CLI** — owned by Phase-5 cores (F023/F024) and the host
  commands (F022/F026).
