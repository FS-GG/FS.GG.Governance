// Advisory-promotion types for the advisory-to-blocking promotion-gate decision core (F039). The public
// surface is fixed by Model.fsi (Principle II); no top-level binding here carries an access modifier. These
// are the supplied levers and named outcomes that `AdvisoryPromotion.decide` works over; they reuse the F030
// `EvidenceRef` token verbatim rather than redefining it (FR-009).

namespace FS.GG.Governance.AdvisoryPromotion

open FS.GG.Governance.EvidenceReuse.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type PromotionBasis =
        | DeterministicBackingEvidence
        | RepeatedReviewConfidence
        | HumanSignOff

    type ConfirmationCount = ConfirmationCount of int

    type ConfidenceThreshold = ConfidenceThreshold of int

    type SignOff = SignOff of string

    type AdvisoryReason =
        | NoPermittedBasis
        | ConfidenceBelowThreshold of ConfirmationCount * ConfidenceThreshold

    type PromotionFacts =
        { BackingEvidence: EvidenceRef option
          Confirmations: ConfirmationCount
          ConfidenceThreshold: ConfidenceThreshold
          SignOff: SignOff option }

    type PromotionDecision =
        | StaysAdvisory of AdvisoryReason
        | EligibleToBlock of PromotionBasis * PromotionBasis list
