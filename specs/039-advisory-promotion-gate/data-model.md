# Phase 1 Data Model — Advisory-to-Blocking Promotion Gate (F039)

The vocabulary of `FS.GG.Governance.AdvisoryPromotion`. Every type is an immutable value; the one reused type
(`EvidenceRef`) is opened from `FS.GG.Governance.EvidenceReuse.Model` and **not redefined**. No field carries raw
bytes, host paths, clock readings, verdict content, or product vocabulary. Compile order: `Model` → `AdvisoryPromotion`.

## Reused verbatim (not redefined)

| Type | Origin | Use here |
|---|---|---|
| `EvidenceRef = EvidenceRef of string` | F030 `EvidenceReuse.Model` | The opaque deterministic-backing-evidence token; presence (`Some`) satisfies the first basis. Never parsed/validated/dereferenced (research D3). |

## New vocabulary (this feature)

### `PromotionBasis` — the closed three-value permitted-basis vocabulary (FR-002)

```fsharp
type PromotionBasis =
    | DeterministicBackingEvidence   // a deterministic check independently corroborates the finding
    | RepeatedReviewConfidence       // the same verdict reproduced across enough independent reviews
    | HumanSignOff                   // a human explicitly signed off on the finding
```

Exactly three cases, no fourth. The model's own self-reported confidence is **not** a case (FR-002, SC-001).

### `ConfirmationCount` / `ConfidenceThreshold` — the confidence inputs (FR-004)

```fsharp
type ConfirmationCount = ConfirmationCount of int   // independent repeated-review confirmations supplied
type ConfidenceThreshold = ConfidenceThreshold of int   // the supplied policy threshold to clear
```

Both supplied; neither parsed nor sourced by this core. A count of `0` (or absent, modelled as `ConfirmationCount
0`) means no repeated review (research D5). Negative/degenerate values are total inputs (the comparator never throws).

### `SignOff` — the opaque human-sign-off marker (FR-002, research D3)

```fsharp
type SignOff = SignOff of string
```

Presence (`Some (SignOff _)`) satisfies the human-sign-off basis. Opaque: never parsed, validated, or dereferenced;
an empty string is a literal value.

### `AdvisoryReason` — the no-hide attribution for a held finding (FR-005, research D5)

```fsharp
type AdvisoryReason =
    | NoPermittedBasis
    | ConfidenceBelowThreshold of ConfirmationCount * ConfidenceThreshold
```

`ConfidenceBelowThreshold` carries the supplied count and threshold so the hold is attributable
(US1 scenario 2). `NoPermittedBasis` is the bare default: nothing attempted.

### `PromotionFacts` — the supplied levers, the sole input to `decide` (research D2/D4)

```fsharp
type PromotionFacts =
    { BackingEvidence: EvidenceRef option
      Confirmations: ConfirmationCount
      ConfidenceThreshold: ConfidenceThreshold
      SignOff: SignOff option }
```

The finding under decision is **not** a field — the caller associates the result with its finding (research D2).

### `PromotionDecision` — the two-outcome gate verdict (FR-001, research D6)

```fsharp
type PromotionDecision =
    | StaysAdvisory of AdvisoryReason
    | EligibleToBlock of PromotionBasis * PromotionBasis list   // head + tail ⇒ never empty
```

`EligibleToBlock`'s head + tail encoding makes an empty-basis promotion **unrepresentable** (FR-001). The payload
names **every** satisfied basis, in the fixed order *DeterministicBackingEvidence, RepeatedReviewConfidence,
HumanSignOff* (the no-hide rule, SC-002).

## The decision (behaviour summary; full laws in `contracts/advisory-promotion-api.md`)

Given `facts : PromotionFacts`, `decide` computes the satisfied-basis list in fixed order:

| Basis | Satisfied when |
|---|---|
| `DeterministicBackingEvidence` | `facts.BackingEvidence = Some _` |
| `RepeatedReviewConfidence` | `c >= t && c >= 2`, where `ConfirmationCount c = facts.Confirmations`, `ConfidenceThreshold t = facts.ConfidenceThreshold` (research D7) |
| `HumanSignOff` | `facts.SignOff = Some _` |

Then:

- **non-empty** satisfied list `b :: rest` ⇒ `EligibleToBlock (b, rest)`.
- **empty** satisfied list ⇒ `StaysAdvisory r`, where `r = ConfidenceBelowThreshold (facts.Confirmations,
  facts.ConfidenceThreshold)` if `c >= 1` (a review was attempted but fell short), else `NoPermittedBasis`.

Total, deterministic, pure over the supplied facts; identical inputs ⇒ identical decision.

## State & relationships

No state, no transitions, no identity derivation — this is a *decision* core, not a record core. The only
relationship is `PromotionFacts → PromotionDecision` via `decide`. `satisfiedBases` projects a `PromotionDecision`
back to a `PromotionBasis list` (`[]` for advisory, the full list for eligible). Unwrappers (`signOffValue`,
`confirmationValue`, `thresholdValue`) expose the carried primitives for audit/tests.
