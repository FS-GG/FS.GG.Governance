// Curated public signature contract for the advisory-promotion operations (F039).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// AdvisoryPromotion.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility
// is presence/absence here. The `confidenceMet` helper is ABSENT from this surface (private by omission).
//
// Design-first artifact: drafted and exercised in FSI (scripts/prelude.fsx) before any AdvisoryPromotion.fs
// body exists (Principle I). `decide` is PURE and TOTAL (FR-003, FR-006): defined for every input, never
// throwing, reading no clock/filesystem/git/environment/network, invoking no model/agent, hashing no bytes,
// running no review, making no cache-key/verdict-store/lookup/invalidation, building no review record,
// persisting nothing, and identical for identical input regardless of evaluation time, machine, process, or
// working directory. Its sole output is the typed `PromotionDecision`.

namespace FS.GG.Governance.AdvisoryPromotion

open FS.GG.Governance.AdvisoryPromotion.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module AdvisoryPromotion =

    /// Decide whether an agent-reviewed finding may be promoted from advisory to block-eligible. PURE and
    /// TOTAL (FR-003). Computes the satisfied-basis list in the fixed order *DeterministicBackingEvidence,
    /// RepeatedReviewConfidence, HumanSignOff*: `DeterministicBackingEvidence` when
    /// `facts.BackingEvidence = Some _`; `RepeatedReviewConfidence` when `c >= t && c >= 2` (the inclusive
    /// floor with the no-single-sample guard, where `ConfirmationCount c = facts.Confirmations`,
    /// `ConfidenceThreshold t = facts.ConfidenceThreshold`); `HumanSignOff` when `facts.SignOff = Some _`.
    /// Then: a non-empty list `b :: rest` ⇒ `EligibleToBlock (b, rest)` naming every satisfied basis (the
    /// no-hide rule, L-D1/L-D6); an empty list ⇒ `StaysAdvisory r` where `r = ConfidenceBelowThreshold
    /// (facts.Confirmations, facts.ConfidenceThreshold)` if `c >= 1` (a review was attempted but fell short,
    /// L-D2), else `NoPermittedBasis` (the bare default, L-D3). Advisory by default: with no basis satisfied
    /// the result is always `StaysAdvisory _` (L-D4) — the model's own self-confidence is not a basis (L-D9).
    /// An `EligibleToBlock` decision is necessary-not-sufficient (FR-008/L-D13). Reads no
    /// clock/filesystem/git/environment/network, invokes no model, hashes no bytes, runs no review, makes no
    /// cache/verdict-store/lookup/invalidation, builds no review record (FR-006).
    val decide: facts: PromotionFacts -> PromotionDecision

    /// The satisfied bases named by a decision (uniform projection). TOTAL. `[]` for `StaysAdvisory`
    /// (L-S1); the full non-empty `b :: rest` list, in fixed order, for `EligibleToBlock` (L-S2).
    val satisfiedBases: decision: PromotionDecision -> PromotionBasis list

    /// Unwrap a `SignOff` to its string (for audit, messages, tests). TOTAL (L-U1).
    val signOffValue: signOff: SignOff -> string

    /// Unwrap a `ConfirmationCount` to its int (for audit, messages, tests). TOTAL (L-U2).
    val confirmationValue: count: ConfirmationCount -> int

    /// Unwrap a `ConfidenceThreshold` to its int (for audit, messages, tests). TOTAL (L-U3).
    val thresholdValue: threshold: ConfidenceThreshold -> int
