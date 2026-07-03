module FS.GG.Governance.AdvisoryPromotion.Tests.AdvisoryDefaultTests

open Expecto
open FsCheck
open FsCheck.FSharp
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.AdvisoryPromotion
open FS.GG.Governance.AdvisoryPromotion.Model
open FS.GG.Governance.AdvisoryPromotion.Tests.Support

// US1 (SC-001): with no permitted basis satisfied, `decide` is `StaysAdvisory` carrying its reason —
// `NoPermittedBasis` when nothing was attempted, `ConfidenceBelowThreshold` when a review fell short — and the
// model's own self-reported confidence never promotes. A bare agent-reviewed finding is NEVER eligible to block.

/// True when the facts satisfy none of the three permitted bases.
let private noBasis (f: PromotionFacts) =
    let (ConfirmationCount c) = f.Confirmations
    let (ConfidenceThreshold t) = f.ConfidenceThreshold
    f.BackingEvidence.IsNone && not (c >= t && c >= 2) && f.SignOff.IsNone

[<Tests>]
let tests =
    testList
        "AdvisoryDefault"
        [ test "bare default — no basis, zero confirmations ⇒ StaysAdvisory NoPermittedBasis (L-D3)" {
              Expect.equal (AdvisoryPromotion.decide (facts None 0 3 None)) (StaysAdvisory NoPermittedBasis) "bare finding holds advisory with NoPermittedBasis"
          }

          test "attempted-but-insufficient — 2 of 3, no other basis ⇒ ConfidenceBelowThreshold (L-D2)" {
              Expect.equal
                  (AdvisoryPromotion.decide (facts None 2 3 None))
                  (StaysAdvisory(ConfidenceBelowThreshold(ConfirmationCount 2, ConfidenceThreshold 3)))
                  "an insufficient count is not a basis; the hold names the supplied count and threshold"
          }

          test "self-confidence never promotes — there is no field by which model confidence enters (L-D9)" {
              // The only would-be justification is the model's own confidence (none of the three bases present);
              // it cannot enter `PromotionFacts`, so the finding stays advisory.
              for c in [ 0; 1; 5; 100 ] do
                  let d = AdvisoryPromotion.decide (facts None c (c + 1) None)

                  match d with
                  | StaysAdvisory _ -> ()
                  | EligibleToBlock _ -> failtestf "self-confidence promoted a finding: %A" d
          }

          testPropertyWithConfig fscheckConfig "advisory-by-default — no basis satisfied ⇒ never EligibleToBlock (SC-001, L-D4)" (fun (f: PromotionFacts) ->
              if noBasis f then
                  match AdvisoryPromotion.decide f with
                  | StaysAdvisory _ -> true
                  | EligibleToBlock _ -> false
              else
                  true) ]
