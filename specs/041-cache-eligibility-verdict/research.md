# Phase 0 Research — Per-Gate Cache-Eligibility Verdict Core (F041)

All Technical Context items are resolved; there are **no open NEEDS CLARIFICATION**. The spec defers a small set
of shapes to planning (Assumptions, FR-004/FR-006/FR-012, Edge Cases); each is decided below. Format per decision:
**Decision / Rationale / Alternatives considered**.

---

## D1 — One new minimal pure-core module (the established rhythm)

**Decision**: Deliver a single new packable library `FS.GG.Governance.CacheEligibility`, compiled `Model.fsi/fs →
CacheEligibility.fsi/fs`, rather than extending a merged core. The operations module is `CacheEligibility`; the
per-change roll-up is named **`evaluate`** and the per-gate composition **`evaluateGate`**.

**Rationale**: F015–F040 each landed one pure, total, deterministic core per implementation-plan row before any host
edge or projection consumed it; this row is the per-change **roll-up** that the route/audit JSON needs and is the
analogue of F030 `decide` (single candidate) lifted to *all* of a change's selected gates. A new minimal core keeps
the addition isolated and additive (SC-008) and keeps the merged cores' baselines untouched. `evaluate` /
`evaluateGate` read as "evaluate cache eligibility for these candidate gates / this gate", and `evaluateGate` is a
thin relabel over the reused F030 `decide` (D4).

**Alternatives considered**: *Extend `EvidenceReuse` (F030).* Rejected — F030 owns the **single-candidate** reuse
decision against a store and is deliberately scoped to it; this row is the **per-change roll-up** that composes F030
once per selected gate and attributes/orders the results. Folding the roll-up into F030 would rewrite a merged
baseline and conflate "decide for one candidate" with "report across a change's gates". *Extend `RouteJson` (F020) /
`AuditJson` (F025).* Rejected — rendering the verdict into the JSON documents is explicitly the **next** row (Out of
Scope); per the repo's pure-core-first rhythm the decision value lands first, the projection consumes it later.
*Extend `FreshnessKey` (F029).* Rejected — F029 owns the fingerprint/diff vocabulary this core *consumes*; it
deliberately computes a key, which this core must not (FR-008).

---

## D2 — The candidate pairing: a supplied `GateId` + already-resolved `FreshnessInputs`

**Decision**: `CandidateGate = { Gate: GateId; Inputs: FreshnessInputs }` — one selected gate's stable identity
paired with its already-resolved freshness inputs. Both fields are **supplied facts**: the core never resolves,
fabricates, or re-hashes the inputs, and never derives, parses, or cross-checks the `GateId` against `Inputs.Check`
/ `Inputs.Domain`. `evaluate : candidates: CandidateGate list -> store: ReuseStore -> CacheEligibilityReport`
(candidate-list first, store last — the pluralised F030 `decide candidate store` order).

**Rationale**: The spec's Key Entities fix the candidate as "one selected gate's stable **gate identity** paired with
the **freshness inputs** already resolved for it"; FR-005 requires every verdict attributed "to its originating gate
identity, so the verdict can be placed under the correct gate in a later projection," and route.json (F020) /
audit.json (F025) key each gate by its `GateId`. So the attribution key is the **`GateId`** wire identity, reused
verbatim from `FS.GG.Governance.Gates.Model` (FR-012's "reuse the existing … gate-identity vocabulary verbatim").
The `FreshnessInputs` is reused verbatim from `FS.GG.Governance.FreshnessKey.Model`. Treating the `GateId` as the
supplied attribution key — **not** something derived from `Inputs.Check`/`Inputs.Domain` — keeps the core from
resolving or re-deriving identity (FR-009: "treats the supplied freshness inputs … as opaque facts produced
elsewhere") and matches that an edge already knows the routed gate's `GateId` and its resolved inputs.

**Alternatives considered**: *Carry no `GateId`; derive the gate identity from `Inputs.Check` + `Inputs.Domain`.*
Rejected — the route/audit JSON places verdicts under the `GateId` string (`"<domain>:<checkId>"`), so a
`(CheckId, DomainId)` pair would not match the projection's attribution and the core would have to *derive* the
wire id (re-deriving identity the host already owns). *Mint a local `GateId` newtype.* Rejected — duplicates the
existing `Gates.Model.GateId` and violates FR-012's reuse-verbatim rule. *Bundle `candidates` + `store` into one
input record.* Rejected as needless nesting: the store is the shared backdrop and the candidates the per-change
subject; two parameters read more honestly (the F030 `decide candidate store` shape, pluralised).

---

## D3 — Reference `EvidenceReuse` (F030) + `Gates` (F018); reuse all consumed vocabulary verbatim

**Decision**: Reference **`FS.GG.Governance.EvidenceReuse`** (F030 — for `EvidenceReuse.decide`, `ReuseStore`,
`ReuseDecision`, `RecomputeCause`, `EvidenceRef`) and **`FS.GG.Governance.Gates`** (F018 — for `GateId` /
`gateIdValue`). `FreshnessInputs` / `InputCategory` (F029) and `CheckId` / `DomainId` (F014, in `Config`) arrive
**transitively** through F030 / F018 and are *named* (opened) but not referenced directly — the F030 precedent
(F030 names `FreshnessInputs` but references only `FreshnessKey`, with `Config` transitive). All consumed vocabulary
is reused verbatim; the only **new** vocabulary is the minimal set FR-012 names (the candidate pairing, the
two-outcome verdict, the per-gate entry, and the report).

**Rationale**: FR-004 fixes that the per-gate decision MUST be derived by **composing the existing evidence-reuse
decision** (F030 `decide`) — not a re-implementation — so F030 is a hard dependency. FR-005/FR-006/FR-012 require
the `GateId` gate-identity vocabulary, owned by F018 `Gates.Model` (a pure core referencing only `Config`). FR-012
lists the reused vocabulary (freshness-input, evidence-reference, evidence-store, changed-input category,
gate-identity) — every one is opened verbatim from its owning module, none redefined. Both reused cores are pure,
total, deterministic, and reference no clock/filesystem/git/network; transitive project references flow to the
compiler by default (no `DisableTransitiveProjectReferences`), so `FreshnessInputs` / `InputCategory` / `CheckId` /
`DomainId` are nameable without a direct reference, exactly as F030 names `FreshnessInputs` through its single
`FreshnessKey` reference.

**Alternatives considered**: *Reference only `EvidenceReuse`, and avoid `Gates` by using `(CheckId, DomainId)` for
identity (D2's rejected pairing).* Rejected — loses the `GateId` attribution the projection needs and forces
identity re-derivation. *Reference `FreshnessKey` directly too (three references).* Rejected as redundant — F029's
types flow transitively through F030 and are nameable; adding a direct reference duplicates the closure for no gain
(F030's own pattern relies on transitivity for `Config`). *Reference any of `RouteJson` / `AuditJson` /
`Enforcement` / `Ship` / `Snapshot`.* Rejected — projection (F020/F025) is the next row, enforcement/ship
(F023/F024) are downstream, and `Snapshot` is the git-sensing assembly the pure core must never touch (FR-008/FR-011).

---

## D4 — The verdict is a new two-outcome union; its payloads (`EvidenceRef`, `RecomputeCause`) are reused verbatim

**Decision**: `CacheEligibilityVerdict = Reusable of EvidenceRef | MustRecompute of RecomputeCause`. `evaluateGate`
composes F030 once — `EvidenceReuse.decide candidate.Inputs store` — and relabels its `ReuseDecision` **verbatim**:
`Reuse ref → Reusable ref`, `Recompute cause → MustRecompute cause`. The relabel is a total, information-preserving
1-to-1 map; it introduces **no new or divergent reuse policy** (FR-004), re-implements no matching, and re-ranks no
entries. `MustRecompute` carries the F030 `RecomputeCause` verbatim (`NoPriorEvidence | InputsChanged of
InputCategory list`), so the named cause — *no prior evidence* or the exact changed freshness-input categories — is
exactly F030's, with no truncation to "first difference".

**Rationale**: FR-012 explicitly lists "the **two-outcome verdict**" among the *new* minimal vocabulary this row
introduces, and the spec's outcome words are *reusable* / *must-recompute* (distinct from F030's `Reuse` /
`Recompute`). FR-002 requires a *reusable* verdict to carry the **evidence reference** and a *must-recompute* verdict
to carry a **named cause** (no opaque yes/no) — both satisfied by reusing `EvidenceRef` and `RecomputeCause`
verbatim as the case payloads. FR-010's *necessary-not-sufficient* property holds **by construction**: the union
carries only an evidence reference or a recompute cause — no skip action, severity, ship verdict, or exit-code basis
is representable. Minting the union shell (not the payloads) is the minimal faithful reading.

**Alternatives considered**: *Reuse F030 `ReuseDecision` verbatim as the per-gate verdict (mint no verdict type).*
The leanest option — but rejected because FR-012 names "the two-outcome verdict" as expected *new* vocabulary, the
spec's outcome words differ (*reusable*/*must-recompute*), and the *reusable*-is-necessary-not-sufficient framing
(FR-010) reads more honestly on a row-owned verdict. The cost is one tiny total relabel function and a union shell;
the reuse *policy* still lives entirely in F030 (FR-004 preserved). *Mint a new cause type instead of reusing
`RecomputeCause`.* Rejected — duplicates F029/F030's changed-input-category vocabulary, violating FR-012; the cause
*is* `RecomputeCause`. *Carry a bare `bool` + side accessor for the reference.* Rejected — FR-002 forbids an opaque
yes/no and the unrepresentable-bad-state discipline (a *reusable* without a reference) is lost.

---

## D5 — The report: one attributed entry per candidate, ordered by `GateId` ordinal, duplicates kept, no key computed

**Decision**: `CacheEligibilityEntry = { Gate: GateId; Verdict: CacheEligibilityVerdict }` and
`CacheEligibilityReport = CacheEligibilityReport of CacheEligibilityEntry list` (single-case wrapper, the `ReuseStore`
precedent), with an `entries` accessor. `evaluate candidates store` produces **exactly one** entry per supplied
candidate (none dropped, merged, or duplicated), then orders the entries by **`String.CompareOrdinal` on
`gateIdValue`** (the `Gates` "sorted by `GateId` ordinal" convention), breaking ties by the candidate's
**structurally-comparable `FreshnessInputs`** (then the verdict) so the order is **total and independent of input
order** even when two candidates share a `GateId`. Ordering uses **comparison only** — it computes **no freshness
key / hash** (FR-008).

**Rationale**: FR-006 requires exactly one verdict per candidate, every candidate preserved, in deterministic
`GateId` order independent of supply order; SC-006 requires byte-identical reports across input orders. Ordinal
string sort on `gateIdValue` matches the established `Gates` / `FreshnessKey` ordinal discipline and is stable across
cultures/machines. Duplicate `GateId`s (spec Assumptions/Edge Cases: "evaluated independently … neither merges nor
drops duplicates") need a deterministic tiebreak to be total *regardless of supply order*; `FreshnessInputs` is
structurally comparable (all single-case string newtypes, options, lists, and the `EnvironmentClass` union), so a
structural compare gives a total, input-order-independent tiebreak **without** computing a freshness key — honoring
FR-008's "computes no hash or freshness key itself." (The set-semantics of `CoveredArtifacts` matter only to F030's
reuse *decision*, which is unchanged; using order-sensitive structural compare purely as an ordering tiebreak is
sound and total.)

**Alternatives considered**: *Stable sort on `GateId` only (keep supply order among duplicates).* Rejected — the
relative order of duplicate-`GateId` entries would then depend on supply order, violating SC-006's input-order
independence. *Tiebreak on the F029 freshness `Key` string.* Rejected — computing `FreshnessKey.compute` is exactly
the "compute no freshness key" FR-008 forbids; structural comparison achieves the same determinism without it.
*Forbid duplicate `GateId`s as a precondition (the spec's permitted escape hatch).* Rejected for this row — the spec
defaults to "evaluated independently, neither merged nor dropped," and supporting them with a total tiebreak is
strictly safer than a precondition that could surface as a silent collapse; planning surfaced no strong reason to
forbid them. *A bare `CacheEligibilityEntry list` (no wrapper).* Rejected — the single-case wrapper matches the
repo's `ReuseStore` newtype discipline and gives the report a named type for projection and tests.

---

## D6 — The operations surface: `evaluate` / `evaluateGate` + small total projections

**Decision**: The public surface is `evaluate : CandidateGate list -> ReuseStore -> CacheEligibilityReport`,
`evaluateGate : CandidateGate -> ReuseStore -> CacheEligibilityVerdict`, and the small total projections/unwrappers
`entries : CacheEligibilityReport -> CacheEligibilityEntry list`, `isReusable : CacheEligibilityVerdict -> bool`,
`reusableEvidence : CacheEligibilityVerdict -> EvidenceRef option`, and `recomputeCause : CacheEligibilityVerdict ->
RecomputeCause option`. `evaluate` is defined as `candidates |> List.map (fun c -> { Gate = c.Gate; Verdict =
evaluateGate c store }) |> sort |> CacheEligibilityReport`.

**Rationale**: Exposing the per-gate `evaluateGate` makes the US1/US2 per-gate laws directly testable and makes the
roll-up's definition obvious (map-then-sort); it mirrors how F040 exposed both the decision and its projections.
`isReusable` / `reusableEvidence` / `recomputeCause` are the audit/test projections (the F040 `isCalibrated` /
`calibrationReason` / `calibrationMetrics` precedent) and let a downstream host inspect a verdict without
re-matching the union. `entries` unwraps the report for the projection row and tests. No `empty` is needed —
`evaluate [] store` is the empty report (a total, valid result, Edge Cases).

**Alternatives considered**: *Expose only `evaluate` (hide the per-gate path).* Rejected — the spec's US1/US2 are
per-gate properties; a public `evaluateGate` makes them first-class and keeps the report definition transparent.
*Add an `empty` report constant.* Rejected as redundant — `evaluate []` already yields it; an extra constant widens
the surface for no behaviour. *Expose accessors that throw on the wrong case (e.g. `theEvidence : verdict ->
EvidenceRef`).* Rejected — partial accessors break totality; the `option`-returning projections are total.

---

## D7 — Necessary-not-sufficient and the scope guard (FR-010/FR-011)

**Decision**: `CacheEligibilityVerdict` and `CacheEligibilityReport` carry **no** skip action, enforcement severity,
ship verdict, exit-code basis, or projection/JSON shape — a *reusable* verdict asserts only "prior evidence may be
reused for this gate". The core renders **no JSON**, performs **no cache lookup against a real store**, persists
nothing, maps **no exit code**, and adds **no CLI**. The `CacheEligibility` assembly references **only**
`EvidenceReuse` (F030) and `Gates` (F018) and their transitive pure cores (`FreshnessKey`, `Config`); it references
no `RouteJson` / `AuditJson` / `Enforcement` / `Ship` / `Snapshot` / host / CLI / adapter assembly, and adds no
third-party `PackageReference`. A reflective `SurfaceDrift` test pins the public surface and this referenced-assembly
set.

**Rationale**: FR-010 makes "necessary-not-sufficient" structural — there is nothing in the verdict value that could
be mistaken for an authorization to skip a gate. FR-011 / FR-014 keep the row a pure decision value with no
persistence, projection, exit code, or CLI, and no merged core / baseline / projection modified. The scope guard
mirrors F040's `SurfaceDrift` referenced-assembly assertion, catching any accidental dependency drift (e.g. a stray
`Snapshot` reference that would smuggle in git I/O).

**Alternatives considered**: *Let the verdict carry a `skip: bool` convenience.* Rejected — it re-admits exactly the
authorization FR-010 forbids; the host composes the skip downstream. *Skip the referenced-assembly guard.* Rejected
— the F029–F040 precedent guards scope reflectively because a transitive impure reference is the easiest invariant to
break silently.

---

## D8 — Totality and determinism are the contract; performance is not

**Decision**: `evaluate` / `evaluateGate` are **total** (a well-formed report/verdict for every input — including no
candidates, one candidate, duplicate `GateId`s, an empty store, and a store with no matching entry) and **pure /
deterministic** (identical candidates + store ⇒ byte-identical report; reads no clock, filesystem, git, environment,
or network; invokes no gate; computes no hash or freshness key; resolves none of the supplied inputs). Latency is not
a success criterion.

**Rationale**: FR-007 / FR-008 / SC-004 / SC-005 fix totality, purity, and determinism as the contract (the spec
Assumptions: "Determinism is the contract, not performance"). The computation is `List.map` of a verbatim F030
`decide` plus a comparison sort — no I/O, no exception path. FsCheck properties cover totality, determinism,
recompute-by-default, one-per-gate, and order-independence; the worked examples are pinned to
`contracts/cache-eligibility-api.md`, plus the FSI proof.

**Alternatives considered**: *Set a latency goal.* Rejected — the spec explicitly declines performance as a success
criterion; the decision is a small per-gate computation. *Allow a partial result on a "malformed" candidate.*
Rejected — every `CandidateGate` is well-typed by construction; there is no malformed input to drop, and dropping a
candidate would violate FR-006/FR-007.
