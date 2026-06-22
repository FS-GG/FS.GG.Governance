module FS.GG.Governance.AdvisoryPromotion.Tests.EligibilityTests

open Expecto
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.AdvisoryPromotion
open FS.GG.Governance.AdvisoryPromotion.Model
open FS.GG.Governance.AdvisoryPromotion.Tests.Support

// US2 (SC-002): when ≥1 of the three permitted bases is satisfied, `decide` is `EligibleToBlock` naming EVERY
// satisfied basis (the no-hide rule) in the fixed order *DeterministicBackingEvidence, RepeatedReviewConfidence,
// HumanSignOff* — one basis names just it; two or three name all of them.

[<Tests>]
let tests =
    testList
        "Eligibility"
        [ test "one basis — backing evidence ⇒ EligibleToBlock (DeterministicBackingEvidence, []) (L-D5/L-D6)" {
              Expect.equal
                  (AdvisoryPromotion.decide (facts (Some(EvidenceRef "e")) 0 3 None))
                  (EligibleToBlock(DeterministicBackingEvidence, []))
                  "evidence alone names just the deterministic-backing basis"
          }

          test "one basis — repeated review at the floor ⇒ EligibleToBlock (RepeatedReviewConfidence, [])" {
              Expect.equal
                  (AdvisoryPromotion.decide (facts None 3 3 None))
                  (EligibleToBlock(RepeatedReviewConfidence, []))
                  "c = t = 3 (>= 2) names just the repeated-review basis"
          }

          test "one basis — human sign-off ⇒ EligibleToBlock (HumanSignOff, [])" {
              Expect.equal
                  (AdvisoryPromotion.decide (facts None 0 3 (Some(SignOff "u"))))
                  (EligibleToBlock(HumanSignOff, []))
                  "sign-off alone names just the human-sign-off basis"
          }

          test "all three bases ⇒ EligibleToBlock naming all in fixed order (the no-hide rule, L-D6)" {
              Expect.equal
                  (AdvisoryPromotion.decide (facts (Some(EvidenceRef "e")) 5 3 (Some(SignOff "u"))))
                  (EligibleToBlock(DeterministicBackingEvidence, [ RepeatedReviewConfidence; HumanSignOff ]))
                  "all three satisfied ⇒ all named in the fixed order"
          }

          test "two bases — evidence + sign-off ⇒ both named in fixed order (no RepeatedReviewConfidence)" {
              Expect.equal
                  (AdvisoryPromotion.decide (facts (Some(EvidenceRef "e")) 0 3 (Some(SignOff "u"))))
                  (EligibleToBlock(DeterministicBackingEvidence, [ HumanSignOff ]))
                  "evidence + sign-off, no confidence ⇒ DeterministicBackingEvidence then HumanSignOff"
          }

          test "two bases — evidence + confidence ⇒ both named in fixed order" {
              Expect.equal
                  (AdvisoryPromotion.decide (facts (Some(EvidenceRef "e")) 3 3 None))
                  (EligibleToBlock(DeterministicBackingEvidence, [ RepeatedReviewConfidence ]))
                  "evidence + confidence ⇒ DeterministicBackingEvidence then RepeatedReviewConfidence"
          }

          test "two bases — confidence + sign-off ⇒ both named in fixed order" {
              Expect.equal
                  (AdvisoryPromotion.decide (facts None 4 3 (Some(SignOff "u"))))
                  (EligibleToBlock(RepeatedReviewConfidence, [ HumanSignOff ]))
                  "confidence + sign-off ⇒ RepeatedReviewConfidence then HumanSignOff"
          }

          test "worked examples — every contract row decides exactly as pinned" {
              for (f, expected) in workedExamples do
                  Expect.equal (AdvisoryPromotion.decide f) expected (sprintf "worked example mismatch for %A" f)
          }

          testPropertyWithConfig fscheckConfig "all-named — satisfiedBases (decide f) equals the bases f satisfies, in fixed order (SC-002, L-D6)" (fun (f: PromotionFacts) ->
              AdvisoryPromotion.satisfiedBases (AdvisoryPromotion.decide f) = expectedBases f)

          testPropertyWithConfig fscheckConfig "eligible iff a basis — decide f is EligibleToBlock iff f satisfies ≥1 basis (L-D5)" (fun (f: PromotionFacts) ->
              let eligible =
                  match AdvisoryPromotion.decide f with
                  | EligibleToBlock _ -> true
                  | StaysAdvisory _ -> false

              eligible = not (List.isEmpty (expectedBases f))) ]
