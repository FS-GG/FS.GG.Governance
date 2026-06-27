# Phase 0 Research: Promote `governedReferences` to First-Class Routing Facts

**Feature**: `082-route-governed-refs` | **Date**: 2026-06-27

No `NEEDS CLARIFICATION` markers remained after specification: the spec resolves every open
question in its **Assumptions** block. This document records the design decisions (D1–D9)
that ground the plan, each with rationale and the alternatives rejected.

---

## D1 — Where the declared paths become routing candidates: a new `Consumer.candidatePaths` function

**Decision**: Add ONE pure, total public function to the existing consumer adapter:

```fsharp
val candidatePaths: reads: Reader.HandoffRead list -> GovernedPath list
```

It parses each located document via the existing `Reader.parse`, keeps only the `Ok`
(consumable) ones, collects `handoff.GovernedReferences |> List.collect (_.Paths)`,
`List.distinct`-dedups, and returns a deterministically-ordered `GovernedPath list`. Empty
input ⇒ `[]`.

**Rationale**: It reuses the exact parse + "skip the bad document" rule that `consume`
already owns (FR-008 falls out for free — an `Error` parse contributes no candidates), keeps
the new surface to a single additive `.fsi` line, and is trivially unit-testable in
isolation. It is independent of routing, so the host can call it *before* `Routing.route`
without reordering F081's post-select `consume` fold.

**Alternatives considered**:
- **Add a `Candidates` field to `ConsumeResult`** (one parse, both gates + candidates).
  Rejected: it widens the record constructor signature (a larger, riskier baseline diff) and
  forces the host to move the `consume` call *above* `Routing.route`, perturbing the
  carefully byte-identical F081 fold ordering. The double-parse the separate function incurs
  is negligible (small JSON, already in memory, run once per command) and buys a strictly
  additive, lower-regression-risk change.
- **Inline the parse+collect in each host.** Rejected: triplicates parse logic across three
  hosts and is not cleanly `.fsi`-curated (Constitution II).
- **A new `GovernedRefs` module.** Rejected as over-factoring; the function belongs next to
  `consume`, which already owns "parse every doc, skip bad ones."

## D2 — The candidate-merge seam: before `Routing.route`, dedup the union

**Decision**: In each host's `Loaded(Valid facts)` arm, change only the candidate assembly:

```fsharp
let sensed   = model.Candidates |> Option.defaultValue []
let declared = Consumer.candidatePaths model.Handoffs          // F082
let candidates = sensed @ declared |> List.distinct            // FR-006 dedup
let report = Routing.route facts candidates
// …everything below (registry, findings, Route.select, F081 consume-union fold) UNCHANGED
```

**Rationale**: Merging *before* `Routing.route` (per the spec's "candidate-assembly seam"
assumption) is the single place where FR-002 ("same machinery"), FR-006 (dedup → routed
once, recorded once, counted once), and FR-007 (a declared in-root-but-unmatched path
surfaces the same unknown-governed-path finding) all hold automatically — the declared paths
flow through `Routing.route → Findings.findUnknownGovernedPaths → Route.select` exactly as
sensed paths do. `Route.select` already dedups by `GateId` and orders selecting paths by
normalized path (FR-010, SC-006); no selection-algorithm change is needed.

**Why dedup at this seam (not in `Route.select`)**: A duplicate candidate path would produce
duplicate `RoutingResult`s and thus a doubled selecting-path. De-duplicating the
`sensed ∪ declared` candidate list *before* routing is what guarantees SC-003 (one
selecting-path entry, counted once). The sensed list is already distinct; the union must be
re-distinct because a declared path may equal a sensed path.

## D3 — Normalization: already done at read time (no new normalization)

**Decision**: Do **not** re-normalize declared paths in `candidatePaths` or the hosts.

**Rationale**: `Reader.parse` already maps declared `governedReferences[].paths` through
`normalizePath` (Reader.fs line 229), exactly as the host normalizes sensed `ExplicitPaths`
and as snapshot sensing normalizes diff paths. Declared and sensed `GovernedPath` values are
therefore value-equality compatible, so `List.distinct` deduplicates correctly (SC-003) and
`Routing.route` (which assumes already-normalized candidates) is satisfied (FR-002). Adding a
second normalization would be redundant and risk double-normalization drift.

## D4 — Bad / version-mismatched documents contribute zero candidates (FR-008)

**Decision**: `candidatePaths` keeps only `Reader.parse … = Ok handoff`; an `Error` (malformed,
missing-required, unknown major, declared-`autoSynthetic`) is dropped from the candidate
source. The document's blocking integrity gate is **unaffected** — it is still produced by
the unchanged `consume` path that runs later in the same `Loaded(Valid)` arm.

**Rationale**: Mirrors `consume`'s existing rule (a bad document yields a blocking integrity
gate and NO mapped evidence/readiness gate, FR-011 of F081). A bad document must not be
allowed to *widen* enforcement by injecting routing candidates (SC-005). Because
`candidatePaths` and `consume` both call `Reader.parse`, the two stay consistent by
construction.

## D5 — The handoff's own self-glob provenance is unchanged; only domain-gate selection uses real globs (SC-006)

**Decision**: Leave F081's `Consumer.selectingPaths` (the self-glob `{ Path = p; MatchedGlob = p }`
provenance on the handoff's OWN `sdd-handoff:*` evidence/readiness/integrity gates) exactly as
is (FR-009). The new domain-gate selection produced from declared candidates carries the
**real** path-map glob, because it comes from `Route.select` recording `{ Path; MatchedGlob = glob }`.

**Rationale**: SC-006 forbids a synthetic self-glob *leaking into domain-gate selection* — it
does not forbid the self-glob on the handoff's own gates, which is the intended F081 provenance
(relevance = the declared work item, not a path match). The two are different gates:
`sdd-handoff:evidence:<id>` keeps the self-glob; `build:build` (selected because a declared
path routed to the `build` domain) gets the real glob. No leak; FR-003 + FR-009 both hold.

## D6 — Three identical host edits; no host `.fsi` change

**Decision**: Apply the D2 seam edit to all three hosts (`RouteCommand`, `ShipCommand`,
`VerifyCommand`) identically. No `Loop.fsi` changes (the edit is internal to `update`'s
`Loaded(Valid)` arm — `init`/`parse`/`update`/`render`/`exitCode` signatures are unchanged).

**Rationale**: The spec requires identical contribution across the three hosts (Edge case:
"no host-specific divergence"). The seam is structurally identical in all three
(`let candidates = model.Candidates |> Option.defaultValue []` immediately precedes
`Routing.route facts candidates` in each). Verify's empty-selection short-circuit and Ship's
`rollup` both run *after* the merge, so they see the enriched selection with no further change.

## D7 — Surface baseline: additive re-bless of one adapter file only

**Decision**: Re-bless `surface/FS.GG.Governance.Adapters.SddHandoff.surface.txt` with
`BLESS_SURFACE=1` to add the single `candidatePaths` method line under `ConsumerModule`. No
in-test surface literal exists for this adapter (its `SurfaceDriftTests.fs` reads the committed
`.txt` and supports `BLESS_SURFACE=1`), so no test literal edit is required. No host surface
baseline changes (no host `.fsi` change — D6).

**Rationale**: Tier 1 additive surface change (Constitution Change Classification). The diff is
exactly one new `[Method]` line — strictly additive, no removed or altered surface.

## D8 — Host scope-guard hygiene: verify member-granularity, not assumed

**Decision**: Before/while editing, confirm the three host surface-drift / scope-guard tests
(relaxed in F081 to permit the `Adapters.SddHandoff` edge) gate at **assembly-reference**
granularity, not exact-member granularity. If any guard enumerates the specific adapter
members the host may call, extend it additively to permit `candidatePaths` alongside
`consume`/`Reader`/`Model`.

**Rationale**: F081 "relaxed the three host surface-drift hygiene guards to PERMIT the one
`Adapters.SddHandoff` edge." The edge already exists; this feature adds a second call to the
same already-permitted adapter. Most likely the guard is assembly-level (no change needed),
but this is the one place a guard *could* fail, so it is an explicit verification step rather
than an assumption.

## D9 — Findings & cost follow the "same machinery" intent (no suppression)

**Decision**: Do nothing special for findings or cost. A declared in-root path that matches
no domain produces an `UnmatchedInRoot` routing ⇒ the same unknown-governed-path finding a
sensed path would (FR-007), and declared paths contribute to the `CostRollup` like any other
selected gate's paths.

**Rationale**: Matches the spec Assumption ("Declared paths are treated exactly like changed
paths for routing, findings, and cost") and FR-002's "no special-case routing logic." The
alternative — select-only declared paths while suppressing their findings/cost — was
considered and rejected by the spec as a machinery-violating special case. `/speckit-clarify`
may revisit if the unknown-path finding proves too noisy, but that is out of scope here.

---

## Resolved unknowns summary

| Question | Resolution |
|----------|------------|
| Where do declared paths enter routing? | New `Consumer.candidatePaths`, merged before `Routing.route` (D1, D2). |
| Re-normalize declared paths? | No — `Reader.parse` already normalizes them (D3). |
| Bad document behavior? | Zero candidates; integrity gate still fires via unchanged `consume` (D4). |
| Self-glob vs real glob? | Self-glob stays on handoff's own gates; domain gates get real globs from `Route.select` (D5). |
| How many hosts / `.fsi` impact? | Three identical internal edits; no host `.fsi` change (D6). |
| Surface baseline impact? | One additive line in the adapter baseline; re-bless with `BLESS_SURFACE=1` (D7). |
| Scope-guard risk? | Verify assembly-vs-member granularity; extend additively only if needed (D8). |
| Findings/cost for declared paths? | Same machinery — no suppression (D9). |
