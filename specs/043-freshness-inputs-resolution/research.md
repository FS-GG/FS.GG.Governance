# Phase 0 Research: Per-Gate Freshness-Inputs Resolution Core

**Feature**: `043-freshness-inputs-resolution` | **Date**: 2026-06-22

The spec has no open `NEEDS CLARIFICATION`; the Assumptions section already pins the design intent. This file
records the decisions that turn that intent into a concrete pure core, each grounded in an already-merged
sibling surface read during planning.

---

## D1 — One new pure-core sibling library (`FS.GG.Governance.FreshnessResolution`)

**Decision**: Add one new packable `src/` library, `FS.GG.Governance.FreshnessResolution`, compiling
`Model.fsi/fs` (the new vocabulary) then `FreshnessResolution.fsi/fs` (the `resolve` entry point + accessors),
plus its sibling test project. No existing `src/`, `surface/`, or merged test project changes.

**Rationale**: This continues the maintainer-confirmed **pure-core-first** rhythm — every emission row landed a
pure, total, deterministic core in its own assembly before any host edge or projection consumed it (F029
`FreshnessKey`, F030 `EvidenceReuse`, F041 `CacheEligibility`, F042 `CacheEligibilityJson`). This row is the
**join** that those cores' host edge is blocked on; it belongs in its own pure assembly so the later host row
(git sensing) and the later projection rows layer on top of it, not into it (constitution: heavier capabilities
layer on top, not into the core).

**Alternatives considered**: (a) Fold `resolve` into F041 `CacheEligibility` — rejected: it would couple the
verdict core to the F018 `Gate`/`FreshnessKey` input shape it deliberately does not consume (F041 takes an
already-assembled `CandidateGate`), and would grow a merged surface this row is supposed to leave untouched
(FR-014). (b) Put it in the later host assembly — rejected: the host senses git/filesystem; this join is pure
and must be testable without any I/O (FR-009), exactly the property a separate pure core guarantees.

---

## D2 — Single direct project reference: `FS.GG.Governance.CacheEligibility` (F041)

**Decision**: The library takes **one** `ProjectReference` — `FS.GG.Governance.CacheEligibility` (F041). The
F029 `FreshnessInputs` + its newtypes (`RuleHash`, `ArtifactHash`, `CommandVersion`, `GeneratorVersion`,
`Revision`), the F018 `Gate`/`GateId`/`FreshnessKey` + `Gates.gateIdValue`, the F030 `EvidenceReuse`, and the
F014 `Config` newtypes (`CheckId`, `DomainId`, `CommandId`, `EnvironmentClass`) all arrive **transitively**
through F041 and need no direct reference.

**Rationale**: This mirrors F042 exactly ("references ONLY FS.GG.Governance.CacheEligibility; the rest arrive
transitively"). F041 already references `Gates` (F018), `FreshnessKey` (F029), and `EvidenceReuse` (F030);
transitive project references flow to the compiler, so a single edge gives this row the full vocabulary it
joins. **No new third-party `PackageReference`** (FR-013): the join is pure F# over typed values — no
serialization, no `System.Text.Json`, only `FSharp.Core` + `System.*`.

**Alternatives considered**: Referencing `Gates`/`FreshnessKey`/`CacheEligibility` explicitly — harmless but
redundant; the F042 single-reference precedent is cleaner and documents the true coupling (this row exists to
produce F041's `CandidateGate`).

---

## D3 — Input is the F018 `Gate list`, reused verbatim (cost dropped at the join)

**Decision**: `resolve` consumes a `Gate list` (F018 `Gates.Model.Gate`). Each `Gate` carries both halves the
spec names: its stable identity (`Gate.Id : GateId`) and its **five-field freshness-key identity**
(`Gate.FreshnessKey : { Check; Domain; Cost; Environment; Command }`). The join sources `Check`, `Domain`,
`Environment`, `Command` from `Gate.FreshnessKey` and **drops `Cost`** (FR-002 — cost is not a freshness input,
F029 research D5).

**Rationale**: FR-012 says reuse existing vocabulary **verbatim** rather than redefine it. The F018 `Gate` is
the single already-typed value that carries the gate identity *and* the carried five-field freshness-key
identity together; consuming it redefines nothing. The host obtains this list trivially from the F019 route
result (`routeResult.SelectedGates |> List.map (fun sg -> sg.Gate)`), so "a routed change's selected gates" is
honoured without this pure core referencing the F019 `Route` assembly or its unused `SelectingPaths`.

**Alternatives considered**: (a) Consume the F019 `SelectedGate list` directly — rejected: pulls in the `Route`
assembly for fields (`SelectingPaths`, `CostRollup`) this join never reads, against the minimal-coupling
discipline F041 set when it defined its own `CandidateGate` rather than consume `SelectedGate`. (b) Define a
new minimal `{ Id; FreshnessKey }` input record — rejected: that *redefines* gate identity + freshness key,
exactly what FR-012 forbids when an upstream value already carries them.

---

## D4 — The sensed-facts bundle: `option` for repo-wide facts, `Map` for per-key facts

**Decision**: Introduce one new record, `SensedFacts`:

```fsharp
type SensedFacts =
    { RuleHash: RuleHash option              // repo-wide; None ⇒ not sensed
      GeneratorVersion: GeneratorVersion option
      Base: Revision option
      Head: Revision option
      CoveredArtifacts: Map<GateId, ArtifactHash list>     // per gate; key absent ⇒ not sensed
      CommandVersions: Map<CommandId, CommandVersion> }    // per command; key absent ⇒ not sensed
```

The four repo-wide facts are `option` (a missing repo-wide fact makes *every* gate that needs it unresolved,
Edge Cases). The two per-key facts are `Map`s keyed by the identity that scopes them — covered artifacts per
**gate**, command version per **command** — where **key-present** means *sensed* and **key-absent** means *not
sensed*.

**Rationale**: This is the minimal new vocabulary FR-012 permits, built entirely from already-typed F029
newtypes + F018 `GateId` + F014 `CommandId`. The `Map`-presence encoding is what lets D5 distinguish "sensed as
empty" from "not sensed at all" for covered artifacts — a present `GateId` key mapping to `[]` is a legitimate
resolved empty set; an absent key is *unresolved* (FR-003, Edge Cases). The core senses none of these; it joins
what it is handed (Assumption: sensing happens upstream).

**Alternatives considered**: (a) Make every field non-optional and treat a sentinel/empty value as "missing" —
rejected: it conflates "sensed empty" with "unsensed" and would force a fabricated default, violating FR-003.
(b) A flat `(GateId * ArtifactHash list) list` instead of a `Map` — rejected: a `Map` gives deterministic,
total lookup with present/absent semantics and no dependence on list order (FR-009).

---

## D5 — Two-outcome per-gate result; `Unresolved` is recompute-safe by construction

**Decision**: Model the per-gate outcome as a **closed two-case** union and attribute it to the gate id:

```fsharp
type MissingFact =                 // closed, in FR-002 field order (D6)
    | MissingRuleHash
    | MissingCoveredArtifacts
    | MissingCommandVersion
    | MissingGeneratorVersion
    | MissingBaseRevision
    | MissingHeadRevision

type ResolutionOutcome =
    | Resolved of FreshnessInputs              // the complete F029 ten-field value
    | Unresolved of MissingFact list           // non-empty, names every gap (no-hide)

type FreshnessResolutionEntry = { Gate: GateId; Outcome: ResolutionOutcome }
type FreshnessResolutionReport = FreshnessResolutionReport of FreshnessResolutionEntry list
```

The bridge to F041 is the accessor `candidate : FreshnessResolutionEntry -> CandidateGate option` — `Some {
Gate = e.Gate; Inputs = inputs }` for `Resolved inputs`, **`None`** for `Unresolved _`.

**Rationale**: FR-004 demands an `Unresolved` outcome be **impossible to convert into a resolved input set**. A
closed union makes that structural: `Unresolved` carries *no* `FreshnessInputs`, so there is no field surgery,
default, or coercion by which a consumer recovers one — and the `candidate` accessor, the only F041 bridge,
returns `None` for it. `Resolved` carries the full F029 `FreshnessInputs` verbatim, so `candidate` hands F041 a
`CandidateGate` **without adaptation** (FR-010, SC-007). The single union shell is the only genuinely new type;
its payloads (`FreshnessInputs`, `MissingFact` over reused field names, `GateId`) reuse upstream vocabulary —
the same minimalism F041 applied to its `CacheEligibilityVerdict`.

**Alternatives considered**: (a) `Resolved of CandidateGate` — rejected: duplicates the `GateId` already on the
entry; carrying `FreshnessInputs` + a `candidate` accessor keeps the model non-redundant while still giving the
"without adaptation" bridge. (b) A resolved value + a separate `bool`/error string — rejected: a stringly gap
is not exhaustive-matchable and invites the truncation FR-003 forbids; the closed `MissingFact` enum makes
"name every gap" a list the compiler keeps honest.

---

## D6 — No-hide: collect *every* missing fact, in a fixed order

**Decision**: For a gate, gather **all** unavailable required facts before deciding the outcome; if the list is
non-empty the gate is `Unresolved` carrying that list, **ordered by the fixed `MissingFact` enum order** (rule
hash → covered artifacts → command version → generator version → base → head, matching FR-002's field order).
A gate that declares **no command** (`FreshnessKey.Command = None`) never contributes `MissingCommandVersion`
(FR-005) — a command version is required *only* when a command is declared.

**Rationale**: FR-003/SC-002 require naming *exactly* the missing fact(s) "and no others", and the no-hide rule
(US2 scenario 3) requires the list never be "truncated to the first gap". Collecting all gaps then ordering by
a fixed enum makes the `Unresolved` payload deterministic and complete. Anchoring the order to FR-002's field
order keeps it legible against the spec. The command-version exemption is the consistent-absence rule (FR-005,
Edge Cases): `Command = None` resolves to `CommandVersion = None`, not a missing fact.

**Alternatives considered**: Short-circuit on the first missing fact — rejected outright by the no-hide rule.
Sorting the missing list alphabetically by token — rejected: the FR-002 field order is the documented,
spec-aligned order and matches how `FreshnessKey.diff` orders its `InputCategory` output.

---

## D7 — Determinism: one entry per gate, ordered by `GateId` ordinal, duplicates preserved

**Decision**: `resolve` maps each input `Gate` to exactly one `FreshnessResolutionEntry`, then sorts by
`String.CompareOrdinal` on `Gates.gateIdValue entry.Gate` with a **total structural tiebreak on the whole
entry** (`Gate` then `Outcome`) so duplicate gate identities are ordered deterministically and **neither merged
nor dropped** (Assumptions; Edge Cases). `resolve [] sensed = FreshnessResolutionReport []`.

**Rationale**: This is the exact ordering contract F041 `CacheEligibility.evaluate` documents ("sorts by
`String.CompareOrdinal` on `gateIdValue Gate` with a total structural tiebreak … any permutation of the
candidate list yields a byte-identical report"). Reusing it verbatim means the resolution report and the
downstream eligibility report share one order, so the later projection places each verdict under the same gate
the resolution attributed it to (FR-006/FR-007). Purity (FR-009) gives byte-identical output regardless of
working directory, clock, or filesystem — there is no I/O to perturb it. `gateIdValue` arrives transitively
through F041.

**Alternatives considered**: Forbidding duplicate gate identities as a precondition — deferred: the spec
Assumptions explicitly keep duplicates legal ("resolved independently and ordered deterministically … neither
merges nor drops"); a structural tiebreak satisfies that without a precondition, matching F041's `L-E4`.

---

## D8 — Accessor set + stable `missingFactToken`, mirroring F041

**Decision**: Expose total accessors alongside `resolve`: `entries` (unwrap), `candidate` (the F041 bridge,
D5), `isResolved : ResolutionOutcome -> bool`, `missingFacts : ResolutionOutcome -> MissingFact list` (`[]` for
`Resolved`), and `missingFactToken : MissingFact -> string` — a stable, injective wire token per case (e.g.
`MissingRuleHash → "ruleHash"`), for messages, tests, and the later projection.

**Rationale**: This mirrors F041's projection set (`entries`, `isReusable`, `reusableEvidence`,
`recomputeCause`) and F029's `categoryToken`. `missingFactToken` gives the later no-hide projection a stable
vocabulary without re-deriving it (the F042 precedent of reusing upstream token accessors verbatim). Every
match over the closed `MissingFact`/`ResolutionOutcome` unions is wildcard-free, so a future case is a compile
error here, never a silently mis-tokened field.

**Alternatives considered**: Omitting `missingFactToken` and letting the projection re-derive tokens — rejected:
that scatters the token vocabulary across rows, the drift F042 avoided by reusing `categoryToken`/`gateIdValue`.

---

## Resolved unknowns

| Topic | Resolution |
|---|---|
| Module name / entry point | `FS.GG.Governance.FreshnessResolution`, `resolve` (spec Assumption confirmed) |
| Input value | F018 `Gate list`, cost dropped at the join (D3) |
| New vocabulary | `SensedFacts`, `MissingFact`, `ResolutionOutcome`, `FreshnessResolutionEntry`, `FreshnessResolutionReport` (D4/D5) — everything else reused |
| Dependencies | one ProjectReference (F041); no third-party package (D2) |
| Ordering / duplicates | `GateId` ordinal + structural tiebreak; duplicates preserved (D7) |
| Recompute-safety | closed union; `Unresolved` carries no inputs; `candidate` returns `None` (D5) |
