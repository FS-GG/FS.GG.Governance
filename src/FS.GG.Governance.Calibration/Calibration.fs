// Calibration-evidence operations for the judge-vs-human calibration-evidence decision core (F040). The public
// surface is fixed by Calibration.fsi (Principle II); no top-level binding here carries an access modifier (the
// `calibrated` comparator helper is private by its absence from the .fsi). `decide` is pure, total, and
// deterministic (FR-003, FR-006): no clock, filesystem, git, environment, or network; no model invoked; no
// bytes hashed; no review run; no human consulted; no cache/verdict operation; identical inputs always yield
// the identical decision. The decision is fixed by contracts/calibration-api.md and data-model.md.

namespace FS.GG.Governance.Calibration

open FS.GG.Governance.Calibration.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Calibration =

    let sampleCountValue (count: SampleCount) : int =
        let (SampleCount n) = count
        n

    let agreementValue (level: AgreementLevel) : int =
        let (AgreementLevel n) = level
        n

    let observedSampleCount (evidence: CalibrationEvidence) : SampleCount =
        SampleCount(List.length evidence.Samples) // L-O1

    let decide (thresholds: CalibrationThresholds) (evidence: CalibrationEvidence) : CalibrationDecision =
        // The honest count of comparison evidence actually present, the supplied minimums, and the EFFECTIVE
        // minimum `max(min, 2)` — the no-single-sample floor, so a lone sample never calibrates (L-D7/L-D12).
        let observed = List.length evidence.Samples
        let min = sampleCountValue thresholds.MinimumSamples
        let effectiveMin = max min 2
        let obs = agreementValue evidence.ObservedAgreement
        let req = agreementValue thresholds.MinimumAgreement

        // The precedence ladder (L-D1..L-D4), each inclusive gate realized as `not (x < y)`. Uncalibrated by
        // default — there is no input or fallback that yields Calibrated from insufficient evidence (L-D5).
        if observed = 0 then
            Uncalibrated NoCalibrationEvidence // L-D1
        elif observed < effectiveMin then
            Uncalibrated(TooFewSamples(SampleCount observed, SampleCount effectiveMin)) // L-D2 (carries the effective bar)
        elif obs < req then
            Uncalibrated(AgreementBelowThreshold(evidence.ObservedAgreement, thresholds.MinimumAgreement)) // L-D3
        else
            // Both gates pass: calibrated, naming exactly what cleared the gate — the no-hide rule (L-D4/L-D8).
            Calibrated
                { ObservedSamples = SampleCount observed
                  RequiredSamples = SampleCount effectiveMin
                  ObservedAgreement = evidence.ObservedAgreement
                  RequiredAgreement = thresholds.MinimumAgreement }

    let calibrationReason (decision: CalibrationDecision) : CalibrationReason option =
        match decision with
        | Uncalibrated r -> Some r // L-P1
        | Calibrated _ -> None

    let calibrationMetrics (decision: CalibrationDecision) : CalibrationMetrics option =
        match decision with
        | Calibrated m -> Some m // L-P2
        | Uncalibrated _ -> None

    let isCalibrated (decision: CalibrationDecision) : bool =
        match decision with
        | Calibrated _ -> true // L-P3
        | Uncalibrated _ -> false
