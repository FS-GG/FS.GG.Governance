# Phase 1 Data Model — Judge-vs-Human Calibration Evidence Gate (F040)

The vocabulary of `FS.GG.Governance.Calibration`. Every type is an immutable value; the reused types (`ModelId` /
`ModelVersion` / `ReviewerPromptHash` from F035, `RecordedVerdict` from F038) are opened from their owning modules and
**not redefined**. No field carries raw bytes, host paths, clock readings, or product vocabulary. Compile order:
`Model` → `Calibration`.

## Reused verbatim (not redefined)

| Type | Origin | Use here |
|---|---|---|
| `ModelId = ModelId of string` | F035 `AgentReviewKey.Model` | The judge's id — part of the per-judge calibration scope. Opaque (research D3). |
| `ModelVersion = ModelVersion of string` | F035 `AgentReviewKey.Model` | The judge's version — part of the scope. Opaque. |
| `ReviewerPromptHash = ReviewerPromptHash of string` | F035 `AgentReviewKey.Model` | The reviewer-prompt hash — part of the scope. Opaque. |
| `RecordedVerdict = RecordedVerdict of string` | F038 `ReviewRecord.Model` | The opaque judge / human verdict a comparison sample pairs; never interpreted, only the `Agreement` is consumed (FR-007, research D3/D4). |

## New vocabulary (this feature)

### `AgreementClassification` — the consumed per-sample fact (FR-002, FR-007)

```fsharp
type AgreementClassification =
    | Agreeing
    | Disagreeing
```

The already-classified outcome of one judge-vs-human comparison — the **only** thing `decide` consumes from a sample.
The model's own self-reported confidence is **not** a case (FR-002, SC-001): calibration is human comparison, never
self-assessment.

### `JudgeIdentity` — the per-judge calibration scope (FR-009 SHOULD, research D3)

```fsharp
type JudgeIdentity =
    { Model: ModelId
      ModelVersion: ModelVersion
      PromptHash: ReviewerPromptHash }
```

Reuses F035 identity verbatim. Calibration is **per judge identity, not global** (Edge Cases / Assumptions): evidence
gathered under one model id / version / reviewer-prompt hash does not calibrate a different identity. This core
**records** the scope (no-hide / audit) and trusts the supplied evidence is already filtered to one identity; it does
not itself filter (research D3 — the spec permits either; receiving identity-filtered evidence is chosen).

### `ComparisonSample` — one judge-vs-human comparison (FR-002, research D3/D4)

```fsharp
type ComparisonSample =
    { JudgeVerdict: RecordedVerdict      // opaque (F038); never interpreted (FR-007)
      HumanVerdict: RecordedVerdict      // opaque (F038); never interpreted (FR-007)
      Agreement: AgreementClassification }   // the consumed already-classified fact
```

The unit of calibration evidence. Pairs the agent reviewer's verdict with a human's verdict on the same item
(reusing F038 `RecordedVerdict` for both, never parsed/dereferenced) and carries the `Agreement` classification —
the consumed fact.

### `SampleCount` / `AgreementLevel` — the summarised-observation newtypes (FR-004, research D4)

```fsharp
type SampleCount = SampleCount of int       // a comparison-sample count (observed-derived or threshold-minimum)
type AgreementLevel = AgreementLevel of int   // an agreement level on the edge's own opaque scale
```

Both are single-case newtypes preserving type-distinctness (a swapped count/level is a compile error). The
`AgreementLevel` scale (percent, permille, basis points, …) is the edge's; this core never parses or interprets it —
it only compares with `>=`. Negative/degenerate values are total inputs (the comparator never throws).

### `CalibrationEvidence` — the collection, scoped to one identity (FR-002, research D4)

```fsharp
type CalibrationEvidence =
    { Scope: JudgeIdentity
      Samples: ComparisonSample list
      ObservedAgreement: AgreementLevel }
```

The comparison samples for one agent reviewer, scoped to a judge identity, plus the **supplied** observed agreement
level. The comparison-sample count is **derived** from `List.length Samples` (`observedSampleCount`), not a separate
supplied field — the honest count of evidence actually present (research D4). An empty `Samples` list is the ordinary
"no calibration evidence" value (Edge Cases), never malformed.

### `CalibrationThresholds` — the supplied policy levers (FR-003)

```fsharp
type CalibrationThresholds =
    { MinimumSamples: SampleCount
      MinimumAgreement: AgreementLevel }
```

The two supplied thresholds the evidence is measured against: a minimum comparison-sample count and a minimum
agreement level. Supplied values, not parsed by this core. No freshness window — recency is not modelled here
(research D8).

### `CalibrationMetrics` — the satisfied metrics named by a calibrated outcome (FR-005, research D5)

```fsharp
type CalibrationMetrics =
    { ObservedSamples: SampleCount
      RequiredSamples: SampleCount      // the EFFECTIVE minimum applied: max(MinimumSamples, 2)
      ObservedAgreement: AgreementLevel
      RequiredAgreement: AgreementLevel }
```

The no-hide record of exactly what cleared the gate (US2 scenario 3). `RequiredSamples` is the **effective** minimum
`max(MinimumSamples, 2)` (research D7), so the named bar is truthful even under a degenerate supplied minimum.

### `CalibrationReason` — the no-hide attribution for an uncalibrated outcome (FR-005, research D5)

```fsharp
type CalibrationReason =
    | NoCalibrationEvidence
    | TooFewSamples of observed: SampleCount * required: SampleCount
    | AgreementBelowThreshold of observed: AgreementLevel * required: AgreementLevel
```

`NoCalibrationEvidence`: no comparison samples at all (the design's default). `TooFewSamples`: a count below the
effective minimum — too thin to be meaningful (carries observed + effective-required). `AgreementBelowThreshold`: enough
samples but agreement below the threshold (carries observed + required). **No `Stale` case** — recency deferred
(research D8).

### `CalibrationDecision` — the two-outcome gate verdict (FR-001, research D6)

```fsharp
type CalibrationDecision =
    | Uncalibrated of CalibrationReason
    | Calibrated of CalibrationMetrics
```

`Calibrated` **always** carries its satisfied `CalibrationMetrics`, so a *calibrated* decision without the thresholds
having been met is **unrepresentable** (FR-001). `Uncalibrated` always carries its `CalibrationReason`. A `Calibrated`
value asserts only beyond-advisory *maturity*: it carries no blocking action and no enforcement verdict (FR-008).

## The decision (behaviour summary; full laws in `contracts/calibration-api.md`)

Given `thresholds : CalibrationThresholds` and `evidence : CalibrationEvidence`, let `observed = List.length
evidence.Samples`, `min = MinimumSamples`, `effectiveMin = max min 2`, `obs = ObservedAgreement`, `req =
MinimumAgreement`. `decide` returns, in precedence order:

| Condition | Result |
|---|---|
| `observed = 0` | `Uncalibrated NoCalibrationEvidence` |
| `observed < effectiveMin` | `Uncalibrated (TooFewSamples (SampleCount observed, SampleCount effectiveMin))` |
| `obs < req` | `Uncalibrated (AgreementBelowThreshold (evidence.ObservedAgreement, thresholds.MinimumAgreement))` |
| otherwise | `Calibrated { ObservedSamples = SampleCount observed; RequiredSamples = SampleCount effectiveMin; ObservedAgreement = evidence.ObservedAgreement; RequiredAgreement = thresholds.MinimumAgreement }` |

Total, deterministic, pure over the supplied facts; identical inputs ⇒ identical decision. Inclusive `>=` is realized
as `not (observed < effectiveMin)` and `not (obs < req)`; the no-single-sample floor is the `max min 2` (research D7).

## State & relationships

No state, no transitions, no identity derivation — this is a *decision* core, not a record core. The only
relationship is `(CalibrationThresholds, CalibrationEvidence) → CalibrationDecision` via `decide`. `calibrationReason`
/ `calibrationMetrics` project a `CalibrationDecision` to `CalibrationReason option` / `CalibrationMetrics option`;
`isCalibrated` is the boolean projection; `observedSampleCount` derives the honest `SampleCount` from evidence; the
unwrappers (`sampleCountValue`, `agreementValue`) expose the carried primitives for audit/tests.
