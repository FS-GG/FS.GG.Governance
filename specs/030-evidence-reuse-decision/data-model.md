# Phase 1 Data Model: Evidence-Reuse Decision Core

All types live in `FS.GG.Governance.EvidenceReuse.Model` (sole public declaration: `Model.fsi`). They are
product-neutral, comparable values carrying no raw bytes, host paths, clock readings, or product vocabulary.
The F029 freshness vocabulary (`FreshnessInputs`, `InputCategory`) is `open`ed from
`FS.GG.Governance.FreshnessKey.Model`; nothing in `FreshnessKey`/`Config` is modified (FR-010). Names are the
recommended spelling; minor identifier adjustments at implementation are allowed as long as the contracts in
`contracts/` hold.

## Reused vocabulary (from `FreshnessKey.Model`, F029 — verbatim)

| Type | Form | Role in this feature |
|---|---|---|
| `FreshnessInputs` | record (10 categories) | The world a recorded evidence was produced against, and the candidate's world. Compared with F029 `matches`/`diff`. |
| `InputCategory` | 10-case DU | The no-hide vocabulary returned inside `RecomputeCause.InputsChanged` (via F029 `diff`). |

The operations `FreshnessKey.matches` / `FreshnessKey.diff` are consumed verbatim; this core defines no new
comparison over `FreshnessInputs`.

## New opaque newtype (this feature)

Single-case `of string`, opaque and comparable; the actual reference is minted at the edge and supplied as
data (FR-001). No validation, no parsing, no dereference — an empty string is a literal value (FR-012).

| Type | Form | Represents |
|---|---|---|
| `EvidenceRef` | `EvidenceRef of string` | A handle to *already-recorded* evidence (e.g. a content-addressed pointer / recorded-evidence id). Carried back on *Reuse*; never interpreted by this core. |

## Key entity — `RecordedEvidence`

One stored entry: the world the evidence was recorded against, paired with its opaque reference (FR-001).

```text
RecordedEvidence =
    { Inputs:   FreshnessInputs   // the world this evidence was recorded against (F029)
      Evidence: EvidenceRef }     // the opaque handle to that recorded evidence
```

## Key entity — `ReuseStore`

The immutable collection of recorded entries — the supplied, in-value "what has been recorded so far"
(FR-002). A single-case DU over a list; **newest-first** by `record` convention (research D4). Not a live
cache, connection, or file.

```text
ReuseStore = ReuseStore of RecordedEvidence list
```

- `empty : ReuseStore` — the `ReuseStore []` starting value.
- Invariant maintained by `record` (not enforced on hand-built values): **at most one entry per
  matching-input class** (FR-008). `decide` is still total and deterministic even if this invariant is
  violated by a hand-built store (head-first scan ⇒ most-recent wins).

## Key entity — `RecomputeCause` (the no-hide explanation)

Why no entry served — always present and locatable (FR-006, Principle VI).

```text
RecomputeCause =
    | NoPriorEvidence                       // no entry shares the candidate's GateId (Check+Domain)
    | InputsChanged of InputCategory list   // prior evidence for this gate exists; these categories changed
```

- `InputsChanged` always carries a **non-empty** list (it is produced only when no entry fully matches), and
  it **never** contains `CheckIdentity`/`DomainIdentity` (those are equal for the chosen prior entry by
  construction — research D5).
- The two cases are crisply distinct: `NoPriorEvidence` ("never recorded this gate") vs `InputsChanged`
  ("recorded, but the world moved"). The empty list is deliberately *not* a valid `InputsChanged` payload —
  an all-categories-agree situation is a `Reuse`, never a `Recompute`.

## Key entity — `ReuseDecision`

The total result of `decide` (FR-003).

```text
ReuseDecision =
    | Reuse of EvidenceRef        // some entry matched on every category — reuse this evidence
    | Recompute of RecomputeCause // no entry matched — here is why
```

## Relationships / data flow

```text
candidate: FreshnessInputs ─┐
                            ├─ EvidenceReuse.decide ─▶ Reuse EvidenceRef
store:     ReuseStore ──────┘                        └ Recompute (NoPriorEvidence | InputsChanged [..])

inputs: FreshnessInputs ─┐
evidence: EvidenceRef ───┼─ EvidenceReuse.record ─▶ ReuseStore'  (prior full-match removed, new entry at head)
store:    ReuseStore ────┘
```

- `decide` uses `FreshnessKey.matches` for the reuse test and `FreshnessKey.diff` for the
  `InputsChanged` payload; the `NoPriorEvidence` vs `InputsChanged` split keys off `Check`+`Domain`
  equality (research D5).
- `record` uses `FreshnessKey.matches` to drop a superseded entry, then conses the new entry (research D4).
- Both are pure over their supplied values: no clock, filesystem, git, environment, or network (FR-009).

## Validation / totality rules

| Rule | Source |
|---|---|
| Every `FreshnessInputs`, `EvidenceRef`, and `ReuseStore` is a valid input; no value throws. | FR-003, FR-012 |
| `decide` is *Reuse* iff some entry `matches` the candidate on every category; else *Recompute*. | FR-004 |
| On *Reuse*, the carried `EvidenceRef` is from a matching entry; with duplicates, the most-recent (head-first). | FR-005 |
| Every *Recompute* carries a located cause (`NoPriorEvidence` or non-empty `InputsChanged`). | FR-006 |
| `record` does not mutate its input store; result holds ≤1 entry per matching-input class. | FR-007, FR-008 |
| Covered-artifact order/duplication never changes a decision (inherited from F029 `matches`/`diff`). | FR-004 |
