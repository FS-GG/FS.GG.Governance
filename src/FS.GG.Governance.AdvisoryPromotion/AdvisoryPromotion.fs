// Advisory-promotion operations for the advisory-to-blocking promotion-gate decision core (F039). The public
// surface is fixed by AdvisoryPromotion.fsi (Principle II); no top-level binding here carries an access
// modifier (the `confidenceMet` helper is private by its absence from the .fsi). `decide` is pure, total, and
// deterministic (FR-003, FR-006): no clock, filesystem, git, environment, or network; no model invoked; no
// bytes hashed; no review run; no cache/verdict operation; identical facts always yield the identical
// decision. The decision is fixed by contracts/advisory-promotion-api.md and data-model.md.

namespace FS.GG.Governance.AdvisoryPromotion

open FS.GG.Governance.AdvisoryPromotion.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdvisoryPromotion =

    let signOffValue (signOff: SignOff) : string =
        let (SignOff s) = signOff
        s

    let confirmationValue (count: ConfirmationCount) : int =
        let (ConfirmationCount n) = count
        n

    let thresholdValue (threshold: ConfidenceThreshold) : int =
        let (ConfidenceThreshold n) = threshold
        n

    // The repeated-review confidence basis: satisfied EXACTLY at the inclusive `c >= t` floor AND only with
    // at least two independent reviews (`c >= 2`) — a lone review never clears it, even when `t = 1` (the
    // no-single-sample floor, L-D8). Private by omission from the .fsi.
    let confidenceMet (facts: PromotionFacts) : bool =
        let c = confirmationValue facts.Confirmations
        let t = thresholdValue facts.ConfidenceThreshold
        c >= t && c >= 2

    let decide (facts: PromotionFacts) : PromotionDecision =
        // Build the satisfied-basis list in the FIXED order, so the no-hide payload names every basis the
        // facts justify (L-D6). A list comprehension keeps the order by construction.
        let bases =
            [ if facts.BackingEvidence.IsSome then
                  DeterministicBackingEvidence
              if confidenceMet facts then
                  RepeatedReviewConfidence
              if facts.SignOff.IsSome then
                  HumanSignOff ]

        match bases with
        | b :: rest -> EligibleToBlock(b, rest) // ≥1 basis ⇒ eligible, naming all (L-D1)
        | [] ->
            // No basis: advisory by default. Attribute the hold — a review attempted but short of the floor
            // is ConfidenceBelowThreshold (L-D2); nothing attempted is NoPermittedBasis (L-D3).
            if confirmationValue facts.Confirmations >= 1 then
                StaysAdvisory(ConfidenceBelowThreshold(facts.Confirmations, facts.ConfidenceThreshold))
            else
                StaysAdvisory NoPermittedBasis

    let satisfiedBases (decision: PromotionDecision) : PromotionBasis list =
        match decision with
        | StaysAdvisory _ -> [] // L-S1
        | EligibleToBlock(b, rest) -> b :: rest // L-S2
