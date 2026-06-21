# Feature Specification: Freshness Key Computation Core

**Feature Branch**: `029-freshness-key-core`

**Created**: 2026-06-21

**Status**: Draft

**Input**: User description: "next item in plan" — resolved against `docs/initial-implementation-plan.md`.
Phase 2 (Governance Ship Walking Skeleton & Catalog MVP) and Phase 5 (Route Parity, Profiles, and
Enforcement Fixtures) are now complete (F014–F028 merged). The next Governance-owned area in the design's
ordering — and the one every prior gate/route/audit row explicitly **deferred to "Phase 11"** — is
**Phase 11: Cost, Cache, and Provenance**, whose first checkbox is *"Define freshness keys over rule hash,
artifact hash, command version, generator version, base/head, environment class, and output digest."*
Continuing this repo's maintainer-confirmed **pure-core-first** rhythm (F015–F025 each landed a pure,
total, deterministic core before any host edge consumed it), this row is sliced to that single first
checkbox: the typed freshness-key vocabulary and the total, deterministic function that computes a
**stable, comparable freshness key** from those declared inputs, plus the **match predicate** that a later
cache step ("cache reusable evidence only when all freshness inputs match") will build on. It performs
**no cache lookup or storage**, computes **no hashes itself**, reads **no clock / filesystem / git /
network**, persists **no artifact**, and adds **no CLI**.

## Overview

Governance keeps the local authoring loop cheap by reusing expensive evidence (a build, a test run, a
pack) instead of recomputing it — but **only when it is defensible to do so**. The question "may I reuse
the evidence I recorded last time?" must have a single, deterministic, auditable answer. That answer is the
**freshness key**: a canonical fingerprint of every input that, if changed, would invalidate prior
evidence. Two runs that produce the **same** freshness key are working against the same world and may share
evidence; two runs that produce **different** keys must not.

Today the gate registry (F018) already **carries** a minimal MVP freshness key per gate — the always-available
declared identity (check id, domain, cost, environment class, command) — but it explicitly **evaluates
nothing**: it computes no key value, compares no inputs, and caches nothing. Every downstream row (F019
route selection, F020 route.json, F021 gates.json, F024/F026 ship) likewise *carries the inputs forward and
defers the freshness decision to Phase 11*. This feature is that deferred decision's foundation, expressed
as a pure, total core:

- **Model the full freshness-input set** the design names — extending the carried MVP identity with the
  Phase-11 inputs: the **rule hash**, the **artifact hash(es)** the evidence covers, the **command
  version**, the **generator version**, and the **base/head** revision pair — as closed, comparable typed
  values that carry **no raw bytes, no host paths, no clock, and no product vocabulary**.
- **Compute a stable, comparable freshness key** from those inputs via a single deterministic function:
  identical inputs always yield byte-identical keys, regardless of when, where, or in what input order the
  computation runs.
- **Decide reuse** with a total match predicate: a candidate run's key matches a recorded run's key **iff
  every freshness input matches**. This is the literal foundation of the plan's "cache reusable evidence
  only when all freshness inputs match" — without yet touching any cache store.

The core is **pure over supplied data**. Like the existing kernel `Freshness` module (which decides whether
recorded evidence is still *current* over supplied instants), this core reads no clock, filesystem, git, or
network: the hashes and versions are sensed **at the edge** (the interpreter / snapshot layer) and passed
in as already-computed values. This feature only *fingerprints and compares* them. It is the **identity**
companion to kernel `Freshness`'s **currency** companion: kernel `Freshness` answers "has a covered artifact
changed since the evidence was recorded?"; this feature answers "are these two runs even asking the same
question?". Cache storage, lookup, eviction, and the broad-route cost explanation are **out of scope** and
remain later Phase-11 rows.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Identical inputs reuse evidence; any changed input forbids reuse (Priority: P1)

A Governance consumer ran an expensive gate and recorded its evidence keyed by a freshness key. On a later
run, Governance needs to decide — deterministically and without re-running the gate — whether that recorded
evidence still applies. It computes the freshness key for the new run from the same input categories and
compares it to the recorded key.

**Why this priority**: This is the whole point of the feature and the foundation of the entire cost/cache
phase. Without a deterministic "same inputs ⇒ same key, any change ⇒ different key" guarantee, evidence
reuse is either unsafe (reusing stale evidence) or worthless (never reusing). It is independently
demonstrable and delivers the core value on its own.

**Independent Test**: Build two freshness-input sets that are identical in every field, compute both keys,
and assert they are equal and `matches` is true. Then, for each input field in turn, build a second set
that differs in **only that one field**, and assert the key changes and `matches` is false. Fully exercises
the contract with no host, no I/O, and no other feature.

**Acceptance Scenarios**:

1. **Given** two freshness-input sets equal in every field, **When** their keys are computed, **Then** the
   two keys are equal and `matches` returns true (reuse is permitted).
2. **Given** two input sets differing only in the rule hash, **When** their keys are computed, **Then** the
   keys differ and `matches` returns false (reuse is forbidden).
3. **Given** two input sets differing only in a covered artifact hash, **When** their keys are computed,
   **Then** the keys differ and `matches` returns false.
4. **Given** two input sets differing only in the command version, the generator version, the base
   revision, the head revision, or the environment class (each tested separately), **When** their keys are
   computed, **Then** the keys differ and `matches` returns false.

---

### User Story 2 - The key is byte-stable and order-independent (Priority: P1)

The same logical inputs must always fingerprint to the exact same key — across runs, machines, processes,
and regardless of the order the covered-artifact hashes happen to be supplied in — so a freshness key
recorded by one run is comparable to a key computed by any other run, and so committed/snapshotted keys do
not drift.

**Why this priority**: A freshness key that is not byte-stable is not a key at all — it would spuriously
forbid every reuse and corrupt any later cache. Determinism and order-independence are non-negotiable for a
fingerprint that is compared across runs and machines. This is the contract every consumer relies on, so it
is co-P1 with Story 1.

**Independent Test**: Compute the key for a fixed input set twice and assert byte equality. Compute the key
for the same inputs with the covered-artifact hashes supplied in a different order and assert the key is
unchanged. Compute keys for a small representative table of input sets and assert each equals its committed
golden form.

**Acceptance Scenarios**:

1. **Given** one freshness-input set, **When** its key is computed twice, **Then** the two results are
   byte-identical.
2. **Given** an input set whose covered-artifact hashes are reordered (same set, different sequence),
   **When** the key is computed, **Then** it equals the key computed from the original order (collection
   order does not affect the key).
3. **Given** a duplicate covered-artifact hash appearing twice in the supplied list, **When** the key is
   computed, **Then** it equals the key computed from the de-duplicated list (the key fingerprints the
   *set* of covered artifacts, not their multiplicity).

---

### User Story 3 - Every freshness input is named, never hidden (Priority: P2)

When evidence is *not* reused, an auditor must be able to see **which input changed** — the key must be
explainable, not an opaque blob. The full set of inputs that produced a key is recoverable/inspectable from
the key value, so a reviewer can answer "why did this run not reuse last run's evidence?" by comparing input
sets field by field.

**Why this priority**: The design's honesty boundary ("profiles never hide underlying verdicts"; "every
generated view must identify sources, source digests, generator version") extends to cache decisions: a
reuse-or-not decision that cannot be explained is a silent failure. This is essential for auditability but
builds on Stories 1–2, so it is P2.

**Independent Test**: From a freshness key (or its accompanying input record), read back every contributing
input category and assert it equals what was supplied. For two non-matching keys, identify the differing
field(s) without re-deriving anything outside the core.

**Acceptance Scenarios**:

1. **Given** a computed freshness key, **When** its inputs are inspected, **Then** every input category
   (rule hash, covered artifact hashes, command version, generator version, base revision, head revision,
   environment class, and the carried gate identity) is present and equals the supplied value.
2. **Given** two non-matching input sets, **When** they are compared, **Then** the core reports at least
   one specific differing input category (no "they just differ" without a locatable cause).

---

### Edge Cases

- **No covered artifacts.** An input set covering zero artifacts is valid and produces a stable key (it is
  not an error); it matches another zero-artifact set with otherwise-equal inputs.
- **Optional command / command version absent.** A gate with no declared command (carried `Command = None`)
  has no command version; the key is still stable, and "absent command version" is distinct from "some
  present command version" (two such sets never collide).
- **Base equals head.** A run whose base and head revisions are identical is valid and produces a stable
  key, distinct from a run where they differ.
- **Hash/version collisions across categories.** The same opaque string appearing as both a rule hash and
  an artifact hash must not let one input masquerade as another — category boundaries are preserved so that
  moving a value from one field to another changes the key.
- **Input-order and duplication.** Reordering or duplicating the supplied covered-artifact hashes never
  changes the key (Story 2).
- **Empty / degenerate hash or version strings.** A supplied empty-string hash or version is treated as a
  literal value (it is the edge's responsibility to supply meaningful digests); the core neither rejects
  nor special-cases it, and two empty values match while an empty and a non-empty value do not.
- **Future input growth.** Adding a new freshness input category in a later release necessarily changes
  every key, which (correctly) forbids reuse of evidence recorded before the category existed — the design
  intent that a more complete key invalidates older, less-complete evidence.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST define a closed, typed **freshness-input** value that names every input the
  design's Phase-11 freshness key covers: the **rule hash**, the set of **covered artifact hash(es)**, the
  **command version** (optional — absent when the gate declares no command), the **generator version**, the
  **base revision**, the **head revision**, the **environment class**, and the **carried gate identity**
  (the check/domain/command identity F018 already carries on each gate).
- **FR-002**: The system MUST provide a single, pure, total function that computes a **freshness key** from
  a freshness-input value. The function MUST be defined for every well-typed input (no input value causes
  failure or exception).
- **FR-003**: The freshness key MUST be **deterministic and byte-stable**: identical inputs always produce
  the identical key, independent of evaluation time, machine, process, or the order in which collection
  inputs (covered artifact hashes) are supplied.
- **FR-004**: The key MUST treat the covered-artifact hashes as a **set**: reordering or duplicating them
  MUST NOT change the key.
- **FR-005**: The system MUST provide a total **match predicate** that returns true **iff** two freshness
  inputs (equivalently, their keys) agree on **every** input category — the literal "all freshness inputs
  match" condition. It MUST return false whenever any single category differs.
- **FR-006**: The key MUST be **injective across input categories with respect to value placement**: the
  same opaque string supplied in different input categories MUST NOT produce colliding keys (category
  boundaries are preserved; a value cannot masquerade as a different input).
- **FR-007**: The system MUST keep the contributing inputs **inspectable** from the key (or an accompanying
  record) so that a reviewer can recover every input category's value and identify which category caused
  two keys to differ (the no-hide / explainability requirement).
- **FR-008**: The core MUST be **pure over supplied data**: it MUST read no clock, no filesystem, no git,
  no environment, and no network. All hashes, versions, and revisions are supplied as already-computed
  values; sensing them is the edge interpreter's responsibility and is OUT OF SCOPE.
- **FR-009**: The core MUST **consume the existing carried vocabulary verbatim** where it already exists —
  the F018 gate freshness identity (check id, domain, environment class, optional command) and the F014
  typed-fact newtypes — rather than re-deriving or re-validating them. It MUST NOT modify F018's carried
  `FreshnessKey` shape or any merged core; this feature is additive.
- **FR-010**: The core MUST compute **no cache lookup, storage, eviction, or reuse side effect**; it MUST
  compute no ship verdict; it MUST persist no artifact and add no CLI surface. Its sole outputs are the key
  value and the match decision.
- **FR-011**: The core MUST handle the degenerate cases as ordinary, total outcomes (not errors): zero
  covered artifacts, absent command version, base equal to head, and empty-string hash/version values
  (each as described in Edge Cases).
- **FR-012**: If this feature introduces a public F# module, its surface MUST be governed by the repo's
  `.fsi`-first and `surface/*.surface.txt` baseline rules (Constitution Principles I & II) — a **Tier 1**
  change (see Assumptions). [The concrete module home and name are a planning decision deferred to
  `/speckit-plan`.]
- **FR-013**: The core MUST NOT add a new third-party package dependency; the key computation MUST use only
  facilities already available to the merged cores (the shared framework / BCL).

### Key Entities *(include if feature involves data)*

- **Freshness inputs**: The complete, closed set of values that determine whether prior evidence may be
  reused — rule hash, covered artifact hash set, optional command version, generator version, base
  revision, head revision, environment class, and the carried gate identity. Each is an opaque,
  comparable value carrying no raw bytes, host paths, clock readings, or product vocabulary.
- **Freshness key**: The deterministic, byte-stable, comparable fingerprint computed from a freshness-input
  value. Equal keys mean "same world, reuse permitted"; different keys mean "something that matters
  changed, reuse forbidden." Inspectable back to its contributing inputs.
- **Match decision**: The total predicate over two freshness inputs/keys — true iff every input category
  agrees — that a later cache step consumes to decide reuse, and that names the differing category when it
  is false.
- **Covered artifact hash** (reused concept): A supplied digest standing for the version of one artifact
  the evidence depends on; the key fingerprints the *set* of these.
- **Carried gate identity** (reused from F018): The check id, domain, environment class, and optional
  command already attached to every gate's MVP freshness key — consumed verbatim, not redefined.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For any freshness-input set, computing the key twice yields **byte-identical** results in
  100% of cases (determinism).
- **SC-002**: For two input sets that are equal in every category, `matches` returns true and the keys are
  equal in 100% of cases; reordering or duplicating the covered-artifact hashes changes neither the key nor
  the match result.
- **SC-003**: For two input sets differing in **exactly one** category, the keys differ and `matches`
  returns false — verified for **every** input category (rule hash, covered artifact set, command version
  present/absent, generator version, base revision, head revision, environment class, gate identity), i.e.
  100% single-field-change coverage.
- **SC-004**: Moving the same opaque string between two different input categories changes the key in 100%
  of tested category pairs (no cross-category collision).
- **SC-005**: From any computed key (or its record), 100% of contributing input categories are recoverable,
  and for any non-matching pair the differing category is identifiable.
- **SC-006**: The core reads no clock, filesystem, git, environment, or network — demonstrable by the key
  for a fixed input set being identical when computed in different working directories, at different times,
  and with unrelated repository/filesystem state changed between computations.
- **SC-007**: The merged cores and their `surface/*.surface.txt` baselines, and `dotnet build` / `dotnet
  test` over the existing projects, are **unchanged** by this feature except for the additive new surface
  (if any) — no existing baseline is rewritten and no existing test changes outcome.

## Assumptions

- **Scope is the key + match predicate only.** The actual cache store (lookup, write, eviction), the
  "reuse evidence" effect, the broad-route cost explanation, command-run records, and provenance/attestation
  are **later Phase-11 rows** and are out of scope here. This row produces only the deterministic key and
  the match decision the cache step will consume.
- **Hashes and versions are supplied, not computed.** Consistent with the existing kernel `Freshness`
  purity contract and the F018 "ids only — no raw YAML, no clock" framing, every digest/version/revision is
  computed at the edge (interpreter/snapshot) and passed in. This core never opens a file, runs git, or
  reads a clock.
- **The output digest is not part of the lookup key.** The Phase-11 plan line lists "output digest" among
  freshness-key concerns, but an output digest is a *result* of running a gate, not an *input* that decides
  reuse. It is treated as a downstream **verification companion** (recorded alongside reused evidence to
  confirm integrity) and is therefore **out of scope** for the input-identity key computed here. (Flagged
  for confirmation at `/speckit-plan`.)
- **Change classification: Tier 1 (contracted change).** This feature adds new public API surface (a new
  module/assembly) and a new `surface/*.surface.txt` baseline, so per the Constitution it is **Tier 1** and
  carries the full chain: spec, plan, `.fsi`, surface baseline, and tests. It adds **no new third-party
  dependency**. It consumes the merged cores (F014 typed facts, F018 carried gate freshness identity, kernel
  `Freshness`) verbatim and modifies none of them. (Whether it lands as a new pure-core module or extends an
  existing one is the only home decision left to `/speckit-plan`; the plan resolved it to a new core.)
- **Determinism is the contract, not performance.** The input set is small (a handful of hashes/versions
  per gate); there is no latency or throughput target. Byte-stability and totality are the guarantees.
- **The base/head pair reuses the existing snapshot vocabulary** (F016 `Snapshot`) where a typed revision
  value already exists; if a suitable typed value is not already public, the plan chooses the minimal
  representation — this is deferred to `/speckit-plan`.
