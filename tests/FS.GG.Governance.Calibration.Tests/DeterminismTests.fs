module FS.GG.Governance.Calibration.Tests.DeterminismTests

open System.IO
open Expecto
open FS.GG.Governance.Calibration
open FS.GG.Governance.Calibration.Model
open FS.GG.Governance.Calibration.Tests.Support

// User Story 3 (part) — determinism / purity (SC-005, L-D14): `decide t e = decide t e` always; the decision
// is structurally identical when computed in different working directories, at different times, and with
// unrelated filesystem state changed between calls; no model invoked, no human consulted, no review run, no
// clock/filesystem/git/environment/network read, no bytes hashed, nothing persisted. Mirrors the
// EvidenceReuse/ReviewRecord/AdvisoryPromotion purity-test precedent.

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "decide is structurally identical across cwd change and unrelated filesystem mutation" {
              let originalCwd = Directory.GetCurrentDirectory()
              let tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
              Directory.CreateDirectory tmp |> ignore

              try
                  let d1 = Calibration.decide T (evidenceOf 5 95)

                  // Change the working directory and touch an unrelated file between the two computations.
                  Directory.SetCurrentDirectory tmp
                  File.WriteAllText(Path.Combine(tmp, "noise.txt"), "unrelated state")

                  let d2 = Calibration.decide T (evidenceOf 5 95)

                  Expect.equal d2 d1 "the decision is structurally identical regardless of cwd/filesystem state"
              finally
                  Directory.SetCurrentDirectory originalCwd

                  try
                      Directory.Delete(tmp, true)
                  with _ ->
                      ()
          }

          test "every worked example re-decides identically on a second call" {
              for (t, e, _) in workedExamples do
                  Expect.equal (Calibration.decide t e) (Calibration.decide t e) "decide is a pure function of its inputs"
          }

          testPropertyWithConfig fscheckConfig "decide is a pure function of its inputs (SC-005, L-D14)"
          <| fun (t: CalibrationThresholds) (e: CalibrationEvidence) -> Calibration.decide t e = Calibration.decide t e ]
