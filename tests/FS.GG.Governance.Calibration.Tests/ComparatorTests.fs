module FS.GG.Governance.Calibration.Tests.ComparatorTests

open Expecto
open FsCheck
open FS.GG.Governance.Calibration
open FS.GG.Governance.Calibration.Model
open FS.GG.Governance.Calibration.Tests.Support

// The two-threshold comparator (SC-003, FR-004, L-D7): the calibration basis is satisfied EXACTLY when
// `observed >= max(min, 2) && obs >= req`, both gates inclusive, with the no-single-sample floor. Plus the
// reason precedence (NoCalibrationEvidence before TooFewSamples before AgreementBelowThreshold).

[<Tests>]
let tests =
    testList
        "Comparator"
        [ test "sample gate for min >= 2: below / at / above (agreement satisfied)" {
              // min = 3, agreement comfortably satisfied (req 80, obs 100).
              Expect.isFalse (Calibration.isCalibrated (Calibration.decide (thresholds 3 80) (evidenceOf 2 100))) "observed 2 < min 3"
              Expect.isTrue (Calibration.isCalibrated (Calibration.decide (thresholds 3 80) (evidenceOf 3 100))) "observed 3 = min 3"
              Expect.isTrue (Calibration.isCalibrated (Calibration.decide (thresholds 3 80) (evidenceOf 4 100))) "observed 4 > min 3"
          }

          test "a lone sample never calibrates, for any min (incl. min = 1)" {
              // observed = 1 ⇒ TooFewSamples (1, 2) because max(min, 2) >= 2 > 1, even when min = 1.
              for min in [ 1; 2; 3 ] do
                  Expect.equal
                      (Calibration.decide (thresholds min 0) (evidenceOf 1 100))
                      (Uncalibrated(TooFewSamples(SampleCount 1, SampleCount(max min 2))))
                      (sprintf "1 sample / min %d ⇒ TooFewSamples (1, %d)" min (max min 2))
          }

          test "observed = 0 is NoCalibrationEvidence, not TooFewSamples (precedence)" {
              Expect.equal
                  (Calibration.decide (thresholds 3 80) (evidence [] 100))
                  (Uncalibrated NoCalibrationEvidence)
                  "0 samples ⇒ NoCalibrationEvidence even though 0 < effectiveMin"
          }

          test "agreement gate: below / at / above (sample gate satisfied)" {
              // 3 samples (>= effectiveMin), req 80.
              Expect.isFalse (Calibration.isCalibrated (Calibration.decide (thresholds 3 80) (evidenceOf 3 79))) "obs 79 < req 80"
              Expect.isTrue (Calibration.isCalibrated (Calibration.decide (thresholds 3 80) (evidenceOf 3 80))) "obs 80 = req 80 (inclusive)"
              Expect.isTrue (Calibration.isCalibrated (Calibration.decide (thresholds 3 80) (evidenceOf 3 81))) "obs 81 > req 80"
          }

          testPropertyWithConfig fscheckConfig "isCalibrated ⟺ observed >= max(min,2) && obs >= req (L-D7)"
          <| fun (t: CalibrationThresholds) (e: CalibrationEvidence) ->
              let observed = List.length e.Samples
              let effectiveMin = max (Calibration.sampleCountValue t.MinimumSamples) 2
              let obs = Calibration.agreementValue e.ObservedAgreement
              let req = Calibration.agreementValue t.MinimumAgreement
              Calibration.isCalibrated (Calibration.decide t e) = (observed >= effectiveMin && obs >= req)

          testPropertyWithConfig fscheckConfig "reason precedence: NoCalibrationEvidence ▸ TooFewSamples ▸ AgreementBelowThreshold"
          <| fun (t: CalibrationThresholds) (e: CalibrationEvidence) ->
              let observed = List.length e.Samples
              let effectiveMin = max (Calibration.sampleCountValue t.MinimumSamples) 2
              let obs = Calibration.agreementValue e.ObservedAgreement
              let req = Calibration.agreementValue t.MinimumAgreement

              match Calibration.decide t e with
              | Uncalibrated NoCalibrationEvidence -> observed = 0
              | Uncalibrated (TooFewSamples (SampleCount o, SampleCount r)) ->
                  observed > 0 && observed < effectiveMin && o = observed && r = effectiveMin
              | Uncalibrated (AgreementBelowThreshold (o, r)) ->
                  observed >= effectiveMin && obs < req && o = e.ObservedAgreement && r = t.MinimumAgreement
              | Calibrated _ -> observed >= effectiveMin && obs >= req ]
