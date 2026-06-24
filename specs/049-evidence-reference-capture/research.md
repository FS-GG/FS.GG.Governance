# Phase 0 Research: Capture A Real Evidence Reference From An Executed Gate

All unknowns are resolved by the spec's Assumptions section and the existing F030/F032/F047 surfaces. No
`NEEDS CLARIFICATION` remained after loading the spec. The decisions below record *why* each shape was chosen.

## D1 — The reference IS the F032 reproducible identity, wrapped (not a new hash)

**Decision**: `referenceOf record = EvidenceRef (CommandRecord.identityValue (CommandRecord.canonicalId
record))`. The `EvidenceRef` string is byte-for-byte the F032 canonical identity string.

**Rationale**: F032 already delivered a canonical, deterministic, byte-stable, **injective-over-the-reproducible-
facts** rendering of a `CommandRecord`, computed only over `record.Reproducible` and excluding `record.Duration`
by construction (F032 D2). That is *exactly* the contract this row needs for the reference (FR-002 duration-
invariance, FR-003 injectivity, FR-008 byte-stability). Reusing it satisfies every reference requirement for
free and keeps the row hashing **no** bytes (F032 D3/D10 — the identity is a length-prefixed, uniquely-tagged
BCL string build, not a hash). Inventing a second identity scheme would duplicate F032's encoding, risk
divergence, and add surface for no benefit.

**Alternatives considered**:
- *Hash the canonical identity (e.g. SHA-256) into a shorter ref.* Rejected: adds a hashing dependency and a
  collision surface for zero benefit — the store holds the ref opaquely and never dereferences it (F030
  `EvidenceRef` is opaque), so length is irrelevant, and F032 D10 deliberately avoided hashing.
- *Mint a fresh GUID / content-address per capture.* Rejected: a GUID is non-reproducible — a perfect re-run
  would record a *different* reference and never be served (defeats US1/US2). The reference must be a pure
  function of the reproducible facts.
- *Use the sensed duration or wall-clock in the ref.* Rejected outright — that is precisely the failure US2 /
  FR-002 forbid. `canonicalId` structurally cannot read `record.Duration`, which is the strongest possible
  guarantee.

## D2 — `capture` reuses F030 `record` verbatim (no new fold)

**Decision**: `capture inputs record store = EvidenceReuse.record inputs (referenceOf record) store`.

**Rationale**: F030 `record` already implements the exact fold this row needs — newest-first, store in / store
out, immutable, total, no I/O (F030 FR-007/FR-008/FR-009). Reusing it verbatim satisfies FR-004 (no new reuse
policy / store representation / evidence representation), FR-005 (the close-the-loop round-trip, because
`decide` is F030's own inverse of `record`), and FR-006 (recompute-safety is inherited — F030 `record` only
ever makes the just-recorded world reusable and de-dups only an exact full-match of that same world). Note F030
`record` is *de-duplicating* (it drops any existing entry that `matches` the new world), which is stronger than
"newest-first append" but is exactly the merged convention the spec's US3 scenario 2 ("serves the
most-recently-captured reference") and Edge case "Duplicate capture" describe — this row adopts that behaviour
verbatim and introduces no dedup policy of its own.

**Alternatives considered**:
- *A bespoke append/cons into `ReuseStore`.* Rejected: would reach past F030's API into the store
  representation, duplicating (and risking divergence from) the merged `record` convention, and would re-open
  the dedup-policy question this row explicitly defers to F030.

## D3 — A new standalone library, not a function added to F030 or F032

**Decision**: New project `FS.GG.Governance.EvidenceCapture`, layered on F030 + F032.

**Rationale**: FR-009 / SC-006 require zero edits to any existing core and its golden baseline / surface
baseline. Adding `referenceOf` to `CommandRecord` or `capture` to `EvidenceReuse` would mutate a merged public
surface and force a surface-baseline re-bless on a frozen core. A separate project is the established pattern for
layering (F042 `CacheEligibilityJson` on F041; F047 `EvidenceReuseStore` on F030) and keeps the bridge
referenced-by-nothing-on-landing, exactly as F047 was.

**Alternatives considered**:
- *Add the two functions to `EvidenceReuse` (F030).* Rejected: edits a frozen merged core + its baseline
  (FR-009 violation) and would force F030 to take a `ProjectReference` on F032, coupling two otherwise
  independent cores.
- *Add `referenceOf` to `CommandRecord` (F032).* Rejected: same baseline-edit problem, and it would couple F032
  to F030's `EvidenceRef` type — F032 has no business knowing about the reuse store.

## D4 — Model-less: no new type, so no `Model.fsi/fs`

**Decision**: The library carries only `EvidenceCapture.fsi` + `EvidenceCapture.fs` — no `Model` file.

**Rationale**: The row introduces **no new type** (FR-009 "no new evidence representation"). It reuses
`EvidenceRef` and `ReuseStore` (F030 `Model`), `CommandRecord` (F032 `Model`), and `FreshnessInputs` (F029
`Model`) verbatim. Both functions return existing types. F030 needed a `Model` file because it introduced
`EvidenceRef`/`RecordedEvidence`/`ReuseStore`/`ReuseDecision`; this row introduces nothing, so a `Model` file
would be empty. Compile order is simply `EvidenceCapture.fsi -> EvidenceCapture.fs`.

## D5 — Function names and signatures match the spec's vocabulary verbatim

**Decision**: `referenceOf : CommandRecord -> EvidenceRef` and
`capture : FreshnessInputs -> CommandRecord -> ReuseStore -> ReuseStore`.

**Rationale**: The spec's Independent Tests and Acceptance Scenarios call `capture inputs record store` and
`referenceOf record` by those names and argument orders (`capture` takes inputs first, then the record, then the
store — matching F030 `record`'s `inputs -> evidence -> store` shape with `record` substituted for the derived
`evidence`, so a reader sees the parallel immediately). Keeping the names and currying identical to the spec and
to F030's precedent minimises surprise and lets the round-trip read as `decide inputs (capture inputs record
empty) = Reuse (referenceOf record)`.

## D6 — Persistence round-trip is verified by reuse, not by new code

**Decision**: FR-010 / SC-007 are satisfied by a **test** that grows a store with `capture`, runs it through the
already-merged F047 `EvidenceReuseStore.serialise`, and re-reads it through the already-merged F046
`FreshnessSensing.realStoreReader` — asserting the captured world and exact reference survive. No persistence
code is added in this row.

**Rationale**: The reference is rendered into the document verbatim (F047 `serialise` renders `EvidenceRef`
strings without re-parsing or re-hashing — F047 FR-004), and the F046 reader carries it back verbatim. Because
the reference is an ordinary string and the freshness world is the same F029 vocabulary F047/F046 already
round-trip losslessly (F047 FR-002), a `capture`-grown store is indistinguishable to persistence from a
hand-built one. The round-trip needs only test-time `ProjectReference`s on EvidenceReuseStore + FreshnessSensing
(no production dependency), keeping the production library's dependency set at exactly F030 + F032.

## D7 — No reuse policy, no exit-code gating (mechanical capture)

**Decision**: `capture` records whatever `CommandRecord` it is handed, including a non-zero `ExitCode`.

**Rationale**: Spec Assumption "Capture is mechanical, not policy" and the "Failed run" edge case place all
gating (should a failed gate's evidence be captured? success/exit-code suppression?) in the **host row** that is
out of scope. F032 already records failures as ordinary reproducible facts (F032 FR-003), and `referenceOf` is
total over every exit code. Adding any gate here would invent the very reuse policy FR-004 forbids.

## Resolved unknowns

| Unknown | Resolution |
|---------|------------|
| What is the reference derived from? | `identityValue (canonicalId record)`, wrapped as `EvidenceRef` (D1) |
| How is duration kept out? | Structurally — `canonicalId` reads only `record.Reproducible`, never `record.Duration` (D1) |
| How does the store grow? | `EvidenceReuse.record` reused verbatim (D2) |
| New project or edit an existing core? | New standalone library `FS.GG.Governance.EvidenceCapture` (D3) |
| New types needed? | None — Model-less, reuses F030/F032/F029 vocabulary (D4) |
| New third-party dependency? | None — BCL + FSharp.Core, on F030 + F032 ProjectReferences (D3) |
| How is the persistence round-trip proven? | Test reusing merged F047 `serialise` + F046 reader (D6) |
| Any reuse / exit-code policy? | None — mechanical capture; gating is the out-of-scope host row (D7) |
