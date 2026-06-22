# Feature Specification: Emit Real Cache-Eligibility Verdicts From `fsgg route` and `fsgg ship`

**Feature Branch**: `046-route-ship-cache-eligibility`

**Created**: 2026-06-22

**Status**: Draft

**Input**: User description: "next item in plan." — resolved (maintainer-confirmed this session, via
AskUserQuestion, over the real-cache-store row and jumping to Phase 13) to the **host wiring** that closes
the cache-eligibility thread end-to-end: make the `fsgg route` (F022) and `fsgg ship` (F026) host commands
actually *sense* each selected gate's freshness facts from the real repository, *resolve* them (F043),
*evaluate* cache eligibility (F041), and supply the resulting **real** per-gate `CacheEligibilityReport`
to the F045 embed — so `route.json` and `audit.json` finally carry genuine `reusable` / `mustRecompute`
verdicts instead of the placeholder *not-evaluated* section they emit today.

## Overview

The cache-eligibility thread is one wire away from complete. Every pure core exists and is merged: the
per-gate freshness-inputs resolution join (F043 `FreshnessResolution.resolve`), the per-gate cache roll-up
(F041 `CacheEligibility.evaluate`), the standalone `cache-eligibility.json` projection (F042), and — most
recently — the **embed** (F045) that taught `RouteJson.ofRouteResult` and `AuditJson.ofShipDecision` to
accept an *optional* `CacheEligibilityReport` and render, per gate matched by `GateId`, that gate's
verdict beside existing content. F044 already proved the impure composition end-to-end in a *standalone*
command: sense each gate's freshness facts at the effects boundary → assemble F043 `SensedFacts` →
`resolve` → `evaluate` over a read-only reuse store → project.

But the two artifacts consumers actually read — `route.json` from `fsgg route` and `audit.json` from
`fsgg ship` — still pass `None` to that embed. They emit an honest **not-evaluated** cache section because
no freshness inputs are ever resolved inside them. The verdict the whole thread was built to deliver never
reaches the documents people and CI look at.

This feature is the missing wire. It teaches **both** existing host commands to run the cache-eligibility
pipeline F044 already established — reusing F044's freshness sensing and read-only store loading, plus the
F043/F041 composition, verbatim — over the gates each command already selects, and to pass the resulting
**real** `CacheEligibilityReport` (`Some report`) into the F045 embed. After this row, a developer who runs
`fsgg route` sees, on each selected-gate entry, whether prior evidence may be reused or the gate must
recompute and why; a maintainer who runs `fsgg ship` sees the same verdict on each gate item of the
ship audit, beside its enforcement detail.

It composes already-typed, already-tested values; it **re-derives, re-senses, re-resolves, re-classifies,
and re-evaluates nothing** that a merged core already owns, and it adds no new sensing technique F044 did
not establish. Two invariants are load-bearing and inviolable:

1. **Cache eligibility is information, not a verdict.** Wiring it in changes neither command's existing
   behavior beyond filling in the cache section: `fsgg route` still always succeeds (exit 0); `fsgg ship`'s
   pass/fail verdict, three-way blockers/warnings/passing partition, enforcement detail, and exit-code
   basis are **untouched** by any cache verdict. A `reusable` verdict on a blocking gate leaves that gate a
   blocker. The cache section is additive information layered on the documents the commands already emit.
2. **Cache sensing introduces no new way to fail.** If the freshness facts cannot be sensed or the reuse
   store cannot be read, the command does **not** newly fail or change its exit code; it emits its document
   with the cache section honestly degraded — affected gates marked *must-recompute by default* or
   *unresolved* with their missing facts named (no-hide) — preserving every existing success and failure
   path of `fsgg route` / `fsgg ship`.

It is deliberately **scoped to the embed path only**. The standalone F042/F044 `cache-eligibility.json`
command and sidecar are left untouched. Writing, evicting, or expiring a real evidence-reuse store is a
later row — the store this command reads stays **read-only**, and when absent (the state today) it is the
empty store, so every gate resolves to *must-recompute* (`noPriorEvidence`) by safe default until a real
store exists. Phase 13 (Release & Distribution) remains out of scope.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - `fsgg route` carries real cache verdicts in route.json (Priority: P1)

A developer runs `fsgg route` against a working tree with pending changes. The emitted `route.json` now
shows, on **each selected-gate entry**, a real cache-eligibility verdict: whether that gate may reuse prior
evidence (`reusable`, with its opaque evidence reference) or must recompute (`mustRecompute`, naming the
cause — `noPriorEvidence`, or `inputsChanged` naming exactly which freshness inputs changed). With no real
store yet, every selected gate honestly reads `mustRecompute / noPriorEvidence`, but the section is now an
**evaluated** report, not the *not-evaluated* placeholder.

**Why this priority**: This is the headline value of the entire cache-eligibility thread — the verdict
finally reaching the artifact consumers read. `fsgg route` is the lower-risk of the two commands (it
already always exits 0, makes no merge decision), so it is the natural first proof that the wire is sound.

**Independent Test**: Run `fsgg route` against a fixture repo with selected gates; assert `route.json`'s
top-level cache section reports *evaluated* (not the `None` not-evaluated state) and each selected-gate
entry carries a verdict matched by `GateId`, with the cause named for each `mustRecompute`. Delivers the
real per-gate verdict on the route artifact.

**Acceptance Scenarios**:

1. **Given** a repo whose change selects one or more gates and **no** reuse store on disk, **When** a
   developer runs `fsgg route`, **Then** `route.json` reports the cache section as *evaluated* and every
   selected-gate entry carries `mustRecompute` with cause `noPriorEvidence`.
2. **Given** a repo with a routed change, **When** `fsgg route` runs, **Then** every other field of
   `route.json` (selected gates, route trace, findings, cost rollup, schema version) is exactly what
   `fsgg route` produced before this wiring — only the cache section changes from *not-evaluated* to
   *evaluated*.
3. **Given** a routed change, **When** `fsgg route` runs, **Then** the command still exits 0 regardless of
   whether gates are reusable, must-recompute, or unresolved.

---

### User Story 2 - `fsgg ship` carries real cache verdicts without altering the ship verdict (Priority: P1)

A maintainer runs `fsgg ship` under a chosen mode/profile. The emitted `audit.json` now carries a real
cache-eligibility verdict on **each gate item** of the blockers/warnings/passing partition, beside that
gate's full enforcement detail. The cache verdict is purely informational: the ship pass/fail verdict, the
partition, every enforcement field, and the numeric exit code are identical to what `fsgg ship` produced
before this wiring. Finding items carry no cache verdict (cache reuse is gate-scoped).

**Why this priority**: `fsgg ship` is the protected-branch gate; its verdict and exit code are
safety-critical. Delivering the real cache verdict here while **proving** it never perturbs the ship
decision is the core risk this row must retire, and is equal in importance to US1.

**Independent Test**: Run `fsgg ship` against a fixture repo; assert each `kind:"gate"` audit item carries
a verdict matched by `GateId`, each `kind:"finding"` item carries none, and the verdict, partition,
enforcement fields, and exit code are byte-for-byte / value-for-value identical to a pre-wiring run of the
same input.

**Acceptance Scenarios**:

1. **Given** a repo with selected gates and no reuse store, **When** a maintainer runs `fsgg ship`, **Then**
   each gate item in `audit.json` carries `mustRecompute / noPriorEvidence` and each finding item carries no
   cache verdict.
2. **Given** any change and any mode/profile, **When** `fsgg ship` runs, **Then** the ship verdict
   (pass/fail), the blockers/warnings/passing partition, every per-item enforcement field, the exit-code
   basis, and the numeric process exit code are identical to a run of the same input before this wiring.
3. **Given** a gate that the cache step would mark `reusable`, **When** that gate is a base-blocking gate,
   **Then** it remains a blocker — the cache verdict never relaxes, hides, or reclassifies it.

---

### User Story 3 - Honest degradation when facts can't be sensed or the store can't be read (Priority: P2)

The freshness facts for a gate cannot be fully sensed (e.g. a covered artifact or command version is
unavailable), or the reuse store file is present but unreadable. The command does **not** newly fail and
does **not** change its exit code. It emits its document with the cache section honestly degraded: a gate
whose inputs could not be resolved is marked recompute-by-default with its missing facts named (no-hide); a
store that cannot be read degrades to the empty store (recompute-by-default for all gates). No gate is ever
silently reported `reusable`.

**Why this priority**: The no-hide / safe-failure discipline is the thread's correctness contract and the
guarantee that wiring cache sensing into the safety-critical commands cannot weaken them. It is essential
but secondary to the headline verdict delivery (US1/US2).

**Independent Test**: Run each command against a fixture where a required freshness fact is unsensed and
separately where the store path is unreadable; assert the document still emits, the affected gates show
recompute-by-default with named missing facts, no gate is `reusable`, and the command's exit code is
unchanged from the all-resolvable case.

**Acceptance Scenarios**:

1. **Given** a selected gate whose required freshness facts cannot all be sensed, **When** `fsgg route` /
   `fsgg ship` runs, **Then** that gate is reported recompute-by-default (never `reusable`), its missing
   facts are named, and the command's exit code is unchanged.
2. **Given** a reuse store path that exists but cannot be parsed, **When** either command runs, **Then** the
   command does not newly fail; it proceeds as if the store were empty (all gates `mustRecompute /
   noPriorEvidence`) and the failure to read is surfaced honestly in the command's summary output.
3. **Given** any partially-resolvable change, **When** either command runs, **Then** no gate is reported
   `reusable` unless its inputs fully resolved and the (read-only) store actually matched.

---

### User Story 4 - Deterministic, byte-stable artifacts (Priority: P3)

Running the same command twice against the same repository state produces byte-identical `route.json` /
`audit.json`, including the cache section. The cache verdicts follow the document's existing gate order, and
the wiring introduces no nondeterminism (no wall-clock, no ordering instability) beyond the sensing already
present in the commands.

**Why this priority**: Determinism is required for these artifacts to be diffable and CI-comparable, but it
is a property the underlying cores already guarantee; this row need only preserve it.

**Independent Test**: Project the same fixture repo twice; assert byte-identical documents including the
cache section, and that cache entries appear in the same order as the documents' existing gate order.

**Acceptance Scenarios**:

1. **Given** a fixed repository state, **When** either command runs twice, **Then** the two emitted
   documents are byte-identical including the cache section.
2. **Given** a routed change, **When** either command runs, **Then** each gate's cache verdict appears in the
   same position as that gate's existing entry/item in the document.

---

### Edge Cases

- **Empty route / no selected gates**: the command emits its document with an *evaluated* cache section
  carrying no per-gate verdicts (distinct from the *not-evaluated* placeholder); exit code unchanged.
- **Finding-only change (no gates)**: findings render as before; no cache verdict attaches to any finding.
- **Gate declaring no command**: resolves with absent command/command-version (consistent absence, not
  unresolved) per F043; evaluated normally.
- **Store file absent** (the state today): treated as the empty store ⇒ every gate `mustRecompute /
  noPriorEvidence`; exit code unchanged; success.
- **Store file present but malformed**: degrades to empty store (US3), failure surfaced in summary, exit
  code unchanged — never crashes the command.
- **Duplicate `GateId`** in the selection/document: the F045 embed's deterministic reconciliation governs
  rendering; this row supplies the report and changes no reconciliation rule.
- **A gate selected but absent from the resolved report** (e.g. unresolved): the F045 embed renders it
  *not-evaluated / recompute-by-default* per its no-hide rule; never `reusable`, never omitted.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `fsgg route` MUST sense each selected gate's freshness facts from the real repository at the
  effects boundary (base/head revisions, rule-pack hash, generator version, per-gate covered-artifact
  hashes, per-command command versions), reusing the freshness-sensing technique F044 established, and MUST
  add no new sensing technique beyond it.
- **FR-002**: `fsgg ship` MUST sense the same freshness facts in the same way, for the gates it selects.
- **FR-003**: Both commands MUST assemble the sensed facts into the F043 `SensedFacts` bundle and call F043
  `resolve` to obtain, per selected gate, a complete resolved input set or a no-hide *unresolved* outcome —
  reusing F043 verbatim, fabricating no hash, revision, or version.
- **FR-004**: Both commands MUST run F041 `CacheEligibility.evaluate` over the resolved candidate gates
  against a reuse store, reusing F041 verbatim, and MUST NOT introduce any new reuse policy.
- **FR-005**: The reuse store MUST be loaded **read-only**; an absent store MUST be treated as the empty
  store (every gate `mustRecompute / noPriorEvidence` by safe default). This row MUST NOT write, evict, or
  expire any store entry.
- **FR-006**: `fsgg route` MUST pass the resulting `CacheEligibilityReport` as `Some report` to the F045
  `RouteJson.ofRouteResult` embed, so `route.json` reports the cache section as *evaluated* and carries each
  selected gate's verdict; it MUST NOT pass `None` (the not-evaluated placeholder) on a successful sense.
- **FR-007**: `fsgg ship` MUST pass the resulting `CacheEligibilityReport` as `Some report` to the F045
  `AuditJson.ofShipDecision` embed, so each gate item of `audit.json` carries its verdict and each finding
  item carries none.
- **FR-008**: Wiring cache eligibility MUST NOT change `fsgg route`'s behavior beyond filling the cache
  section: it still always exits 0, and every other `route.json` field is identical to a pre-wiring run of
  the same input.
- **FR-009**: Wiring cache eligibility MUST NOT change `fsgg ship`'s pass/fail verdict, its
  blockers/warnings/passing partition, any per-item enforcement field, its exit-code basis, or its numeric
  process exit code; a `reusable` verdict MUST NOT relax, hide, or reclassify any blocker.
- **FR-010**: A gate whose freshness inputs cannot be fully resolved MUST be reported recompute-by-default
  (never `reusable`) with its missing facts named (no-hide); the command MUST still emit its document and
  MUST NOT newly fail or change its exit code on account of unresolved inputs.
- **FR-011**: A reuse store that is present but cannot be read/parsed MUST NOT newly fail the command; the
  command MUST proceed as if the store were empty and MUST surface the read failure honestly in its summary
  output, with its exit code unchanged.
- **FR-012**: Both emitted documents MUST be deterministic and byte-stable: identical repository state ⇒
  byte-identical documents including the cache section; cache verdicts MUST follow each document's existing
  gate order.
- **FR-013**: The commands MUST NOT dereference the opaque evidence reference, compute any freshness key or
  hash themselves, render any raw freshness input, or assign any cache-derived severity / enforcement /
  ship verdict — cache eligibility is information only.
- **FR-014**: This row MUST leave the standalone F042 `cache-eligibility.json` projection and the F044
  standalone command/sidecar untouched, and MUST NOT edit any merged pure core (F041/F042/F043/F045) — it
  composes them verbatim.
- **FR-015**: Each command's human / JSON summary output MUST reflect the cache outcome (which gates may
  reuse, which must recompute and why, which were unresolved, and any store-read failure), consistent with
  the document.

### Key Entities *(include if feature involves data)*

- **Sensed freshness facts**: the bundle of repository facts sensed per run — base/head revisions, rule-pack
  hash, generator version, per-gate covered-artifact hashes, per-command command versions — assembled into
  the F043 `SensedFacts` shape. Sensed at the effects boundary; never fabricated.
- **Read-only reuse store**: the evidence-reuse store the command reads but never mutates; absent ⇒ empty.
- **Cache-eligibility report**: the per-gate roll-up (F041 `CacheEligibilityReport`) produced by evaluating
  resolved candidates against the store; supplied to the F045 embed as `Some report`.
- **Enriched route.json / audit.json**: the existing documents, now with an *evaluated* cache section and a
  real per-gate verdict — every non-cache field unchanged.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Running `fsgg route` against a routed change emits a `route.json` whose cache section is
  *evaluated* and whose every selected-gate entry carries a verdict matched by `GateId`, with the cause
  named for each `mustRecompute`.
- **SC-002**: Running `fsgg ship` emits an `audit.json` in which every gate item carries a cache verdict and
  every finding item carries none.
- **SC-003**: For every mode/profile and change, the `fsgg ship` verdict, partition, enforcement fields,
  exit-code basis, and numeric exit code are identical with and without this wiring on the same input
  (verified against the pre-wiring golden expectations).
- **SC-004**: Every non-cache field of `route.json` and `audit.json` is byte-identical to a pre-wiring run
  of the same input (the cache section is the only delta).
- **SC-005**: With no store on disk, 100% of selected gates report `mustRecompute / noPriorEvidence`; no gate
  reports `reusable`.
- **SC-006**: Injecting an unsensed required fact, or an unreadable store, leaves the command's exit code
  unchanged and produces a document in which affected gates are recompute-by-default with named missing
  facts and no gate is `reusable`.
- **SC-007**: Running either command twice against the same repository state yields byte-identical
  documents including the cache section.
- **SC-008**: The full solution builds clean and all existing projects' tests stay green; the standalone
  F042/F044 artifacts and all merged pure cores are unchanged (no edits, no re-bless of their baselines).

## Assumptions

- **Freshness sensing reuses F044's technique verbatim.** Base/head from the snapshot range; rule-pack hash
  over `.fsgg/*.yml`; generator version from the tool assembly version; covered-artifact hashes over the
  repo-wide surface (the F044 MVP coverage); command versions per declared command. No richer or per-gate
  coverage sensing is introduced (deferred).
- **The reuse store is read-only and effectively empty today.** No store-writing row exists yet, so the
  realistic outcome is all-`mustRecompute / noPriorEvidence`; the wiring is nonetheless real (evaluated,
  not placeholder) and ready for a future store-writing row.
- **The store path / discovery mirrors F044's convention** (a conventional default path; absent ⇒ empty).
  Whether either command exposes a flag to point at a store, and the exact default path, is a plan-time
  mechanism decision; the safe default (absent ⇒ empty ⇒ recompute-by-default) holds regardless.
- **Whether F044's freshness-sensing port + read-only store-reader are extracted into shared code or
  duplicated into RouteCommand/ShipCommand** is a plan-time structural decision (DRY vs locality); the spec
  requires only that the technique is reused verbatim, not where the code lives.
- **The F045 embed and its v2 schema are reused as-is.** This row supplies a real report where the commands
  previously supplied `None`; it does not change the document shape, vocabulary, schema version, or the
  embed's matching/reconciliation rules.
- **The command tests recompute their expected `route.json` / `audit.json` live**, so passing a real report
  is a behavior change verified against recomputed expectations; the F028 golden `audit.json` snapshots
  (projected with `None` by the fixture generator) stay on the not-evaluated path and are unaffected by this
  row, which touches only the live host commands.

## Out of Scope

- Writing, evicting, or expiring a real evidence-reuse store (the next cache row) — the store here is
  read-only and absent ⇒ empty.
- Richer or per-gate covered-artifact / command-version sensing beyond the F044 MVP technique.
- Any change to the standalone F042 `cache-eligibility.json` projection or the F044 standalone command /
  sidecar.
- Any edit to the merged pure cores (F041/F042/F043/F045) or re-bless of their golden baselines.
- Any cache-derived severity, enforcement, ship verdict, exit-code change, or provenance.
- Phase 13 (Release & Distribution Readiness).
