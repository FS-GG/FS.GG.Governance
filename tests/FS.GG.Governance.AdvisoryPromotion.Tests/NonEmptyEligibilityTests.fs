module FS.GG.Governance.AdvisoryPromotion.Tests.NonEmptyEligibilityTests

open Expecto
open FS.GG.Governance.AdvisoryPromotion
open FS.GG.Governance.AdvisoryPromotion.Model
open FS.GG.Governance.AdvisoryPromotion.Tests.Support

// FR-001 (L-D7/L-S1/L-S2): `EligibleToBlock (b, rest)` always carries the head `b`, so the named-basis set of
// any eligible decision is non-empty — the head + tail encoding makes an empty-basis promotion UNREPRESENTABLE
// (there is no constructor for it). Conversely, every `StaysAdvisory` projects to the empty basis list.

[<Tests>]
let tests =
    testList
        "NonEmptyEligibility"
        [ test "the head+tail encoding makes an empty-basis promotion unrepresentable (compile-time guarantee)" {
              // Documented by construction: `EligibleToBlock of PromotionBasis * PromotionBasis list` REQUIRES a
              // head basis. Building one always names at least the head; there is no `EligibleToBlock []` form.
              let eligible = EligibleToBlock(DeterministicBackingEvidence, [])
              Expect.equal (AdvisoryPromotion.satisfiedBases eligible) [ DeterministicBackingEvidence ] "an eligible decision always names at least its head basis"
          }

          testPropertyWithConfig fscheckConfig "every EligibleToBlock has satisfiedBases <> [] and every StaysAdvisory has satisfiedBases = [] (L-S1/L-S2)" (fun (f: PromotionFacts) ->
              match AdvisoryPromotion.decide f with
              | EligibleToBlock(_, _) as d -> not (List.isEmpty (AdvisoryPromotion.satisfiedBases d))
              | StaysAdvisory _ as d -> List.isEmpty (AdvisoryPromotion.satisfiedBases d)) ]
