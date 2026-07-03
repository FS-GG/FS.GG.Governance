module FS.GG.Governance.Calibration.Tests.NecessaryNotSufficientTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Calibration
open FS.GG.Governance.Calibration.Model
open FS.GG.Governance.Calibration.Tests.Support

// User Story 3 (part) — necessary-not-sufficient + no-hide (SC-006, FR-008, L-D15). The FR-008 negatives are
// BY CONSTRUCTION, not fail-then-pass behavioral tests: that `CalibrationDecision` exposes no blocking action,
// no Severity, no effective severity, no enforcement verdict, and no F039 eligibility is proven by an
// exhaustive pattern match that COMPILES (every value is `Uncalibrated _` or `Calibrated _` and nothing more)
// plus the SurfaceDrift + reference-graph guard (SurfaceDriftTests), which together pin that no such member or
// dependency exists. The genuine value-level assertion here is the no-hide rule (L-D8/L-D9), which also pins
// FR-001 at the value level: every Calibrated names its metrics, every Uncalibrated names its reason, and a
// Calibrated-without-metrics value is unconstructible.

/// The whole vocabulary of a decision: exactly two cases, each carrying only its named payload. This function
/// COMPILING is the proof that the type is the calibration-maturity verdict and nothing more — there is no
/// blocking action, severity, or enforcement member to destructure (L-D15, by construction).
let private describe (decision: CalibrationDecision) : string =
    match decision with
    | Uncalibrated NoCalibrationEvidence -> "no-evidence"
    | Uncalibrated(TooFewSamples _) -> "too-few"
    | Uncalibrated(AgreementBelowThreshold _) -> "agreement-below"
    | Calibrated _ -> "calibrated"

[<Tests>]
let tests =
    testList
        "NecessaryNotSufficient"
        [ test "a decision is exactly one of the two named cases — nothing more (L-D15, by construction)" {
              // The exhaustive match above compiles only because the two cases are the whole surface; here we
              // just confirm it evaluates for both outcomes.
              Expect.equal (describe (Calibration.decide T (evidence [] 95))) "no-evidence" "uncalibrated case"
              Expect.equal (describe (Calibration.decide T (evidenceOf 5 95))) "calibrated" "calibrated case"
          }

          test "no-hide: a Calibrated decision always names its metrics; an Uncalibrated always names its reason (L-D8/L-D9, FR-001)" {
              for (t, e, _) in workedExamples do
                  match Calibration.decide t e with
                  | Calibrated m ->
                      Expect.equal (Calibration.calibrationMetrics (Calibrated m)) (Some m) "Calibrated ⇒ metrics named"
                      Expect.isNone (Calibration.calibrationReason (Calibrated m)) "Calibrated ⇒ no reason"
                  | Uncalibrated r ->
                      Expect.equal (Calibration.calibrationReason (Uncalibrated r)) (Some r) "Uncalibrated ⇒ reason named"
                      Expect.isNone (Calibration.calibrationMetrics (Uncalibrated r)) "Uncalibrated ⇒ no metrics"
          }

          testPropertyWithConfig fscheckConfig "every decide result names exactly its own payload (L-D8/L-D9)"
          <| fun (t: CalibrationThresholds) (e: CalibrationEvidence) ->
              match Calibration.decide t e with
              | Calibrated m ->
                  Calibration.calibrationMetrics (Calibrated m) = Some m
                  && Calibration.calibrationReason (Calibrated m) = None
                  && Calibration.isCalibrated (Calibrated m)
              | Uncalibrated r ->
                  Calibration.calibrationReason (Uncalibrated r) = Some r
                  && Calibration.calibrationMetrics (Uncalibrated r) = None
                  && not (Calibration.isCalibrated (Uncalibrated r)) ]
