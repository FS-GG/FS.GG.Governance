# Feature Specification: Capture A Real Evidence Reference From An Executed Gate

**Feature Branch**: `049-evidence-reference-capture`

**Created**: 2026-06-24

**Status**: Draft

**Input**: User description: "next item in the plan." — resolved (via AskUserQuestion, choosing the
**pure-core-first** scope over the impure host gate-execution row) to the single remaining deferred row of the
cache/evidence-reuse thread, named three times in `docs/initial-implementation-plan.md` (lines 226, 471, 682):
**"real evidence-reference capture from gate execution."** This row delivers the **pure capture core** — the
value-only bridge that turns an *already-executed* gate's command record (F032) into the real, reproducible
`EvidenceRef` the evidence-reuse store holds, and folds it into the store against that gate's resolved
freshness world (F030 `record`). Mirrors how F047 delivered the pure write half before F048 wired it: the
**impure** edge (actually running gates inside `fsgg route`/`fsgg ship`, sensing each output digest, building
the `CommandRecord`, and recording during a run) is the **following** row and is out of scope here.

## Context

The cache-eligibility / freshness thread (F029–F048) is wired end-to-end: `fsgg route` and `fsgg ship` sense
each selected gate's freshness facts, resolve them to a complete `FreshnessInputs` world (F043), evaluate reuse
against the loaded evidence-reuse store (F030 `decide` → F041), embed the per-gate `reusable` / `mustRecompute`
verdict into `route.json` / `audit.json` (F045/F046), and — opt-in — persist the bounded, pruned store back to
disk (F047 `serialise`/`retain`/`prune` + F048 `--persist-store`). The store can now be loaded, evaluated,
bounded, pruned, serialised, and persisted across runs.

**But the store never gains a new entry.** `EvidenceReuse.record` (F030) — the function that folds one recorded
evidence reference into the store — is called by **nobody** in the host path. Every `EvidenceRef` that exists
today is a **synthetic literal** (the disclosed `Synthetic` token in fixtures, Principle V). Nothing turns an
actually-executed gate into a real, reproducible evidence reference. So the persisted store can only ever
*shrink* (prune/retain) — it can never *grow* from a real run, and a future run's reuse decision can only ever
match a hand-written test world, never a world a real gate execution produced.

The missing piece is a **reproducible reference** derived from a real execution. F032 `CommandRecord` already
models an executed command's reproducible facts (executable, arguments, working directory, environment delta,
timeout, exit code, stdout/stderr digests, captured-output outcome) held **structurally apart** from the one
sensed fact (`SensedDuration`), and already folds the reproducible facts to a byte-stable `CommandIdentity` via
`canonicalId` (duration excluded — D2). F030 already holds an opaque `EvidenceRef` and folds it into the store
via `record`. What no row has yet done is **bridge them**: derive the `EvidenceRef` from a command record's
reproducible identity, so the reference a run records is itself reproducible (not the sensed wall-clock, not a
fresh GUID), and a re-execution of the same world yields the **same** reference.

This row delivers that pure bridge as a new value-only core (`FS.GG.Governance.EvidenceCapture`), referenced by
nothing yet (exactly as F047 was on landing). It runs **no** gate, spawns **no** process, hashes **no** bytes,
reads **no** clock/filesystem/git/environment/network, bumps **no** schema version, and changes **no** existing
core, host command, or golden baseline.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A real execution becomes a reusable store entry (Priority: P1)

As the governance cache author, given a gate that has *already executed* — represented by its `CommandRecord`
(F032) — and the resolved freshness world it ran under (`FreshnessInputs`, F043), I can fold a real, reproducible
evidence reference into the evidence-reuse store, so a later run under the **same** world reuses that real
evidence instead of recomputing.

**Why this priority**: This is the whole point of the row — it is the only step that lets the store grow from a
real run. Without it the entire persist/prune/retain machinery (F047/F048) operates on a store that can never
gain a real entry. It is the smallest standalone slice that closes the capture gap.

**Independent Test**: In FSI / a semantic test, build a `CommandRecord` and a `FreshnessInputs`, call `capture
inputs record store` over the empty store, and assert `EvidenceReuse.decide inputs (result)` returns `Reuse r`
where `r` equals `referenceOf record` — i.e. the captured world is now reusable and serves exactly the derived
reference. No I/O.

**Acceptance Scenarios**:

1. **Given** an executed gate's `CommandRecord` and its resolved `FreshnessInputs`, **When** `capture` folds it
   into the empty store, **Then** `EvidenceReuse.decide` for that same `FreshnessInputs` over the result returns
   `Reuse r` with `r = referenceOf record` (the world is now reusable and serves the derived reference).
2. **Given** the same `CommandRecord` and `FreshnessInputs`, **When** `capture` runs twice on two machines /
   processes, **Then** both produce the byte-identical `EvidenceRef` and the byte-identical resulting store
   (determinism, no clock/GUID leakage).
3. **Given** a captured world, **When** `decide` is asked about a **different** freshness world (any input
   category differs), **Then** it returns `Recompute` — capture introduces no new match, only the recorded one.

---

### User Story 2 - The reference is reproducible: the sensed duration never leaks (Priority: P1)

As the cache author, two executions of the *same* gate that differ only in how long they took must yield the
**same** evidence reference, so the reusable reference is a function of *what ran and what it produced*, never of
the non-deterministic wall-clock — otherwise no two runs would ever agree and the cache would be useless.

**Why this priority**: A reference that varied with the sensed duration would defeat reuse entirely (a perfect
re-run would record a *different* reference and never be served). Duration-invariance is a correctness contract,
not a nicety. It is the reason the reference is derived from the reproducible identity (F032 `canonicalId`),
which excludes `SensedDuration` by construction.

**Independent Test**: Build two `CommandRecord`s identical in every reproducible fact but differing in their
`SensedDuration`; assert `referenceOf` returns the byte-identical `EvidenceRef` for both.

**Acceptance Scenarios**:

1. **Given** two command records differing **only** in `SensedDuration`, **When** `referenceOf` derives a
   reference for each, **Then** the two references are byte-identical.
2. **Given** two command records differing in **any one** reproducible fact (executable, an argument or its
   order, working directory, the env-delta as a set, timeout, exit code, either output digest, or the
   captured-output outcome), **When** `referenceOf` derives a reference for each, **Then** the two references
   differ.

---

### User Story 3 - Capture is purely additive: no policy, no clobber, no recompute regression (Priority: P2)

As the maintainer, capturing evidence must reuse the F030 `record` convention verbatim (newest-first, supplied
store in / store out), introduce no new reuse policy, and never turn a world that was previously a `Recompute`
into a *wrong* `Reuse` — it may only make the **just-captured** world reusable, leaving every other world's
verdict exactly as F030 already decides it.

**Why this priority**: It guards the additive, recompute-safe guarantee the whole thread inherits — capture must
not become a back door that weakens the freshness contract.

**Independent Test**: Capture into a non-empty store and assert every pre-existing entry is unchanged and that
`decide` for any world other than the captured one returns exactly what it returned before the capture.

**Acceptance Scenarios**:

1. **Given** a non-empty store, **When** `capture` folds a **new** world's evidence in, **Then** the result is the
   F030 `record`-of that derived reference (newest-first), every prior entry **for a non-matching world**
   preserved byte-for-byte (F030 `record` de-dups only an exact full-match of the captured world).
2. **Given** a world already recorded with a different reference, **When** the same world is captured again with
   a new execution, **Then** `decide` for that world serves the **most-recently-captured** reference (F030
   newest-first convention, no new policy).

### Edge Cases

- **Empty / boundary digests**: A `CommandRecord` whose stdout or stderr digest is the empty string is a literal
  reproducible value (F032 D3) — `referenceOf` is total over it and distinguishes it from a non-empty digest.
- **Failed run**: A non-zero `ExitCode` is a reproducible fact (F032 records failures, FR-003); `referenceOf` is
  total over it and yields a reference distinct from the same command at a different exit code. **Whether a
  failed gate's evidence should be captured at all** is a host-row reuse-policy decision, explicitly **out of
  scope** here — this core derives a reference and records whatever record it is handed (no policy).
- **Captured-output outcome**: `NoCapturedOutput`, `CapturedAt (CapturedOutputPath "")`, and `CapturedAt
  (CapturedOutputPath "x")` are three distinct reproducible outcomes (F032 FR-011); they yield three pairwise
  distinct references.
- **Empty store**: `capture inputs record EvidenceReuse.empty` yields a one-entry store; `referenceOf` needs no
  store.
- **Duplicate capture of the identical world + record**: folds a second identical entry per the F030 `record`
  convention (no dedup is this core's job); `decide` still serves that reference. The F047 `prune` already
  collapses superseded worlds at persist time — capture adds no dedup policy.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a pure operation that derives an opaque evidence reference (`EvidenceRef`,
  F030) from an executed gate's `CommandRecord` (F032), so a real run records a real reference rather than a
  synthetic literal.
- **FR-002**: The derived reference MUST be a function of the command record's **reproducible** facts only — it
  MUST be computed from the F032 reproducible identity (`CommandRecord.canonicalId`) and MUST NOT read the
  record's `SensedDuration`. Two records differing **only** in duration MUST yield the byte-identical reference
  (reproducibility, FR duration-invariance).
- **FR-003**: The derived reference MUST distinguish any two records that differ in **any** reproducible fact
  (executable, an argument or its order, working directory, the environment delta compared as a set, timeout,
  exit code, either output digest, or the captured-output outcome) — i.e. the derivation is injective over the
  F032 identity it reuses.
- **FR-004**: The system MUST provide a pure operation that folds one executed gate's evidence into a supplied
  `ReuseStore` — pairing that gate's resolved `FreshnessInputs` (F029/F043) with the derived reference — by
  reusing the F030 `EvidenceReuse.record` convention **verbatim** (newest-first, store in / store out). It MUST
  introduce no new reuse policy, no new store representation, and no new evidence representation.
- **FR-005**: After capturing world `W`'s evidence into a store, `EvidenceReuse.decide W` over the result MUST
  return `Reuse r` where `r` equals the reference derived from the captured record — the captured world becomes
  reusable and serves exactly the derived reference (the close-the-loop round-trip).
- **FR-006**: Capture MUST be recompute-safe: relative to the input store, it may only make the **just-captured**
  world reusable; for every **other** candidate world, `decide` over the captured store MUST return exactly what
  it returned over the input store (capture never fabricates a match for an unrelated world, never weakens a
  prior verdict).
- **FR-007**: Both operations MUST be **pure and total**: defined for every input (including the empty store,
  empty digests, a failed exit code, and every captured-output outcome), never throwing; reading no clock,
  filesystem, git, environment, or network; spawning no process; hashing no bytes; and byte-for-byte identical
  for identical input regardless of evaluation time, machine, process, or collection order.
- **FR-008**: The reference and the resulting store MUST be deterministic and byte-stable: identical inputs
  produce the byte-identical `EvidenceRef` and the byte-identical store on every run and machine — no
  wall-clock, GUID, path, locale, or environment leakage.
- **FR-009**: The change MUST be additive and self-contained: a new value-only library reusing F032
  `CommandRecord`, F030 `EvidenceReuse`, and F029 `FreshnessKey` vocabulary verbatim; **no** new third-party
  dependency; **no** schema-version bump; **no** edit to any existing core, host command, golden baseline, or
  reader-accepted shape. The library is referenced by nothing on landing (the host wiring is the next row).
- **FR-010**: A captured reference MUST round-trip losslessly through the existing store-persistence path: a
  store grown by `capture` then run through F047 `serialise` and re-read by the F046 reader MUST preserve the
  captured entry's full freshness world and the exact derived reference (the captured reference is rendered
  verbatim, never re-parsed or re-hashed by persistence).

### Key Entities *(include if feature involves data)*

- **CommandRecord** (F032, reused): an executed command's reproducible facts (executable, arguments, working
  directory, environment delta, timeout, exit code, stdout/stderr digests, captured-output outcome) held apart
  from the one sensed fact (`SensedDuration`); already foldable to a byte-stable `CommandIdentity` via
  `canonicalId`. The **input** evidence of this row.
- **EvidenceRef** (F030, reused): the opaque, comparable reference string the store holds — no validation, no
  parsing. The **derived output** of this row.
- **FreshnessInputs** (F029, reused): the complete freshness world (rule hash, covered artifacts, command
  version, generator version, base/head, environment class, output digest) a gate ran under, as resolved by
  F043. The world a captured reference is recorded **against**.
- **ReuseStore** (F030, reused): the immutable newest-first collection of recorded entries; a value handed in
  and returned. The capture **target**.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001** (close the loop): For every `(CommandRecord, FreshnessInputs)` pair, capturing into a store and then
  asking `decide` about that same world returns `Reuse` with the exact derived reference — 100% of captured
  worlds become reusable and serve their own reference.
- **SC-002** (duration-invariance): For every pair of command records identical in all reproducible facts and
  differing only in `SensedDuration`, the two derived references are byte-identical — 0% reference variation
  attributable to the sensed wall-clock.
- **SC-003** (reproducible-fact sensitivity): For every single-field perturbation of a reproducible fact, the
  derived reference changes — 100% of distinct reproducible identities map to distinct references.
- **SC-004** (recompute-safety): For every input store and every captured world, no candidate world other than
  the captured one changes its `decide` verdict — 0 regressions of an unrelated world's reuse/recompute verdict.
- **SC-005** (determinism / byte-stability): Re-running `referenceOf` and `capture` on identical inputs yields
  byte-identical output across runs and machines — 100% reproducible, with no clock/GUID/path/locale/env input.
- **SC-006** (additive): The library adds zero third-party dependencies, bumps zero schema versions, and edits
  zero existing cores, host commands, or golden baselines; the full solution build and test suite stay green and
  every pre-existing artifact remains byte-identical.
- **SC-007** (lossless persistence round-trip): A store grown by `capture`, serialised by F047 and re-read by the
  F046 reader, preserves the captured entry's freshness world and exact reference — 100% lossless.
- **SC-008** (no I/O in the core): Every semantic test of this core runs with no filesystem, clock, process, or
  network access.

## Assumptions

- **Pure-core-first scope is intentional and maintainer-confirmed** (this session, via AskUserQuestion): this row
  is the value-only reference-derivation + store-fold bridge, mirroring F047's pure write half. The impure host
  row that follows (the F048 analogue) is what actually executes gates and records during a run.
- **The reference is the F032 reproducible identity, wrapped.** The cleanest reproducible, byte-stable,
  injective-over-reproducible-facts reference already exists as `CommandRecord.canonicalId`; this row reuses it
  rather than inventing a second identity scheme or hashing bytes itself (F032 D3/D10 — no hashing here either).
  The reference string is therefore `identityValue (canonicalId record)`, wrapped as an `EvidenceRef`.
- **The caller supplies the executed record and the resolved world.** This core does not sense facts, run gates,
  capture digests, or resolve freshness — it consumes an already-built F032 `CommandRecord` and an already-resolved
  F043 `FreshnessInputs`. Producing those is the host row's job.
- **Capture is mechanical, not policy.** It records whatever record it is handed; gating on exit code, success,
  or freshness-resolution outcome is a host-row reuse-policy decision (out of scope), preserving the no-new-policy
  guarantee.
- **Standard determinism / no-clock discipline** applies as to every core in this thread.

## Out of Scope

- **Executing gates / running processes / capturing real output digests / building the `CommandRecord`** — the
  impure host row (the F048 analogue) that wires this core into `fsgg route` / `fsgg ship` and records during a
  run. This row consumes an already-built `CommandRecord`; it produces none.
- **Reuse policy over captured evidence** — whether a *failed* (non-zero exit) gate's evidence should be captured
  or suppressed, and any success/exit-code gating. This core records what it is given.
- **Any change to the `fsgg.evidence-reuse-store/v1` schema, the F046 read-only reader's accepted shape, the
  F047 `serialise`/`retain`/`prune` policy, or the `route.json` / `audit.json` schema or content.**
- **Wall-clock TTL / age-based expiry or any `RecordedEvidence` timestamp model change** (F047 deferred this; it
  remains deferred).
- **Editing merged F029–F048 cores, their golden baselines, the F042/F044 standalone `cache-eligibility.json`
  sidecar, or anything in Phase 13.**
