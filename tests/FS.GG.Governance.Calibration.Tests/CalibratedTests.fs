module FS.GG.Governance.Calibration.Tests.CalibratedTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.Calibration
open FS.GG.Governance.Calibration.Model
open FS.GG.Governance.Calibration.Tests.Support

// User Story 2 — a reviewer becomes calibrated on sufficient judge-vs-human evidence, and the evidence is
// named (SC-002). When the supplied thresholds are met — sample count >= max(min, 2) AND observed agreement >=
// req — `decide` is `Calibrated` naming the satisfied `CalibrationMetrics` (the no-hide rule), not a bare flag.
// The agreement comparison is inclusive (obs = req calibrates). Validates the calibrated branch of the single
// total `decide` (no separate US2 implementation).

[<Tests>]
let tests =
    testList
        "Calibrated"
        [ test "calibrated, metrics named (L-D4/L-D8)" {
              // The worked example: 5 samples / agreement 95 against T.
              let expected =
                  Calibrated
                      { ObservedSamples = SampleCount 5
                        RequiredSamples = SampleCount 3
                        ObservedAgreement = AgreementLevel 95
                        RequiredAgreement = AgreementLevel 80 }

              let decision = Calibration.decide T (evidenceOf 5 95)
              Expect.equal decision expected "5 samples / 95 ⇒ Calibrated naming the satisfied metrics"

              // The metrics are exposed via the projection, naming observed count + level and the effective
              // requirements — not a bare flag.
              Expect.equal
                  (Calibration.calibrationMetrics decision)
                  (Some
                      { ObservedSamples = SampleCount 5
                        RequiredSamples = SampleCount 3
                        ObservedAgreement = AgreementLevel 95
                        RequiredAgreement = AgreementLevel 80 })
                  "calibrationMetrics names the satisfied metrics"
          }

          test "inclusive at the agreement threshold (L-D7)" {
              // 3 samples / agreement exactly 80 against T ⇒ calibrated (meets-or-exceeds).
              let decision = Calibration.decide T (evidenceOf 3 80)

              Expect.equal
                  decision
                  (Calibrated
                      { ObservedSamples = SampleCount 3
                        RequiredSamples = SampleCount 3
                        ObservedAgreement = AgreementLevel 80
                        RequiredAgreement = AgreementLevel 80 })
                  "agreement exactly at the threshold calibrates"

              Expect.isTrue (Calibration.isCalibrated decision) "obs = req ⇒ calibrated"
          }

          test "effective-minimum honesty in metrics (L-D12)" {
              // Under a degenerate MinimumSamples = 1, a calibrated decision still reports RequiredSamples = 2
              // (the effective floor), never the understated supplied 1.
              let decision = Calibration.decide (thresholds 1 80) (evidenceOf 2 90)

              Expect.equal
                  (Calibration.calibrationMetrics decision)
                  (Some
                      { ObservedSamples = SampleCount 2
                        RequiredSamples = SampleCount 2
                        ObservedAgreement = AgreementLevel 90
                        RequiredAgreement = AgreementLevel 80 })
                  "RequiredSamples is the effective floor 2, not the supplied 1"
          }

          testPropertyWithConfig fscheckConfig "calibrated-iff-both-gates property, with exact metrics (L-D6)"
          <| fun (t: CalibrationThresholds) (e: CalibrationEvidence) ->
              let observed = List.length e.Samples
              let (SampleCount min) = t.MinimumSamples
              let effectiveMin = max min 2

              let decision = Calibration.decide t e

              // isCalibrated ⟺ both gates pass.
              (Calibration.isCalibrated decision = expectedCalibrated t e)
              && // and every Calibrated decision carries exactly these metrics.
              (match decision with
               | Calibrated m ->
                   m = { ObservedSamples = SampleCount observed
                         RequiredSamples = SampleCount effectiveMin
                         ObservedAgreement = e.ObservedAgreement
                         RequiredAgreement = t.MinimumAgreement }
               | Uncalibrated _ -> true) ]
