// Curated public signature contract for the cost-budget + budgeted-cache-decision types (F25).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Model.fs carries NO `private`/`internal`/`public` modifiers on top-level bindings — visibility is
// presence/absence here.
//
// These types REUSE the upstream vocabulary verbatim, never redefined (D4): F014 `Cost`, F018 `GateId`, F030
// `EvidenceRef`/`RecomputeCause`, F041 `CacheEligibilityVerdict`, F036 `CacheKey`. The only new vocabulary is
// the cost dimension: the budget ceiling, the per-gate candidate, the single budgeted cache decision, and its
// report. No reused type gains or loses a field; `Cost` stays excluded from the freshness key.

namespace FS.GG.Governance.CostBudget

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.AgentReviewKey.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Model =

    /// The maximum Cost tier a must-recompute gate may recompute this run (research D1). `Cheap` is the
    /// floor (zero expensive budget — cheap recompute always proceeds); `Exhaustive` admits the full matrix.
    type CostBudget = { Ceiling: Cost }

    /// Ordinary gate vs an agent-reviewed gate carrying its F036 cache identity. The mark affects ONLY
    /// enforcement (agent-reviewed stays advisory, FR-010); it never changes the budget arithmetic.
    type AgentReviewMark =
        | Deterministic
        | AgentReviewed of CacheKey

    /// One gate's already-sensed cost inputs. `Verdict` is the F041 verdict VERBATIM — `decide` folds it
    /// with the budget, never recomputing a freshness or agent-review match (FR-005, FR-006).
    type CandidateCost =
        { Gate: GateId
          Cost: Cost
          Verdict: CacheEligibilityVerdict
          Review: AgentReviewMark }

    /// Skip (inner-loop mode) vs Defer (boundary mode) for an over-budget gate (research D2).
    type DeferralClass =
        | Skipped
        | Deferred

    /// The named reason an over-budget gate is not run — never a pass (FR-003). `Cause` retains the F030
    /// `RecomputeCause` that made the gate `MustRecompute` so `Findings.cacheFindings` can emit a `Stale`
    /// finding for a deferred/skipped gate whose freshness dimension changed (data-model §Recorded
    /// micro-decisions: "the entry retains enough to emit the Stale finding"). The `cost-budget.json`
    /// `overBudget` decision shape stays `{ class, ceiling, reason }` — `Cause` is consumed only by findings.
    type BudgetReason =
        { Gate: GateId
          Cost: Cost
          Ceiling: Cost
          Class: DeferralClass
          Cause: RecomputeCause }

    /// The single budgeted cache decision per gate (FR-004).
    ///   • `Reuse`      — verdict was `Reusable`; charges NOTHING against the budget.
    ///   • `Recompute`  — `MustRecompute` and cost fits the ceiling; charges its cost (carries the cause).
    ///   • `OverBudget` — `MustRecompute` but cost exceeds the ceiling; Skipped | Deferred with a reason.
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
