// Curated public signature contract for the advisory-promotion types (F039).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any Model.fs body exists
// (Principle I). These are the supplied levers and named outcomes the `AdvisoryPromotion.decide` operation
// works over. They REUSE the F030 `EvidenceRef` token verbatim — opened from
// `FS.GG.Governance.EvidenceReuse.Model`, never redefined (FR-009). The only new vocabulary is the closed
// three-value permitted-basis union, the two confidence newtypes, the opaque sign-off marker, the advisory
// reason, and the facts/decision pair — the minimal vocabulary the single-sample-noise guardrail needs.

namespace FS.GG.Governance.AdvisoryPromotion

open FS.GG.Governance.EvidenceReuse.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The CLOSED three-value vocabulary of permitted promotion bases — the ONLY three justifications by which
    /// an agent-reviewed finding may become eligible to block (FR-002). Exactly these three cases, no fourth:
    /// the model's own self-reported confidence is NOT a case (FR-002, SC-001).
    type PromotionBasis =
        /// A deterministic check independently corroborates the finding (presence of a supplied `EvidenceRef`).
        | DeterministicBackingEvidence
        /// The same verdict was reproduced across enough independent reviews (the inclusive `c >= t && c >= 2`
        /// floor — a lone review never clears it).
        | RepeatedReviewConfidence
        /// A human explicitly signed off on the finding (presence of a supplied `SignOff`).
        | HumanSignOff

    /// The independent repeated-review confirmations supplied. A count of `0` (or absent, modelled as
    /// `ConfirmationCount 0`) means no repeated review. Supplied as data — never parsed or sourced by this
    /// core; negative/degenerate values are total inputs (FR-004).
    type ConfirmationCount = ConfirmationCount of int

    /// The supplied policy threshold the confirmation count must clear. Supplied as data — never parsed or
    /// sourced by this core; negative/degenerate values are total inputs (FR-004).
    type ConfidenceThreshold = ConfidenceThreshold of int

    /// The opaque human-sign-off marker. Presence (`Some (SignOff _)`) satisfies the human-sign-off basis;
    /// never parsed, validated, or dereferenced — an empty string is a literal value (FR-002, research D3).
    type SignOff = SignOff of string

    /// Why a finding stays advisory — the no-hide attribution carried by a *stays advisory* outcome, always
    /// present (FR-005, Principle VI). `NoPermittedBasis`: nothing was attempted. `ConfidenceBelowThreshold`:
    /// a repeated review was attempted but fell short — it carries the supplied count and threshold so the
    /// hold is attributable.
    type AdvisoryReason =
        | NoPermittedBasis
        | ConfidenceBelowThreshold of ConfirmationCount * ConfidenceThreshold

    /// The supplied levers — the SOLE input to `decide` (research D2/D4). The finding under decision is NOT a
    /// field (the caller associates the result with its finding); the finding's verdict is NOT a field — the
    /// verdict is an opaque fact this core never produces, interprets, or re-scores (FR-007).
    type PromotionFacts =
        { BackingEvidence: EvidenceRef option
          Confirmations: ConfirmationCount
          ConfidenceThreshold: ConfidenceThreshold
          SignOff: SignOff option }

    /// The two-outcome gate verdict (FR-001, research D6). `StaysAdvisory` carries its no-hide reason;
    /// `EligibleToBlock` names EVERY satisfied basis as a head + tail, in the fixed order
    /// *DeterministicBackingEvidence, RepeatedReviewConfidence, HumanSignOff* (SC-002). The head + tail
    /// encoding makes an empty-basis promotion UNREPRESENTABLE (FR-001). An `EligibleToBlock` value is
    /// necessary-not-sufficient: it carries no blocking action and asserts no calibration claim (FR-008).
    type PromotionDecision =
        | StaysAdvisory of AdvisoryReason
        | EligibleToBlock of PromotionBasis * PromotionBasis list
