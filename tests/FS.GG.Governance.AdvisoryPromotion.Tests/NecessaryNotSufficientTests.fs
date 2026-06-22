module FS.GG.Governance.AdvisoryPromotion.Tests.NecessaryNotSufficientTests

open Expecto
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.AdvisoryPromotion
open FS.GG.Governance.AdvisoryPromotion.Model
open FS.GG.Governance.AdvisoryPromotion.Tests.Support

// US3 (SC-006, L-D13): an `EligibleToBlock` decision carries ONLY the satisfied `PromotionBasis` set — no
// blocking action, no `Severity`, no enforcement verdict, no calibration claim. The type is the eligibility
// verdict and nothing more (necessary, not sufficient — calibration is the sixth row). This is a guarantee by
// CONSTRUCTION: `PromotionDecision` has exactly two cases and `EligibleToBlock` carries exactly
// `PromotionBasis * PromotionBasis list`. We pin it by exhaustive pattern match — the payload is bases only.

[<Tests>]
let tests =
    testList
        "NecessaryNotSufficient"
        [ test "EligibleToBlock carries only its PromotionBasis set — no blocking action / severity / verdict" {
              // Exhaustive match: an eligible decision exposes exactly a head basis + tail of bases, nothing
              // else. There is no field that could carry a Severity, an enforcement verdict, or a calibration
              // claim — the type makes that unrepresentable.
              let d = AdvisoryPromotion.decide (facts (Some(EvidenceRef "e")) 5 3 (Some(SignOff "u")))

              match d with
              | EligibleToBlock(head, tail) ->
                  // Every element is a PromotionBasis — one of exactly three cases, none of which is a blocking
                  // action. Forcing the match proves the payload is bases-only.
                  for b in head :: tail do
                      match b with
                      | DeterministicBackingEvidence
                      | RepeatedReviewConfidence
                      | HumanSignOff -> ()
              | StaysAdvisory _ -> failtest "expected EligibleToBlock for the all-three facts"
          }

          test "the eligible payload is exactly the named bases — it asserts no boundary may be blocked" {
              // `satisfiedBases` is the WHOLE observable content of an eligible decision; there is no further
              // 'block' signal to read. Eligibility names which permitted bases authorized promotion, no more.
              let d = AdvisoryPromotion.decide (facts None 3 3 None)
              Expect.equal (AdvisoryPromotion.satisfiedBases d) [ RepeatedReviewConfidence ] "eligibility names only its bases — no blocking action attached"
          } ]
