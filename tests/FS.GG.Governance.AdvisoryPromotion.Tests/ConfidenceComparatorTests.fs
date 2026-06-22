module FS.GG.Governance.AdvisoryPromotion.Tests.ConfidenceComparatorTests

open Expecto
open FS.GG.Governance.AdvisoryPromotion
open FS.GG.Governance.AdvisoryPromotion.Model
open FS.GG.Governance.AdvisoryPromotion.Tests.Support

// SC-003 / FR-004 / L-D8: the repeated-review confidence basis (no other basis present) is satisfied EXACTLY
// when `c >= t && c >= 2`, verified across c < t, c = t, c > t; a lone review (c = 1) never satisfies it for
// any t (incl. t = 1 — the no-single-sample floor); c = 0 / absent never satisfies it; for t >= 2 the floor is
// invisible (the basis is exactly c >= t).

/// Whether `decide` named the repeated-review basis, with no other basis present (evidence/sign-off both None).
let private confidenceEligible (c: int) (t: int) =
    AdvisoryPromotion.satisfiedBases (AdvisoryPromotion.decide (facts None c t None))
    |> List.contains RepeatedReviewConfidence

[<Tests>]
let tests =
    testList
        "ConfidenceComparator"
        [ test "c < t never satisfies (below the threshold)" {
              for (c, t) in [ (0, 3); (1, 3); (2, 3); (1, 2); (4, 5) ] do
                  Expect.isFalse (confidenceEligible c t) (sprintf "c=%d < t=%d must not satisfy" c t)
          }

          test "c = t satisfies for t >= 2 (the inclusive floor)" {
              for t in [ 2; 3; 5; 10 ] do
                  Expect.isTrue (confidenceEligible t t) (sprintf "c = t = %d must satisfy" t)
          }

          test "c > t satisfies for t >= 2" {
              for (c, t) in [ (3, 2); (5, 3); (10, 4) ] do
                  Expect.isTrue (confidenceEligible c t) (sprintf "c=%d > t=%d must satisfy" c t)
          }

          test "a lone review (c = 1) never satisfies, for any t — incl. t = 1 (the no-single-sample floor)" {
              for t in [ -1; 0; 1; 2; 3 ] do
                  Expect.isFalse (confidenceEligible 1 t) (sprintf "lone review c=1 must not satisfy for t=%d" t)
              // The contract worked example: None / 1 / 1 / None ⇒ advisory, not eligible.
              Expect.equal
                  (AdvisoryPromotion.decide (facts None 1 1 None))
                  (StaysAdvisory(ConfidenceBelowThreshold(ConfirmationCount 1, ConfidenceThreshold 1)))
                  "1-of-1 holds advisory — the no-single-sample floor"
          }

          test "c = 0 / absent never satisfies, for any t" {
              for t in [ -1; 0; 1; 2; 3 ] do
                  Expect.isFalse (confidenceEligible 0 t) (sprintf "c=0 must not satisfy for t=%d" t)
          }

          test "for t <= 0 the c >= 2 guard still governs — c >= 2 satisfies, c < 2 does not" {
              // With a degenerate non-positive threshold, c >= t is trivially true; the floor is the guard.
              Expect.isTrue (confidenceEligible 2 0) "c=2, t=0 satisfies (c >= 2)"
              Expect.isTrue (confidenceEligible 2 -5) "c=2, t=-5 satisfies (c >= 2)"
              Expect.isFalse (confidenceEligible 1 0) "c=1, t=0 must not satisfy (lone review)"
              Expect.isFalse (confidenceEligible 1 -5) "c=1, t=-5 must not satisfy (lone review)"
          }

          testPropertyWithConfig fscheckConfig "RepeatedReviewConfidence ∈ satisfiedBases iff c >= t && c >= 2 (L-D8)" (fun (c: int) (t: int) ->
              confidenceEligible c t = (c >= t && c >= 2)) ]
