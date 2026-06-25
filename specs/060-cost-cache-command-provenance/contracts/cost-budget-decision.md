# Contract: Cost Budget + Budgeted Cache Decision (`FS.GG.Governance.CostBudget`)

Pure leaf core. **No I/O, no clock, no git, no environment.** Total over every input. Byte-identical for
identical inputs; order-independent (reordering candidates never changes the report). FR-001, FR-002, FR-004,
FR-005, FR-006, FR-011, FR-014.

## `Model.fsi`

```fsharp
namespace FS.GG.Governance.CostBudget

open FS.GG.Governance.Config.Model              // Cost
open FS.GG.Governance.Enforcement.Enforcement   // Profile, RunMode
open FS.GG.Governance.Gates.Model               // GateId
open FS.GG.Governance.EvidenceReuse.Model        // EvidenceRef, RecomputeCause
open FS.GG.Governance.CacheEligibility.Model      // CacheEligibilityVerdict
open FS.GG.Governance.AgentReviewKey.Model         // CacheKey

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

    /// The named reason an over-budget gate is not run — never a pass (FR-003).
    type BudgetReason =
        { Gate: GateId
          Cost: Cost
          Ceiling: Cost
          Class: DeferralClass }

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
```

## `Budget.fsi`

```fsharp
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

    val entries: report: CacheDecisionReport -> CacheDecisionEntry list
```

## Behaviour table (the matrices the tests assert)

| F041 verdict | cost vs ceiling | `RunMode` class | `CacheDecision` | budget charge |
|--------------|-----------------|-----------------|-----------------|---------------|
| `Reusable ref` | (not consulted) | — | `Reuse ref` | none |
| `MustRecompute cause` | `cost <= ceiling` | — | `Recompute cause` | its cost |
| `MustRecompute _` | `cost > ceiling` | Verify/Gate/Release | `OverBudget { Class = Deferred }` | none |
| `MustRecompute _` | `cost > ceiling` | Sandbox/Inner/Focused | `OverBudget { Class = Skipped }` | none |

Edge cases (spec): budget exactly met → `cost == ceiling` is `Recompute` (inclusive); all reusable → budget
untouched; budget zero/disabled → `Cheap` ceiling, every `Medium+` `MustRecompute` is `OverBudget` while cheap
recompute and all reuse proceed; cost-tier change alone (freshness unchanged) → still `Reusable` (verdict
arrives from F041, which ignores cost); determinism under reordering → `decide` re-sorts by `GateId`.
