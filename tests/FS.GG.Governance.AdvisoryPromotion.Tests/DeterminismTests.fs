module FS.GG.Governance.AdvisoryPromotion.Tests.DeterminismTests

open System.IO
open Expecto
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.AdvisoryPromotion
open FS.GG.Governance.AdvisoryPromotion.Model
open FS.GG.Governance.AdvisoryPromotion.Tests.Support

// US3 (SC-005, L-D12): `decide facts = decide facts` always; the decision is byte-for-byte / structurally
// identical when computed in different working directories, at different times, and with unrelated filesystem
// state changed between calls; no model invoked, no clock/filesystem/git/environment/network read, no bytes
// hashed, nothing persisted. Mirrors the EvidenceReuse/ReviewRecord purity-test precedent.

let private sample () : PromotionFacts =
    facts (Some(EvidenceRef "e")) 5 3 (Some(SignOff "u"))

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "decide is byte-identical across cwd change and unrelated filesystem mutation" {
              let originalCwd = Directory.GetCurrentDirectory()
              let tmp = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())
              Directory.CreateDirectory tmp |> ignore

              try
                  let d1 = AdvisoryPromotion.decide (sample ())

                  // Change the working directory and touch an unrelated file between the two computations.
                  Directory.SetCurrentDirectory tmp
                  File.WriteAllText(Path.Combine(tmp, "noise.txt"), "unrelated state")

                  let d2 = AdvisoryPromotion.decide (sample ())

                  Expect.equal d2 d1 "the decision is structurally identical regardless of cwd/filesystem state"
              finally
                  Directory.SetCurrentDirectory originalCwd

                  try
                      Directory.Delete(tmp, true)
                  with _ ->
                      ()
          }

          test "every worked example re-decides identically on a second call" {
              for (f, _) in workedExamples do
                  Expect.equal (AdvisoryPromotion.decide f) (AdvisoryPromotion.decide f) "decide is a pure function of its facts"
          }

          testPropertyWithConfig fscheckConfig "decide is a pure function of its facts (SC-005, L-D12)" (fun (f: PromotionFacts) ->
              AdvisoryPromotion.decide f = AdvisoryPromotion.decide f) ]
