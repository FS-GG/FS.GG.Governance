module FS.GG.Governance.Calibration.Tests.UncalibratedDefaultTests

open Expecto
open FsCheck
open FS.GG.Governance.Calibration
open FS.GG.Governance.Calibration.Model
open FS.GG.Governance.Calibration.Tests.Support

// User Story 1 — an uncalibrated agent reviewer stays advisory by default (SC-001). With no calibration
// evidence, or with evidence short of a supplied threshold, `decide` is `Uncalibrated` carrying the reason that
// names the unmet requirement. The model's own self-reported confidence never calibrates (by construction —
// there is no field by which it could enter). Real literal values throughout; no model, no human, no I/O.

[<Tests>]
let tests =
    testList
        "UncalibratedDefault"
        [ test "no evidence ⇒ Uncalibrated NoCalibrationEvidence, any agreement / thresholds (L-D1)" {
              // Empty samples is the design's default: an unmeasured reviewer can never move beyond advisory.
              Expect.equal
                  (Calibration.decide (thresholds 3 80) (evidence [] 95))
                  (Uncalibrated NoCalibrationEvidence)
                  "empty samples ⇒ NoCalibrationEvidence"
              // Independent of agreement level and thresholds.
              Expect.equal
                  (Calibration.decide (thresholds 0 0) (evidence [] -50))
                  (Uncalibrated NoCalibrationEvidence)
                  "empty samples ⇒ NoCalibrationEvidence even with trivial thresholds"
          }

          test "too few samples ⇒ TooFewSamples carries the EFFECTIVE minimum (L-D2/L-D12)" {
              // 1 and 2 samples against a min of 3 ⇒ required = 3 (effectiveMin = max 3 2 = 3).
              Expect.equal
                  (Calibration.decide (thresholds 3 80) (evidenceOf 1 100))
                  (Uncalibrated(TooFewSamples(SampleCount 1, SampleCount 3)))
                  "1 sample / min 3 ⇒ TooFewSamples (1, 3)"
              Expect.equal
                  (Calibration.decide (thresholds 3 80) (evidenceOf 2 100))
                  (Uncalibrated(TooFewSamples(SampleCount 2, SampleCount 3)))
                  "2 samples / min 3 ⇒ TooFewSamples (2, 3)"
              // Degenerate min 1: the no-single-sample floor lifts the required to 2.
              Expect.equal
                  (Calibration.decide (thresholds 1 80) (evidenceOf 1 100))
                  (Uncalibrated(TooFewSamples(SampleCount 1, SampleCount 2)))
                  "1 sample / min 1 ⇒ TooFewSamples (1, 2) — no-single-sample floor"
          }

          test "enough samples but agreement below threshold ⇒ AgreementBelowThreshold (L-D3)" {
              Expect.equal
                  (Calibration.decide (thresholds 3 80) (evidenceOf 3 79))
                  (Uncalibrated(AgreementBelowThreshold(AgreementLevel 79, AgreementLevel 80)))
                  "3 samples / agreement 79 / req 80 ⇒ AgreementBelowThreshold (79, 80)"
              // The carried observed/required are the supplied evidence + threshold levels verbatim.
              Expect.equal
                  (Calibration.decide (thresholds 2 50) (evidenceOf 4 49))
                  (Uncalibrated(AgreementBelowThreshold(AgreementLevel 49, AgreementLevel 50)))
                  "4 samples / agreement 49 / req 50 ⇒ AgreementBelowThreshold (49, 50)"
          }

          test "self-confidence is not a calibration channel — ordinary evidence flows to Uncalibrated (L-D10, by construction)" {
              // By construction: `CalibrationEvidence` has no field for the model's own confidence; only
              // judge-vs-human `Samples` + the supplied `ObservedAgreement` populate it (the type enforces
              // this; the SurfaceDrift baseline guards it). This behavioral check only confirms that ordinary
              // evidence built solely from samples + ObservedAgreement flows through `decide` to an ordinary
              // Uncalibrated outcome — there is no self-assessment path to exercise.
              let e = evidence [ agreeingSample; disagreeingSample ] 60
              Expect.equal
                  (Calibration.decide (thresholds 3 80) e)
                  (Uncalibrated(TooFewSamples(SampleCount 2, SampleCount 3)))
                  "evidence from samples + ObservedAgreement only ⇒ ordinary Uncalibrated"
          }

          testPropertyWithConfig fscheckConfig "uncalibrated-by-default property (L-D5)"
          <| fun (t: CalibrationThresholds) (e: CalibrationEvidence) ->
              // Whenever evidence is absent or short of a gate, the result is Uncalibrated — never Calibrated.
              let observed = List.length e.Samples
              let (SampleCount min) = t.MinimumSamples
              let effectiveMin = max min 2
              let (AgreementLevel obs) = e.ObservedAgreement
              let (AgreementLevel req) = t.MinimumAgreement

              if observed = 0 || observed < effectiveMin || obs < req then
                  match Calibration.decide t e with
                  | Uncalibrated _ -> true
                  | Calibrated _ -> false
              else
                  true ]
