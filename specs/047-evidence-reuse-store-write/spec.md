# Feature Specification: Persist, Bound, And Prune The Evidence-Reuse Store

**Feature Branch**: `047-evidence-reuse-store-write`

**Created**: 2026-06-22

**Status**: Draft

**Input**: User description: "start the next item in the implementation plan." — resolved (via AskUserQuestion) to the deferred **cache-store write/evict/expire** follow-on: make the evidence-reuse store the cache thread reads (`fsgg.evidence-reuse-store/v1`) **writable, bounded, and self-pruning**, where today it is strictly read-only.

## Context

The cache-eligibility thread (F029–F046) is wired end-to-end: `fsgg route` and `fsgg ship` sense each
selected gate's freshness facts, resolve them (F043), evaluate reuse against an evidence-reuse store (F030 →
F041), and emit per-gate `reusable` / `mustRecompute` verdicts into `route.json` / `audit.json` (F045/F046).
But the store that pipeline consults is **read-only**: `FreshnessSensing.realStoreReader` deserializes
`fsgg.evidence-reuse-store/v1`, and `loadStore` maps an absent file to `EvidenceReuse.empty`. There is **no
writer**. F030 already ships the pure insert `record : FreshnessInputs -> EvidenceRef -> ReuseStore ->
ReuseStore` (drop the superseded full-match, cons the new entry newest-first), but **nothing serialises a
`ReuseStore` back to disk**, and **nothing bounds its growth or prunes dead entries**. So in practice every
run reads an absent/empty store and every gate verdict is `mustRecompute noPriorEvidence` — the cache can
never warm.

This row delivers the **write half** of the store lifecycle as a pure, total core — the deterministic
inverse of the existing read-only deserializer, plus a deterministic bounded-retention (eviction) and
dead-entry-pruning (expiry) policy — so the round-trip `serialise → realStoreReader` is lossless and the
store cannot grow without bound or accumulate entries that can never be reused again. It continues the
project's pure-core-first discipline: the impure on-disk persistence wiring (atomic write inside a host
command) and the production of *real* evidence references (which depends on gate **execution**, a capability
not yet built) are explicit later rows.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Persist a reuse store so a later run can warm the cache (Priority: P1)

A maintainer (or a later host row) holds an in-memory `ReuseStore` — the result of `record`-ing freshly
produced evidence into the store loaded at the start of a run — and needs to write it back to disk in the
`fsgg.evidence-reuse-store/v1` format so the **next** run's read-only reader loads it and the cache thread
can return `reusable` verdicts. Today this is impossible: the format has a reader but no writer.

**Why this priority**: This is the MVP and the whole point of the row. Without a deterministic, lossless
writer the store is permanently empty and every cache verdict is `mustRecompute noPriorEvidence` regardless
of how much of the cache machinery (F029–F046) is in place. Eviction and pruning (US2/US3) are refinements
of a store that can first be written at all.

**Independent Test**: Build a non-empty `ReuseStore` in memory, serialise it, feed the bytes to the existing
`FreshnessSensing.realStoreReader` / `loadStore`, and assert the loaded store equals the original
(round-trip identity). Serialise the same store twice and assert byte-identical output (determinism).

**Acceptance Scenarios**:

1. **Given** a `ReuseStore` of one or more recorded entries, **When** it is serialised, **Then** the output
   is a single well-formed `fsgg.evidence-reuse-store/v1` document and re-reading it through the existing
   read-only reader yields a store **equal** to the input (every entry's freshness inputs and opaque
   evidence reference preserved, in the same newest-first order).
2. **Given** the **empty** store (`EvidenceReuse.empty`), **When** it is serialised, **Then** the output is a
   well-formed `v1` document with an empty entry list that re-reads as the empty store (distinct from an
   absent file, but loading either yields the empty store).
3. **Given** the same store value, **When** it is serialised twice (or on two machines), **Then** the two
   outputs are **byte-identical** (deterministic, stable field/entry order, no wall-clock or environment
   leakage).
4. **Given** a store whose entries carry the opaque evidence reference and the full freshness-input set
   (rule hash, artifact hashes, command version, generator version, base/head revisions, environment class),
   **When** it round-trips, **Then** the opaque reference is preserved **verbatim** (never parsed,
   re-hashed, or interpreted) and no freshness input is dropped, reordered within an entry, or fabricated.

---

### User Story 2 - Keep the store bounded so it cannot grow without limit (Priority: P2)

Over many runs the store accumulates one entry per distinct freshness "world" per gate. Without a bound it
grows monotonically. A maintainer needs the store kept to a deterministic, bounded size so disk usage and
read cost stay flat, **without ever causing a stale reuse**.

**Why this priority**: Important for a store that is written every run, but a store that is merely written
(US1) is already useful for the immediate next run; bounding matters once writes accumulate.

**Independent Test**: Apply the retention policy to a store deliberately built past the bound and assert the
result is within the bound, retains the **newest** evidence (the entries a future run is most likely to
match), and that every retained entry is byte-for-byte one of the inputs (eviction only removes, never
mutates or fabricates). Assert that evicting an entry can only ever turn a future `reusable` into a
`mustRecompute` — never the reverse.

**Acceptance Scenarios**:

1. **Given** a store larger than the retention bound, **When** the retention policy is applied, **Then** the
   result is within the bound and contains the most-recent retained entries in newest-first order.
2. **Given** a store at or under the bound, **When** the policy is applied, **Then** the store is returned
   unchanged (idempotent; no spurious reordering or rewriting).
3. **Given** any store, **When** an entry is evicted, **Then** no surviving entry is altered and no entry is
   invented; the only effect on later evaluation is that a previously `reusable` world may become
   `mustRecompute noPriorEvidence` (recompute-by-default safety — eviction never manufactures a reuse).

---

### User Story 3 - Prune dead evidence the store can never reuse again (Priority: P3)

Some recorded entries are dead weight: a gate's evidence for a freshness world that has been superseded by a
newer recorded world for that same gate can never win a reuse decision again (F030 picks the most-recent
full match). A maintainer wants such superseded entries pruned so the store does not carry entries that can
never be served, while the safety guarantee holds: pruning never causes a reuse that would not otherwise
have happened.

**Why this priority**: A tidiness/efficiency refinement on top of a writable, bounded store; valuable but
not required for the cache to warm.

**Independent Test**: Build a store with a superseded entry (an older entry for a gate whose newer entry
would already win, or never), apply pruning, and assert only entries that could still be served by a future
reuse decision remain — and that the pruned store, evaluated against any candidate, yields verdicts no more
permissive than the unpruned store (pruning can only ever add recompute, never reuse).

**Acceptance Scenarios**:

1. **Given** a store containing an entry that no future candidate could ever reuse (a strictly superseded
   world for its gate), **When** pruning is applied, **Then** that entry is removed and the survivors are a
   subset of the input in newest-first order.
2. **Given** a store with no dead entries, **When** pruning is applied, **Then** the store is returned
   unchanged.
3. **Given** any store and any candidate, **When** the **pruned** store is evaluated, **Then** the reuse
   verdict is identical-or-stricter than evaluating the **unpruned** store — never more permissive
   (no-spurious-reuse invariant).

---

### Edge Cases

- **Absent vs empty file**: Serialising the empty store produces a real `v1` document with zero entries;
  this is distinct on disk from an absent file, but the existing reader maps **both** to the empty store, so
  the round-trip contract holds either way.
- **Duplicate-world entries**: If a store somehow holds two entries for the identical freshness world (full
  match), serialisation preserves them verbatim (it neither merges nor de-dups — de-dup is `record`'s job);
  pruning may remove the older such entry as superseded.
- **Unparseable round-trip target**: Serialisation must emit only what the existing reader accepts; any field
  the reader treats as required must be present, and the opaque evidence string must survive even if it
  contains characters needing JSON escaping.
- **Empty freshness inputs**: An entry whose covered-artifacts set is sensed-empty (a present-but-empty set)
  round-trips as empty — distinct from an absent/unsensed field — preserving the F029/F043 sensed-empty vs
  unsensed distinction.
- **Retention bound smaller than the number of distinct gates**: The policy still produces a within-bound
  store; the spec does not require per-gate fairness in the MVP (newest-first global retention is the
  default — see Assumptions), only that the result is bounded and recompute-safe.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a pure, total serialisation of a `ReuseStore` to the
  `fsgg.evidence-reuse-store/v1` format — the deterministic inverse of the existing read-only deserializer.
- **FR-002**: The round-trip `serialise` → existing `FreshnessSensing.realStoreReader` MUST be **lossless**:
  the re-read store equals the input store, with every entry's full freshness-input set and opaque evidence
  reference preserved and in the same newest-first order.
- **FR-003**: Serialisation MUST be **deterministic and byte-stable**: the same `ReuseStore` value produces
  byte-identical output on every run and every machine, with stable field and entry ordering and no
  wall-clock, path, locale, or environment leakage.
- **FR-004**: Serialisation MUST treat the evidence reference as **opaque** — rendered verbatim, never
  parsed, re-hashed, dereferenced, or interpreted — and MUST compute **no** hash, key, digest, or freshness
  decision of its own.
- **FR-005**: The empty store MUST serialise to a well-formed `v1` document carrying the schema version and
  an empty entry list (not an empty/whitespace file), re-readable as the empty store.
- **FR-006**: The system MUST provide a pure, total, deterministic **retention (eviction)** operation that
  bounds the store to a deterministic maximum, retaining the newest evidence and removing only whole entries
  (never mutating or fabricating an entry).
- **FR-007**: The system MUST provide a pure, total, deterministic **pruning (expiry)** operation that
  removes entries that no future reuse decision could serve (strictly superseded worlds for a gate),
  retaining a subset of the input in newest-first order.
- **FR-008**: Every write/retention/pruning operation MUST preserve **recompute-by-default safety**: relative
  to the unmodified store, the resulting store MUST yield reuse verdicts that are identical-or-stricter for
  every candidate — it MUST NEVER cause a candidate that would have been `mustRecompute` to become
  `reusable` (no spurious reuse).
- **FR-009**: All operations MUST be **value transformations** with no I/O — no file, clock, network, git, or
  filesystem access — so they are testable as pure functions; the actual on-disk write is out of scope (a
  later host row).
- **FR-010**: The operations MUST reuse the merged F030 `EvidenceReuse` model and `record`/`decide`
  semantics **verbatim** (full-match supersession, newest-first ordering, opaque `EvidenceRef`) and MUST NOT
  introduce a new reuse policy, a new freshness-match rule, or a new evidence representation.
- **FR-011**: The operations MUST add **no new third-party dependency**; serialisation MUST use only the same
  shared-framework facilities already used by the read-only reader.
- **FR-012**: The change MUST be **additive**: it MUST NOT edit the read-only reader's accepted shape, bump
  the `fsgg.evidence-reuse-store/v1` schema version, or alter any merged cache-thread core (F029–F046) or
  its golden baselines.

### Key Entities *(include if feature involves data)*

- **Reuse store (`ReuseStore`)**: The existing F030 value — a newest-first list of recorded evidence — now
  gaining a write/serialise and a retention/pruning lifecycle alongside its existing read path.
- **Recorded evidence (`RecordedEvidence`)**: The existing F030 entry — a full freshness-input set plus an
  opaque evidence reference. Carried verbatim through serialisation and retention; never re-derived. (It
  carries **no timestamp** today; wall-clock TTL expiry is therefore out of scope — see Assumptions.)
- **Evidence reference (`EvidenceRef`)**: The existing opaque F030 edge token. Rendered and retained
  verbatim, never dereferenced.
- **Serialised store document**: A versioned `fsgg.evidence-reuse-store/v1` text document — the byte-stable,
  losslessly-round-tripping output of serialisation and the exact shape the existing reader accepts.
- **Retention bound / dead-entry criterion**: The deterministic policy inputs that decide which entries an
  evicting/pruning operation keeps — defined so the result is always bounded and recompute-safe.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For every non-empty store, `serialise` → existing read-only reader returns a store **equal** to
  the input (lossless round-trip), verified over property-based generated stores.
- **SC-002**: Serialising any given store value twice yields **byte-identical** output (determinism),
  verified over property-based generated stores.
- **SC-003**: The empty store and an absent file both load to the empty store, and the empty store
  round-trips through serialise → read as the empty store.
- **SC-004**: After retention, the store size is within the deterministic bound and contains the newest
  retained entries; applying retention to an already-bounded store returns it unchanged (idempotent).
- **SC-005**: After pruning, survivors are a newest-first subset of the input, and no candidate's reuse
  verdict against the pruned store is more permissive than against the unpruned store.
- **SC-006**: No write/retention/pruning operation ever turns a `mustRecompute` candidate into a `reusable`
  one — demonstrated by a property test comparing pre- and post-operation `evaluate`/`decide` verdicts
  across generated candidates and stores.
- **SC-007**: The full solution adds **no** new third-party dependency, **no** schema-version bump, and
  **zero** edits to merged F029–F046 cores or their golden baselines (additive-only); the existing read-only
  reader is unchanged and now consumes serialiser output unmodified.
- **SC-008**: Every operation is a pure value transformation (no I/O), demonstrated by tests that exercise it
  with no filesystem/clock/network access.

## Assumptions

- **Pure-core-first slice (safe default, pattern-consistent)**: This row delivers the **pure** write/retention
  core only — serialiser + eviction + pruning as value transformations. The **impure** on-disk persistence
  (atomic temp+rename write wired into `fsgg route`/`fsgg ship`, store-path discovery, the writer port) is a
  **later host row**, mirroring how F042 delivered the pure `cache-eligibility.json` projection before the
  F044/F046 host wiring. This matches six consecutive prior cache slices.
- **Real evidence production is out of scope**: Producing a *genuine* `EvidenceRef` requires **gate
  execution** (running a gate's command and capturing its output digest), a capability not yet built. This
  row makes the store *writable*; populating it with real evidence depends on that later execution row. Tests
  use disclosed synthetic evidence references (the `Synthetic` discipline from F046).
- **Retention policy default**: Bounded retention keeps the **newest** entries globally (newest-first, as
  `record` already orders them), to a deterministic maximum. Per-gate fairness, configurable bounds, and the
  exact numeric cap are plan/clarify mechanism details, not requirement gaps; any chosen bound must satisfy
  FR-006/FR-008.
- **"Expiry" means superseded-world pruning, not wall-clock TTL**: `RecordedEvidence` carries no timestamp,
  so time-based TTL expiry would require extending the F030 model (and an F034 sensed-timestamp) — explicitly
  **deferred**. This row's expiry is the pure, time-free pruning of entries that can never be reused again.
- **Format target**: The serialiser targets exactly the `fsgg.evidence-reuse-store/v1` shape the existing
  `FreshnessSensing` deserializer accepts; the round-trip is verified against that real reader, not a
  re-implementation.
- **Library placement**: The natural home is the merged F030 `FS.GG.Governance.EvidenceReuse` core (it owns
  the store model, `record`, and `decide`); whether the serialiser lives there or in a thin sibling is a
  plan-time decision. No new public dependency beyond what F030 already references is expected.

## Out of Scope

- Impure persistence: writing the file to disk, atomic temp+rename, store-path discovery, an optional
  `--store` write flag, and wiring any of this into `fsgg route` / `fsgg ship` (a later host row).
- Producing real evidence references (gate execution / command running / output-digest capture).
- Wall-clock TTL/age-based expiry and any `RecordedEvidence` timestamp model change.
- Cache invalidation beyond superseded-world pruning; multi-writer concurrency/locking.
- Any schema-version bump or change to the read-only reader's accepted shape.
- Editing merged F029–F046 cores, their golden baselines, the F042/F044 standalone `cache-eligibility.json`
  sidecar, or anything in Phase 13.
