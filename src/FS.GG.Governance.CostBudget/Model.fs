namespace FS.GG.Governance.CostBudget

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.AgentReviewKey.Model

// The F25 cost-budget vocabulary (US1/US2/US3). Closed DUs + plain records; no access modifiers (Principle
// II — the surface is Model.fsi). Every upstream type is reused verbatim (D4): `Cost`, `GateId`,
// `EvidenceRef`/`RecomputeCause`, `CacheEligibilityVerdict`, `CacheKey`. The only new vocabulary is the cost
// dimension. `Cost` is deliberately NOT a freshness dimension; it only bounds recompute.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    type CostBudget = { Ceiling: Cost }

    type AgentReviewMark =
        | Deterministic
        | AgentReviewed of CacheKey

    type CandidateCost =
        { Gate: GateId
          Cost: Cost
          Verdict: CacheEligibilityVerdict
          Review: AgentReviewMark }

    type DeferralClass =
        | Skipped
        | Deferred

    type BudgetReason =
        { Gate: GateId
          Cost: Cost
          Ceiling: Cost
          Class: DeferralClass
          Cause: RecomputeCause }

    type CacheDecision =
        | Reuse of EvidenceRef
        | Recompute of RecomputeCause
        | OverBudget of BudgetReason

    type CacheDecisionEntry =
        { Gate: GateId
          Cost: Cost
          Review: AgentReviewMark
          Decision: CacheDecision }

    type CacheDecisionReport = CacheDecisionReport of CacheDecisionEntry list
