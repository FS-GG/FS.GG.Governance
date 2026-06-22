# Contract — `FS.GG.Governance.AdvisoryPromotion` public API (F039)

The Tier-1 public surface this row commits, with the laws each member upholds. The two `.fsi` files are the sole
declaration of visibility (Principle II); the reflective `SurfaceDrift` test pins this surface to
`surface/FS.GG.Governance.AdvisoryPromotion.surface.txt` and guards the dependency scope. All operations are **pure,
total, and deterministic** (FR-003/FR-006): defined for every well-typed input, never throwing; reading no
clock/filesystem/git/environment/network, invoking no model/agent, hashing no bytes, running no review, persisting
nothing; byte-for-byte identical for identical input regardless of evaluation time, machine, process, or working
directory.

## `Model` (types — see [data-model.md](../data-model.md))

`PromotionBasis`, `ConfirmationCount`, `ConfidenceThreshold`, `SignOff`, `AdvisoryReason`, `PromotionFacts`,
`PromotionDecision`. Reuses `EvidenceRef` from `FS.GG.Governance.EvidenceReuse.Model` verbatim.

## `AdvisoryPromotion` operations

```fsharp
val decide: facts: PromotionFacts -> PromotionDecision
val satisfiedBases: decision: PromotionDecision -> PromotionBasis list
val signOffValue: signOff: SignOff -> string
val confirmationValue: count: ConfirmationCount -> int
val thresholdValue: threshold: ConfidenceThreshold -> int
```

### `decide` — the single total decision (FR-003)

Let `c` = `confirmationValue facts.Confirmations`, `t` = `thresholdValue facts.ConfidenceThreshold`. Define the
satisfied-basis list, in **fixed order**:

```
bases = [ DeterministicBackingEvidence   if facts.BackingEvidence = Some _
          RepeatedReviewConfidence       if c >= t && c >= 2
          HumanSignOff                   if facts.SignOff = Some _ ]
```

Then:

- **L-D1 (eligible)** — `bases = b :: rest` ⇒ `decide facts = EligibleToBlock (b, rest)`.
- **L-D2 (advisory, attempted)** — `bases = []` and `c >= 1` ⇒ `decide facts = StaysAdvisory (ConfidenceBelowThreshold
  (facts.Confirmations, facts.ConfidenceThreshold))`.
- **L-D3 (advisory, bare)** — `bases = []` and `c < 1` ⇒ `decide facts = StaysAdvisory NoPermittedBasis`.

Derived guarantees:

- **L-D4 (advisory by default, FR-003/SC-001)** — no basis satisfied ⇒ always `StaysAdvisory _`. There is no input,
  ordering, or fallback that yields `EligibleToBlock` from an empty `bases`.
- **L-D5 (eligible iff a basis, FR-003)** — `decide facts` is `EligibleToBlock _` **iff** `bases` is non-empty.
- **L-D6 (all named, no-hide, FR-005/SC-002)** — when eligible, `satisfiedBases (decide facts) = bases` lists
  **every** satisfied basis in the fixed order; with two or three satisfied, all appear.
- **L-D7 (no empty promotion, FR-001)** — `EligibleToBlock (b, rest)` always has the head `b`, so the named-basis set
  is never empty; the type makes the empty case unrepresentable.
- **L-D8 (inclusive `>=` + no single sample, FR-004/SC-003)** — `RepeatedReviewConfidence ∈ bases` iff `c >= t &&
  c >= 2`. For `t >= 2` this is exactly `c >= t` (verified across `c < t`, `c = t`, `c > t`); a lone review (`c = 1`)
  never satisfies it for any `t`; `c = 0`/absent never satisfies it.
- **L-D9 (self-confidence is not a basis, FR-002/SC-001)** — there is no input by which the model's own confidence
  enters; only the three permitted bases can populate `bases`.
- **L-D10 (verdict opaque, FR-007)** — the finding's verdict is not an input to `decide` and is never produced,
  interpreted, compared, re-scored, or thresholded.
- **L-D11 (totality, SC-004)** — `decide` returns a decision and never throws for every `PromotionFacts` (any
  `EvidenceRef option`, any non-negative — or negative — `ConfirmationCount`/`ConfidenceThreshold`, any `SignOff
  option`).
- **L-D12 (determinism/purity, SC-005)** — `decide facts = decide facts` always; no clock/file/model/network read.
- **L-D13 (necessary-not-sufficient, FR-008/SC-006)** — `PromotionDecision` carries no blocking action, no severity,
  and no calibration claim; `EligibleToBlock` asserts only promotion eligibility, never that a boundary may be blocked.

### `satisfiedBases` — uniform projection (research D6)

- **L-S1** — `satisfiedBases (StaysAdvisory _) = []`.
- **L-S2** — `satisfiedBases (EligibleToBlock (b, rest)) = b :: rest` (non-empty; fixed order).

### Unwrappers

- **L-U1** — `signOffValue (SignOff s) = s`.
- **L-U2** — `confirmationValue (ConfirmationCount n) = n`.
- **L-U3** — `thresholdValue (ConfidenceThreshold n) = n`.

## Worked examples (pinned by tests)

| `BackingEvidence` | `Confirmations` | `ConfidenceThreshold` | `SignOff` | `decide` result |
|---|---|---|---|---|
| `None` | `0` | `3` | `None` | `StaysAdvisory NoPermittedBasis` |
| `None` | `2` | `3` | `None` | `StaysAdvisory (ConfidenceBelowThreshold (ConfirmationCount 2, ConfidenceThreshold 3))` |
| `None` | `1` | `1` | `None` | `StaysAdvisory (ConfidenceBelowThreshold (ConfirmationCount 1, ConfidenceThreshold 1))` *(lone review never clears — L-D8 floor)* |
| `Some (EvidenceRef "e")` | `0` | `3` | `None` | `EligibleToBlock (DeterministicBackingEvidence, [])` |
| `None` | `3` | `3` | `None` | `EligibleToBlock (RepeatedReviewConfidence, [])` |
| `None` | `0` | `3` | `Some (SignOff "u")` | `EligibleToBlock (HumanSignOff, [])` |
| `Some (EvidenceRef "e")` | `5` | `3` | `Some (SignOff "u")` | `EligibleToBlock (DeterministicBackingEvidence, [RepeatedReviewConfidence; HumanSignOff])` |

## Scope guard (SurfaceDrift test, Principle II / SC-007)

The `FS.GG.Governance.AdvisoryPromotion` assembly references **only** `FS.GG.Governance.EvidenceReuse` (+ its
transitive pure cores `FreshnessKey` / `Config`, unused here) and `FSharp.Core` / BCL. It references no
host/CLI/adapter assembly, no `Enforcement`/`Gates`/`Snapshot`/`Route`/`Findings`/`ReviewRecord`, and adds no
third-party package. Any drift in the rendered public surface or the referenced-assembly set fails the test (with the
`BLESS_SURFACE=1` intentional-rebless path).
