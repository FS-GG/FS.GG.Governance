namespace FS.GG.Governance.CostBudget

open System
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CostBudget.Model

// The F25 budget + budgeted-cache-decision core (US1/US2). PURE and TOTAL: no I/O, no clock, no git, never
// throws. `decide` folds the F041 verdict VERBATIM with the budget — it recomputes no freshness or
// agent-review match (D4) — and sorts the report by GateId ordinal so it is byte-identical regardless of
// candidate order (SC-008). No access modifiers — the surface is Budget.fsi (Principle II); the projection
// tables and sort comparator below are hidden by their absence from the .fsi.

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Budget =

    // ── the two monotone D1 projection tables (hidden — absent from Budget.fsi) ──
    // Both are monotone in their lever's strictness/protectiveness over the ordered `Cost` DU
    // (Cheap < Medium < High < Exhaustive). The run's ceiling is their `min`, so EITHER lever can restrict.

    /// The strictness lever: a stricter profile admits a higher recompute ceiling. `Light` floors to `Cheap`
    /// (no expensive budget); `Release` admits the full matrix.
    let profileCeiling (profile: Profile) : Cost =
        match profile with
        | Light -> Cheap
        | Standard -> Medium
        | Strict -> High
        | Profile.Release -> Exhaustive

    /// The protectiveness lever: a more protective run mode admits a higher recompute ceiling. The inner-loop
    /// modes (`Sandbox`/`Inner`) floor to `Cheap`; `Release` admits the full matrix.
    let modeCeiling (mode: RunMode) : Cost =
        match mode with
        | Sandbox -> Cheap
        | Inner -> Cheap
        | Focused -> Medium
        | Verify -> High
        | Gate -> High
        | RunMode.Release -> Exhaustive

    /// Skip in an inner-loop mode (the inner loop deliberately won't run it); defer in a boundary mode (it
    /// must eventually run at a stricter boundary) — research D2.
    let deferralClass (mode: RunMode) : DeferralClass =
        match mode with
        | Sandbox
        | Inner
        | Focused -> Skipped
        | Verify
        | Gate
        | RunMode.Release -> Deferred

    /// Total ordering of entries by `GateId` ordinal with a structural tiebreak — so the report is
    /// byte-identical regardless of supply order (SC-008), and two entries sharing a `GateId` are still
    /// totally ordered.
    let byGate (a: CacheDecisionEntry) (b: CacheDecisionEntry) : int =
        let c = String.CompareOrdinal(gateIdValue a.Gate, gateIdValue b.Gate)
        if c <> 0 then c else compare a b

    // ── public surface ──

    let budgetFor (profile: Profile) (mode: RunMode) : CostBudget =
        { Ceiling = min (profileCeiling profile) (modeCeiling mode) }

    let fits (budget: CostBudget) (cost: Cost) : bool =
        costRank cost <= costRank budget.Ceiling

    let decide (budget: CostBudget) (mode: RunMode) (candidates: CandidateCost list) : CacheDecisionReport =
        candidates
        |> List.map (fun c ->
            let decision =
                match c.Verdict with
                | Reusable evidence -> Reuse evidence
                | MustRecompute cause ->
                    if fits budget c.Cost then
                        Recompute cause
                    else
                        OverBudget
                            { Gate = c.Gate
                              Cost = c.Cost
                              Ceiling = budget.Ceiling
                              Class = deferralClass mode
                              Cause = cause }

            { Gate = c.Gate
              Cost = c.Cost
              Review = c.Review
              Decision = decision })
        |> List.sortWith byGate
        |> CacheDecisionReport

    let entries (report: CacheDecisionReport) : CacheDecisionEntry list =
        let (CacheDecisionReport xs) = report
        xs

    let recomputeGates (report: CacheDecisionReport) : GateId list =
        entries report
        |> List.choose (fun e ->
            match e.Decision with
            | Recompute _ -> Some e.Gate
            | _ -> None)

    let reuseGates (report: CacheDecisionReport) : GateId list =
        entries report
        |> List.choose (fun e ->
            match e.Decision with
            | Reuse _ -> Some e.Gate
            | _ -> None)

    let overBudget (report: CacheDecisionReport) : (GateId * BudgetReason) list =
        entries report
        |> List.choose (fun e ->
            match e.Decision with
            | OverBudget r -> Some(e.Gate, r)
            | _ -> None)
