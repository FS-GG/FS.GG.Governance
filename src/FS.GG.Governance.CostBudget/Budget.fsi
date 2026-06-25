// Curated public signature contract for the cost-budget operations (F25).
//
// This .fsi is the SOLE declaration of the module's public surface (Constitution Principle II). The matching
// Budget.fs carries NO access modifiers; the `profileCeiling`/`modeCeiling` monotone projection tables and
// the ordinal sort comparator are ABSENT here (private by omission).
//
// All operations are PURE, TOTAL, and DETERMINISTIC (FR-001, FR-011): they read no clock/filesystem/git/
// environment, never throw, and yield byte-identical results for identical inputs regardless of candidate
// order. `decide` folds the F041 verdict VERBATIM with the budget — it recomputes no freshness or
// agent-review match (D4).

namespace FS.GG.Governance.CostBudget

open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.CostBudget.Model

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Budget =

    /// The (profile, mode) budget: `min` of the two monotone ceilings (research D1). TOTAL, deterministic.
    val budgetFor: profile: Profile -> mode: RunMode -> CostBudget

    /// `cost <= ceiling` over the ordered Cost DU — the inclusive boundary (edge "budget exactly met").
    val fits: budget: CostBudget -> cost: Cost -> bool

    /// Fold each candidate's F041 verdict with the budget into one CacheDecision (FR-004). PURE, TOTAL,
    /// ORDER-INDEPENDENT: entries sorted by GateId ordinal; identical inputs -> byte-identical report;
    /// reordering candidates never changes it (SC-008). Skip vs defer chosen by `mode` class (research D2).
    val decide: budget: CostBudget -> mode: RunMode -> candidates: CandidateCost list -> CacheDecisionReport

    /// The gates whose decision is `Recompute` — exactly what the host edge feeds to `ExecuteGates`.
    val recomputeGates: report: CacheDecisionReport -> GateId list

    /// The gates whose decision is `Reuse` (the cache payoff).
    val reuseGates: report: CacheDecisionReport -> GateId list

    /// The gates whose decision is `OverBudget` (skipped/deferred), each with its reason.
    val overBudget: report: CacheDecisionReport -> (GateId * BudgetReason) list

    /// The attributed entries of a report (unwrap), in GateId-ordinal order.
    val entries: report: CacheDecisionReport -> CacheDecisionEntry list
