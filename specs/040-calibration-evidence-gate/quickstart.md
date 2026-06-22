# Quickstart — Judge-vs-Human Calibration Evidence Gate (F040)

Validation guide for `FS.GG.Governance.Calibration`. See [data-model.md](./data-model.md) for the vocabulary and
[contracts/calibration-api.md](./contracts/calibration-api.md) for the signatures + laws.

## Prerequisites

- .NET `net10.0` SDK (repo standard).
- Restore is automatic on first build. No new third-party package; the only project references are
  `FS.GG.Governance.AgentReviewKey` (F035, for the reused `ModelId` / `ModelVersion` / `ReviewerPromptHash` identity
  tokens) and `FS.GG.Governance.ReviewRecord` (F038, for the reused `RecordedVerdict` token).

## Build

```bash
dotnet build src/FS.GG.Governance.Calibration
```

## FSI-exercise the surface (Principle I, design-first proof)

The design pass lives in a new F040 section of `scripts/prelude.fsx`. After building the library:

```bash
dotnet fsi scripts/prelude.fsx
```

Expected highlights (the worked examples from the contract):

```text
[F40] no samples                        ⇒ Uncalibrated NoCalibrationEvidence
[F40] 1 sample (lone), agreement 100    ⇒ Uncalibrated (TooFewSamples (SampleCount 1, SampleCount 3))
[F40] 2 samples, agreement 100          ⇒ Uncalibrated (TooFewSamples (SampleCount 2, SampleCount 3))
[F40] 3 samples, agreement 79 (< 80)    ⇒ Uncalibrated (AgreementBelowThreshold (AgreementLevel 79, AgreementLevel 80))
[F40] 3 samples, agreement 80 (= 80)    ⇒ Calibrated { ObservedSamples = SampleCount 3; RequiredSamples = SampleCount 3; … }
[F40] 5 samples, agreement 95           ⇒ Calibrated { ObservedSamples = SampleCount 5; RequiredSamples = SampleCount 3; … }
[F40] degenerate min=1, 1 sample        ⇒ Uncalibrated (TooFewSamples (SampleCount 1, SampleCount 2))   // no-single-sample floor
```

## Test

```bash
dotnet test tests/FS.GG.Governance.Calibration.Tests
```

Covers, against the **public** surface with real literal values (no mocks, no clock, no model, no human, no I/O):

- **US1 / SC-001** — uncalibrated by default: no evidence ⇒ `NoCalibrationEvidence`; too-few ⇒ `TooFewSamples`;
  agreement-below ⇒ `AgreementBelowThreshold`; self-confidence never calibrates (`UncalibratedDefaultTests`).
- **US2 / SC-002** — calibrated on sufficient evidence, the satisfied metrics named (`CalibratedTests`).
- **SC-003** — the two-threshold comparator across sample counts and agreement levels below / equal to / above their
  thresholds, and the no-single-sample floor (`ComparatorTests`).
- **US3 / SC-004** — totality across the full cross-product, never throws (`TotalityTests`).
- **US3 / SC-005** — determinism + purity under changed cwd / time / filesystem (`DeterminismTests`).
- **US3 / SC-006** — `Calibrated` carries no blocking action / no enforcement verdict (`NecessaryNotSufficientTests`).
- **FR-001** — `Calibrated` is unrepresentable without `CalibrationMetrics` (asserted structurally + in
  `CalibratedTests`).
- **Principle II / SC-007** — surface baseline + `AgentReviewKey`/`ReviewRecord`-only scope guard
  (`SurfaceDriftTests`).

## Re-bless the surface baseline (only when the public surface intentionally changes)

```bash
BLESS_SURFACE=1 dotnet test tests/FS.GG.Governance.Calibration.Tests
```

Writes `surface/FS.GG.Governance.Calibration.surface.txt`. Commit the regenerated baseline alongside the `.fsi`
change (Tier-1 discipline).

## Confirm nothing else moved (SC-007)

```bash
dotnet build && dotnet test
```

Existing `src/`, `surface/`, and merged test projects are unchanged; the new project + test project are purely
additive.
