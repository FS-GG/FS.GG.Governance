module FS.GG.Governance.Calibration.Tests.TotalityTests

open Expecto
open FsCheck
open FS.GG.Governance.Calibration
open FS.GG.Governance.Calibration.Model
open FS.GG.Governance.Calibration.Tests.Support

// User Story 3 (part) — totality (SC-004, L-D13). `decide` is defined for every `CalibrationThresholds` ×
// `CalibrationEvidence` (any sample list incl. [] and singletons, any AgreementLevel, any SampleCount incl.
// zero/negative/Int32 extremes), returns a decision, and never throws.

[<Tests>]
let tests =
    testList
        "Totality"
        [ testPropertyWithConfig fscheckConfig "decide returns a decision and never throws over the full cross-product"
          <| fun (t: CalibrationThresholds) (e: CalibrationEvidence) ->
              // Forcing the result proves no exception escapes; the match proves it is one of the two cases.
              match Calibration.decide t e with
              | Uncalibrated _
              | Calibrated _ -> true

          test "the named edge cases all yield ordinary decisions (no throw)" {
              // No evidence.
              Expect.equal (Calibration.decide T (evidence [] 95)) (Uncalibrated NoCalibrationEvidence) "no evidence"
              // One sample.
              Expect.equal
                  (Calibration.decide T (evidenceOf 1 100))
                  (Uncalibrated(TooFewSamples(SampleCount 1, SampleCount 3)))
                  "one sample"
              // Agreement below / at / above the threshold with the sample gate satisfied.
              Expect.isFalse (Calibration.isCalibrated (Calibration.decide T (evidenceOf 3 79))) "below"
              Expect.isTrue (Calibration.isCalibrated (Calibration.decide T (evidenceOf 3 80))) "at"
              Expect.isTrue (Calibration.isCalibrated (Calibration.decide T (evidenceOf 3 95))) "above"
          }

          test "degenerate / extreme integer thresholds are total" {
              // Negative minimum sample count: effectiveMin = max(min, 2) = 2, so 2 samples calibrate (agreement ok).
              Expect.isTrue
                  (Calibration.isCalibrated (Calibration.decide (thresholds -100 0) (evidenceOf 2 0)))
                  "negative min sample count ⇒ effectiveMin 2"
              // Int32 extremes do not throw.
              Expect.equal
                  (Calibration.decide (thresholds System.Int32.MaxValue System.Int32.MaxValue) (evidenceOf 3 0))
                  (Uncalibrated(TooFewSamples(SampleCount 3, SampleCount System.Int32.MaxValue)))
                  "Int32.MaxValue minimum ⇒ TooFewSamples, no overflow throw"
              Expect.equal
                  (Calibration.decide (thresholds System.Int32.MinValue System.Int32.MinValue) (evidenceOf 2 System.Int32.MinValue))
                  (Calibrated
                      { ObservedSamples = SampleCount 2
                        RequiredSamples = SampleCount 2
                        ObservedAgreement = AgreementLevel System.Int32.MinValue
                        RequiredAgreement = AgreementLevel System.Int32.MinValue })
                  "Int32.MinValue thresholds ⇒ effectiveMin 2, inclusive agreement"
          } ]
