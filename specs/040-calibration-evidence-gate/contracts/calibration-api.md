# Contract — `FS.GG.Governance.Calibration` public API (F040)

The Tier-1 public surface this row commits, with the laws each member upholds. The two `.fsi` files are the sole
declaration of visibility (Principle II); the reflective `SurfaceDrift` test pins this surface to
`surface/FS.GG.Governance.Calibration.surface.txt` and guards the dependency scope. All operations are **pure, total,
and deterministic** (FR-003/FR-006): defined for every well-typed input, never throwing; reading no
clock/filesystem/git/environment/network, invoking no model/agent, running no review, performing no human comparison,
hashing no bytes, persisting nothing; identical for identical input regardless of evaluation time, machine, process,
or working directory.

## `Model` (types — see [data-model.md](../data-model.md))

`AgreementClassification`, `JudgeIdentity`, `ComparisonSample`, `SampleCount`, `AgreementLevel`, `CalibrationEvidence`,
`CalibrationThresholds`, `CalibrationMetrics`, `CalibrationReason`, `CalibrationDecision`. Reuses `ModelId` /
`ModelVersion` / `ReviewerPromptHash` from `FS.GG.Governance.AgentReviewKey.Model` and `RecordedVerdict` from
`FS.GG.Governance.ReviewRecord.Model`, verbatim.

## `Calibration` operations

```fsharp
val decide: thresholds: CalibrationThresholds -> evidence: CalibrationEvidence -> CalibrationDecision
val calibrationReason: decision: CalibrationDecision -> CalibrationReason option
val calibrationMetrics: decision: CalibrationDecision -> CalibrationMetrics option
val isCalibrated: decision: CalibrationDecision -> bool
val observedSampleCount: evidence: CalibrationEvidence -> SampleCount
val sampleCountValue: count: SampleCount -> int
val agreementValue: level: AgreementLevel -> int
```

### `decide` — the single total decision (FR-003)

Let `observed = sampleCountValue (observedSampleCount evidence)` (i.e. `List.length evidence.Samples`), `min =
sampleCountValue thresholds.MinimumSamples`, `effectiveMin = max min 2`, `obs = agreementValue
evidence.ObservedAgreement`, `req = agreementValue thresholds.MinimumAgreement`. In precedence order:

- **L-D1 (no evidence)** — `observed = 0` ⇒ `decide thresholds evidence = Uncalibrated NoCalibrationEvidence`.
- **L-D2 (too few samples)** — `observed > 0` and `observed < effectiveMin` ⇒ `Uncalibrated (TooFewSamples (SampleCount
  observed, SampleCount effectiveMin))`.
- **L-D3 (agreement below threshold)** — `observed >= effectiveMin` and `obs < req` ⇒ `Uncalibrated
  (AgreementBelowThreshold (evidence.ObservedAgreement, thresholds.MinimumAgreement))`.
- **L-D4 (calibrated)** — `observed >= effectiveMin` and `obs >= req` ⇒ `Calibrated { ObservedSamples = SampleCount
  observed; RequiredSamples = SampleCount effectiveMin; ObservedAgreement = evidence.ObservedAgreement;
  RequiredAgreement = thresholds.MinimumAgreement }`.

Derived guarantees:

- **L-D5 (uncalibrated by default, FR-003/SC-001)** — whenever the evidence is absent or falls short of a threshold,
  the result is `Uncalibrated _`. There is no input, ordering, or fallback that yields `Calibrated` from insufficient
  evidence.
- **L-D6 (calibrated iff both gates pass, FR-003/FR-004)** — `decide thresholds evidence` is `Calibrated _` **iff**
  `observed >= effectiveMin && obs >= req`.
- **L-D7 (inclusive `>=` + no single sample, FR-004/SC-003)** — the sample gate is `observed >= max(min, 2)` and the
  agreement gate is `obs >= req`, both inclusive. For `min >= 2` the sample gate is exactly `observed >= min`
  (verified across `observed < min`, `observed = min`, `observed > min`); a lone sample (`observed = 1`) never passes
  for any `min` (`max(min, 2) >= 2 > 1`); `observed = 0` is `NoCalibrationEvidence`. An agreement exactly equal to the
  threshold (`obs = req`) passes (US2 scenario 2).
- **L-D8 (metrics named on success, FR-005/SC-002)** — a `Calibrated` decision carries `CalibrationMetrics` naming the
  observed sample count + agreement level and their effective requirements, not a bare flag; `calibrationMetrics
  (decide …) = Some metrics` exposes them.
- **L-D9 (reason named on hold, FR-005)** — an `Uncalibrated` decision carries a `CalibrationReason` naming which
  requirement was unmet; `calibrationReason (decide …) = Some reason`.
- **L-D10 (self-confidence is not calibration, FR-002/SC-001)** — there is no input by which the model's own
  confidence enters; only judge-vs-human comparison samples + the supplied agreement level populate the evidence.
- **L-D11 (verdicts opaque, FR-007)** — the per-sample `JudgeVerdict` / `HumanVerdict` are never produced,
  interpreted, compared, re-scored, or thresholded; only the `Agreement` classification and the supplied summary count
  / level drive `decide`.
- **L-D12 (effective-minimum honesty, D5/D7)** — `RequiredSamples` (in metrics) and the `required` field of
  `TooFewSamples` are both `SampleCount (max(min, 2))`, the effective bar actually applied — never an understated
  supplied `min`.
- **L-D13 (totality, SC-004)** — `decide` returns a decision and never throws for every `CalibrationThresholds` ×
  `CalibrationEvidence` (any sample list including `[]` and singletons, any `AgreementLevel`, any `SampleCount` —
  including negative/degenerate supplied integers).
- **L-D14 (determinism/purity, SC-005)** — `decide t e = decide t e` always; no clock/file/model/network read, no
  human consulted.
- **L-D15 (necessary-not-sufficient, FR-008/SC-006)** — `CalibrationDecision` carries no blocking action, no severity,
  no effective severity, no enforcement verdict, and no F039 eligibility; `Calibrated` asserts only beyond-advisory
  maturity, never that a protected boundary may be blocked.

### `calibrationReason` / `calibrationMetrics` / `isCalibrated` — projections

- **L-P1** — `calibrationReason (Uncalibrated r) = Some r`; `calibrationReason (Calibrated _) = None`.
- **L-P2** — `calibrationMetrics (Calibrated m) = Some m`; `calibrationMetrics (Uncalibrated _) = None`.
- **L-P3** — `isCalibrated (Calibrated _) = true`; `isCalibrated (Uncalibrated _) = false`.

### `observedSampleCount` / unwrappers

- **L-O1** — `observedSampleCount evidence = SampleCount (List.length evidence.Samples)`.
- **L-U1** — `sampleCountValue (SampleCount n) = n`.
- **L-U2** — `agreementValue (AgreementLevel n) = n`.

## Worked examples (pinned by tests)

Let `id = { Model = ModelId "gpt"; ModelVersion = ModelVersion "1"; PromptHash = ReviewerPromptHash "h" }`, and let an
`Agreeing` sample be `{ JudgeVerdict = RecordedVerdict "v"; HumanVerdict = RecordedVerdict "v"; Agreement = Agreeing }`.
Thresholds `T = { MinimumSamples = SampleCount 3; MinimumAgreement = AgreementLevel 80 }`.

| `Samples` (count) | `ObservedAgreement` | `decide T evidence` result |
|---|---|---|
| `[]` (0) | `AgreementLevel 95` | `Uncalibrated NoCalibrationEvidence` |
| 1 sample | `AgreementLevel 100` | `Uncalibrated (TooFewSamples (SampleCount 1, SampleCount 3))` *(lone sample never calibrates)* |
| 2 samples | `AgreementLevel 100` | `Uncalibrated (TooFewSamples (SampleCount 2, SampleCount 3))` |
| 3 samples | `AgreementLevel 79` | `Uncalibrated (AgreementBelowThreshold (AgreementLevel 79, AgreementLevel 80))` |
| 3 samples | `AgreementLevel 80` | `Calibrated { ObservedSamples = SampleCount 3; RequiredSamples = SampleCount 3; ObservedAgreement = AgreementLevel 80; RequiredAgreement = AgreementLevel 80 }` *(agreement inclusive at the threshold)* |
| 5 samples | `AgreementLevel 95` | `Calibrated { ObservedSamples = SampleCount 5; RequiredSamples = SampleCount 3; ObservedAgreement = AgreementLevel 95; RequiredAgreement = AgreementLevel 80 }` |

With a degenerate `MinimumSamples = SampleCount 1`, a single sample still yields `Uncalibrated (TooFewSamples
(SampleCount 1, SampleCount 2))` — the effective minimum `max(1, 2) = 2` (the no-single-sample floor, L-D7/L-D12).

## Scope guard (SurfaceDrift test, Principle II / SC-007)

The `FS.GG.Governance.Calibration` assembly references **only** `FS.GG.Governance.AgentReviewKey` (F035) and
`FS.GG.Governance.ReviewRecord` (F038), their transitive pure cores (`PromptIsolation`, `SensedMetadata`,
`FreshnessKey`, `Config` — unused here), and `FSharp.Core` / BCL. It references no host/CLI/adapter assembly, no
`Enforcement`/`Gates`/`Snapshot`/`Route`/`Findings`/`VerdictReuse`/`AdvisoryPromotion`, and adds no third-party
package. Any drift in the rendered public surface or the referenced-assembly set fails the test (with the
`BLESS_SURFACE=1` intentional-rebless path).
