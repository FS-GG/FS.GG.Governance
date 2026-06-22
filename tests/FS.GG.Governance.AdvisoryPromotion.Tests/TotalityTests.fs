module FS.GG.Governance.AdvisoryPromotion.Tests.TotalityTests

open System
open Expecto
open FS.GG.Governance.AdvisoryPromotion
open FS.GG.Governance.AdvisoryPromotion.Model
open FS.GG.Governance.AdvisoryPromotion.Tests.Support

// US3 (SC-004, L-D11): `decide` returns a `PromotionDecision` and never throws for every `PromotionFacts` —
// any `EvidenceRef option`, any `ConfirmationCount`/`ConfidenceThreshold` int (incl. 0, negatives, and
// Int32.Min/MaxValue), any `SignOff option`. Every combination is an ordinary named decision.

/// A decision is well-formed: it is one of the two named outcomes (forcing evaluation; never throws).
let private wellFormed (d: PromotionDecision) =
    match d with
    | StaysAdvisory _ -> true
    | EligibleToBlock(_, _) -> true

[<Tests>]
let tests =
    testList
        "Totality"
        [ test "the degenerate-extreme cross-product all decide without throwing" {
              let evidences = [ None; Some(evidence "") ]
              let signs = [ None; Some(signOff "") ]
              let ints = [ Int32.MinValue; -1; 0; 1; 2; Int32.MaxValue ]

              for e in evidences do
                  for s in signs do
                      for c in ints do
                          for t in ints do
                              let d = AdvisoryPromotion.decide (facts e c t s)
                              Expect.isTrue (wellFormed d) (sprintf "decide threw or mis-shaped for e=%A c=%d t=%d s=%A" e c t s)
          }

          testPropertyWithConfig fscheckConfig "decide returns a decision and never throws over the full cross-product (SC-004, L-D11)" (fun (f: PromotionFacts) ->
              wellFormed (AdvisoryPromotion.decide f)) ]
