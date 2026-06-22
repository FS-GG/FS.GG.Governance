# Feature Specification: Persist The Evidence-Reuse Store To Disk From The Host Commands

**Feature Branch**: `048-evidence-reuse-store-persist`

**Created**: 2026-06-22

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved (via AskUserQuestion,
over the gate-execution/real-evidence row and Phase 13) to the deferred **store-persistence host row**: wire
F047's pure serialise/retain/prune core into a real on-disk write inside the host commands that already
consult the evidence-reuse store, so the store the cache thread reads (`fsgg.evidence-reuse-store/v1`) becomes
**writable on disk** where today it is strictly read-only.

## Context

The cache-eligibility thread (F029–F046) is wired end-to-end: `fsgg route` and `fsgg ship` sense each selected
gate's freshness facts, resolve them (F043), evaluate reuse against an evidence-reuse store (F030 → F041), and
emit per-gate `reusable` / `mustRecompute` verdicts into `route.json` / `audit.json` (F045/F046). F047 then
delivered the **pure write half** as a value-only core: `EvidenceReuseStore.serialise` (the deterministic,
byte-stable inverse of the read-only deserializer), `retain` (bounded eviction, newest-first), and `prune`
(superseded-world expiry) — all recompute-safe, all pure, **none wired to disk**.

So today the store is read but never written. `FreshnessSensing.realStoreReader` deserializes the file and
`loadStore` maps an absent file to `EvidenceReuse.empty`; both `fsgg route` and `fsgg ship` already discover a
store path (`--store`, default `<repo>/readiness/evidence-reuse.json`) and load it read-only via the
`LoadStore` effect. But there is **no writer effect** — nothing serialises a `ReuseStore` back, nothing keeps
the on-disk store bounded or pruned across runs. The pure inverse exists (F047); the impure edge does not.

This row delivers that **impure edge**: an atomic store-write effect, wired into the host command(s) that
already load the store, behind an explicit opt-in trigger, applying F047's `prune` + `retain` to the loaded
store before serialising it back to its `v1` document. It mirrors how F044/F046 wired the read side and how
the route/ship interpreters already write `route.json` / `audit.json` atomically (temp + rename).

It does **not** produce real evidence. A genuine `EvidenceRef` requires gate **execution** (running a gate's
command and capturing its output digest) — a capability not yet built and an explicit later row. Because no
new evidence is recorded during a `route`/`ship` run, the write this row performs is a **maintenance write**:
it persists the loaded store back, pruned and bounded, establishing the durable write path so that when the
later execution row begins `record`-ing real evidence, the durable write is already in place. The cache cannot
warm with real reuse until that execution row lands; this row makes the store *durably writable, bounded, and
self-pruning on disk* — the structural prerequisite.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Write the store back to disk so it survives across runs (Priority: P1)

A maintainer runs a host command that consults the evidence-reuse store and wants the store it loaded to be
persisted back to its `fsgg.evidence-reuse-store/v1` file — atomically, deterministically, and losslessly — so
that a later run's read-only reader loads exactly what was written. Today the host can only *read* the store;
there is no path by which a `ReuseStore` value reaches disk in the on-disk format.

**Why this priority**: This is the MVP and the whole point of the row — closing the read-only/no-writer gap by
giving the store a durable on-disk write edge. Bounding and pruning the persisted store (US2) and the safety
invariants (US3) are refinements of a store that can first be written to disk at all.

**Independent Test**: Point the command at a repo with an existing `v1` store file, run with persistence
enabled, and assert the file on disk after the run re-reads (through the existing `FreshnessSensing` reader)
to a store equal to what the command wrote, that the write was atomic (no partial/truncated file is ever
observable, even on an induced write failure), and that running twice with no input change yields a
byte-identical store file (determinism).

**Acceptance Scenarios**:

1. **Given** a repo whose evidence-reuse store path holds a well-formed non-empty `v1` document, **When** the
   command runs with persistence enabled, **Then** the store file on disk after the run is a well-formed `v1`
   document that re-reads through the existing read-only reader to a `ReuseStore` value (lossless round-trip),
   and the opaque evidence references and freshness inputs it contains are preserved verbatim.
2. **Given** the store path does not exist on disk, **When** the command runs with persistence enabled, **Then**
   the store loads as the empty store and the command persists a well-formed `v1` document (creating any
   missing parent directory), distinct on disk from an absent file but re-reading as the empty store.
3. **Given** identical inputs and an identical loaded store, **When** the command is run twice with persistence
   enabled, **Then** the two persisted store files are byte-identical (deterministic write).
4. **Given** the persisted store file is then consumed by a subsequent `fsgg route` / `fsgg ship` run, **When**
   that run loads it, **Then** it loads without error as the store that was written (the write target is
   exactly the shape the existing reader accepts; no schema-version bump).

---

### User Story 2 - Keep the persisted store bounded and pruned (Priority: P2)

Across many runs an on-disk store would otherwise accumulate one entry per distinct freshness world per gate
and grow without limit. A maintainer wants each persisted write to keep the on-disk store within a bounded
size and free of superseded-world dead entries, so disk usage and read cost stay flat — **without ever
causing a stale reuse**.

**Why this priority**: Important for a store written every enabled run, but a store that is merely written
(US1) is already useful for the immediate next run; bounding and pruning matter once writes accumulate. It is
the host's application of F047's `retain` + `prune`.

**Independent Test**: Run persistence against a repo whose store file deliberately exceeds the retention bound
and/or contains a strictly-superseded entry, and assert the persisted file is within the bound, retains the
newest entries in newest-first order, contains no strictly-superseded entry, and that every persisted entry is
byte-for-byte one of the loaded entries (the host removes whole entries only — it never mutates or fabricates
an entry).

**Acceptance Scenarios**:

1. **Given** a loaded store larger than the retention bound, **When** the command persists it, **Then** the
   on-disk result is within the bound and holds the most-recent retained entries in newest-first order.
2. **Given** a loaded store containing an entry a strictly-newer entry already supersedes (the same freshness
   world for that gate), **When** the command persists it, **Then** the superseded entry is absent from the
   persisted file and the survivors are a newest-first subset of the loaded entries.
3. **Given** a loaded store already within the bound and free of superseded entries, **When** the command
   persists it, **Then** the persisted store is value-equal to the loaded store (no spurious reordering,
   rewriting, or fabrication beyond canonical serialisation).

---

### User Story 3 - Persistence never changes a verdict, never fails the command (Priority: P3)

A maintainer relies on `fsgg route` / `fsgg ship` for their current contract: `route`'s exit code (0 on a
normal run; its existing input/tool-error codes otherwise), and `ship`'s pass/fail verdict,
blockers/warnings/passing partition, every enforcement field, and exit code are authoritative. Turning on store persistence must not perturb any of that, and a store-write problem must not
turn a passing command into a failing one.

**Why this priority**: A safety/honesty guarantee layered on top of a writable, bounded store; essential to
ship the row but expressed once the write (US1) and maintenance (US2) behaviour exist.

**Independent Test**: With persistence enabled, run both commands and assert: the cache verdicts emitted into
`route.json` / `audit.json` for the current run are computed from the **loaded** store and are identical to the
verdicts the same run produces with persistence disabled (the write does not feed back into the current run);
inducing a store-write failure leaves the command's exit code, emitted `route.json` / `audit.json`, and ship
verdict unchanged, surfaces a non-fatal note in the summary, and leaves no partial store file. With
persistence disabled, every emitted artifact is byte-identical to the pre-row baseline.

**Acceptance Scenarios**:

1. **Given** persistence is enabled, **When** `fsgg route` / `fsgg ship` run, **Then** the per-gate cache
   verdicts written into `route.json` / `audit.json` are exactly those derived from the **loaded** store
   (unchanged by the subsequent prune/retain/write), and `fsgg ship`'s verdict, partition, enforcement
   detail, and exit code are byte-for-byte what they would be with persistence disabled.
2. **Given** the store write fails (e.g. the target is unwritable), **When** the command runs, **Then** the
   command's exit code is unchanged (`route` still 0; `ship` still governed solely by its verdict basis), the
   emitted `route.json` / `audit.json` are unchanged, no partial/truncated store file is left on disk, and the
   failure is surfaced as a non-fatal note in the run summary.
3. **Given** persistence is disabled (the default), **When** the commands run, **Then** no store file is
   written and every emitted artifact and existing golden baseline is byte-identical to before this row.

---

### Edge Cases

- **No real evidence to add**: Because gate execution is out of scope, no new entry is recorded during a run;
  the persisted store is the loaded store after prune + retain (a maintenance write), not a warmed cache. This
  is expected — the row makes the store durably writable, not populated with real reuse.
- **Absent store file + persistence enabled**: The store loads as empty; persisting the empty store writes a
  well-formed `v1` document with an empty entry list (creating parent directories as needed), distinct on disk
  from the absent file but re-reading as the empty store.
- **Malformed store file on load**: Handled by the existing F046 degrade policy (a malformed store degrades to
  the empty store with a non-fatal note); this row does not change load behaviour. Whether a degraded-to-empty
  load should overwrite the malformed file on write is a plan-time decision (default: do not silently clobber a
  file that failed to parse — see Assumptions).
- **Write target equals a path also read this run**: The store is fully loaded into memory before the write;
  the atomic temp + rename write replaces the file wholesale, so reading and writing the same path within a run
  is safe.
- **Retention bound smaller than the number of distinct gates / worlds**: The persisted store is still
  within-bound and recompute-safe; per-gate fairness is not required in this row (newest-first global retention
  — F047's default — applies).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST add a durable, atomic write of the evidence-reuse store to its
  `fsgg.evidence-reuse-store/v1` file at the effects boundary of the host command(s) that already load the
  store (`fsgg route` and `fsgg ship`), reusing the existing atomic temp-write-then-rename persistence
  technique so a failed write leaves no partial or truncated file.
- **FR-002**: The persisted document MUST be produced by F047's `EvidenceReuseStore.serialise` verbatim, so the
  round-trip persisted-file → existing `FreshnessSensing.realStoreReader` is lossless (every entry's full
  freshness-input set and opaque evidence reference preserved, in newest-first order) and byte-stable.
- **FR-003**: Before serialising, the host MUST apply F047's `prune` then `retain` (a deterministic retention
  bound) to the **loaded** store, so the persisted store is bounded and free of strictly-superseded entries,
  removing only whole entries (never mutating or fabricating one).
- **FR-004**: Store persistence MUST be **opt-in** via an explicit flag, defaulting to **off**, so that with
  the default the host writes no store file and every emitted artifact and existing golden baseline is
  byte-identical to before this row (additive-only).
- **FR-005**: The store-write MUST be **decoupled from the current run's cache verdicts**: the per-gate
  `reusable` / `mustRecompute` verdicts emitted into `route.json` / `audit.json` MUST be computed from the
  store as **loaded** and MUST NOT be affected by the prune/retain/write applied for the next run.
- **FR-006**: Store persistence MUST introduce **no new failure mode**: a store-write failure MUST be
  non-fatal — the command's exit code is unchanged (`fsgg route`'s exit code is whatever it would be without
  persistence — 0 on a normal run, its existing input/tool-error codes otherwise — never altered by a store-write
  outcome; `fsgg ship`'s exit code remains governed solely by its verdict basis), the emitted `route.json` /
  `audit.json` are unchanged, and the failure is surfaced as a non-fatal note in the run summary.
- **FR-007**: The store path MUST be discovered with the existing mechanism — the `--store` flag, defaulting to
  `<repo>/readiness/evidence-reuse.json` — used as the write target when persistence is enabled (the same path
  the read side already uses); no new path-discovery scheme is introduced.
- **FR-008**: The persistence MUST reuse the merged F047 `EvidenceReuseStore` (`serialise`/`retain`/`prune`)
  and F030 `EvidenceReuse` model verbatim, introducing no new reuse policy, freshness-match rule, evidence
  representation, serialisation format, or store schema version.
- **FR-009**: The change MUST be **additive**: it MUST NOT bump the `fsgg.evidence-reuse-store/v1` schema
  version, alter the read-only reader's accepted shape, change the `route.json` / `audit.json` schema or
  content, or edit any merged cache-thread core (F029–F047) or its golden baselines.
- **FR-010**: All decision logic for persistence (whether to write, what to write, how to degrade on write
  failure) MUST live in the host command's pure transition so it is testable without I/O; only the actual
  atomic file write executes at the effects boundary.

### Key Entities *(include if data involved)*

- **Evidence-reuse store file**: The on-disk `fsgg.evidence-reuse-store/v1` document at the discovered store
  path — until now read-only, now also the atomic write target.
- **Loaded reuse store (`ReuseStore`)**: The existing F030 value loaded at run start (absent ⇒ empty); the
  source the current run's verdicts are computed from and the input to prune/retain before persisting.
- **Persisted reuse store**: The loaded store after F047 `prune` + `retain`, serialised via F047 `serialise` —
  the bytes written atomically to the store file.
- **Store-write port**: The effects-boundary capability that writes (path, content) atomically (temp + rename);
  the existing artifact-writer technique reused for the store file.
- **Persistence trigger**: The explicit opt-in flag (default off) that enables the store write for a run.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: With persistence enabled, after a run the store file on disk re-reads through the existing
  read-only reader to the exact `ReuseStore` value the command persisted (lossless durable round-trip),
  verified end-to-end against the real reader.
- **SC-002**: Two enabled runs with identical inputs and identical loaded store produce byte-identical store
  files (deterministic persistence).
- **SC-003**: The persisted store is within the retention bound and contains no strictly-superseded entry,
  while every persisted entry is byte-for-byte one of the loaded entries (bounded, pruned, lossless w.r.t.
  surviving entries).
- **SC-004**: The per-gate cache verdicts in `route.json` / `audit.json` are identical whether persistence is
  on or off, and `fsgg ship`'s verdict/partition/enforcement/exit code are unchanged by enabling persistence
  (write decoupled from current-run verdicts).
- **SC-005**: An induced store-write failure leaves the command's exit code, emitted `route.json` /
  `audit.json`, and (for ship) verdict unchanged, leaves no partial store file on disk, and surfaces a
  non-fatal note (no new failure mode).
- **SC-006**: With persistence disabled (default), no store file is written and every emitted artifact and
  existing golden baseline is byte-identical to the pre-row baseline (additive-only).
- **SC-007**: The full solution adds no new third-party dependency, no schema-version bump, and zero edits to
  merged F029–F047 cores or their golden baselines; the existing read-only reader is unchanged and now consumes
  this row's persisted output unmodified.
- **SC-008**: The persistence decision logic is exercised by tests with no filesystem access (pure transition),
  and the atomic write is exercised by an effects-boundary test demonstrating temp + rename and no partial file
  on failure.

## Assumptions

- **Impure-edge slice (pattern-consistent)**: F047 delivered the pure serialise/retain/prune core; this row
  delivers only its impure on-disk edge wired into the host, mirroring how F044/F046 wired the read side and how
  the route/ship interpreters already persist `route.json` / `audit.json` atomically. Real evidence production
  (gate execution + output-digest capture) remains a separate later row.
- **Maintenance write, not a warmed cache**: Because no gate executes this row, no new `record` happens during a
  run; the persisted store is the loaded store pruned + bounded. The cache will not return real `reusable`
  verdicts until the later execution row records genuine evidence. This row's value is the durable, bounded,
  self-pruning write path — the prerequisite for that row.
- **Opt-in, default off (safe default)**: Persistence is gated behind an explicit flag defaulting off, so
  existing `fsgg route` / `fsgg ship` behaviour and every golden baseline stay byte-unchanged unless a caller
  opts in. This preserves the F046 invariant that the cache thread introduces no new failure mode and no
  surprising side effect on a read-oriented command. The exact flag name/spelling is a plan-time mechanism
  detail.
- **Both commands persist; the dedicated cache command is a plan-time candidate**: This row targets the two
  commands that already load the store (`fsgg route`, `fsgg ship`), mirroring F046. Whether the dedicated
  `fsgg cache-eligibility` command (F044) also gains the write, and whether the writer is shared code or per
  command, are plan-time decisions, not requirement gaps.
- **Retention bound**: The host uses F047's `defaultRetentionBound` unless a caller overrides it; the exact cap
  and any per-gate fairness are F047/plan mechanism details. Any chosen bound must satisfy F047's recompute-safe
  guarantees.
- **Malformed-load handling unchanged**: Load-time degrade-to-empty on a malformed store is the existing F046
  behaviour and is not changed here. The default is to **not** overwrite a store file that failed to parse
  (avoid silently clobbering a possibly-recoverable file); confirming this is a plan/clarify point.
- **Writer port**: The atomic write reuses the existing artifact-writer technique (temp + rename) at the
  effects boundary; no new third-party dependency is expected beyond what the host already references.

## Out of Scope

- Producing real evidence references (gate execution, command running, output-digest capture, `record`-ing new
  evidence into the store during a run).
- Wall-clock TTL / age-based expiry and any `RecordedEvidence` timestamp model change (F047 deferred this; it
  remains deferred).
- Any change to the `fsgg.evidence-reuse-store/v1` schema, the read-only reader's accepted shape, or the
  `route.json` / `audit.json` schema or content.
- Multi-writer concurrency / locking and cross-process coordination of the store file.
- Editing merged F029–F047 cores, their golden baselines, the F042/F044 standalone `cache-eligibility.json`
  sidecar, or anything in Phase 13.
