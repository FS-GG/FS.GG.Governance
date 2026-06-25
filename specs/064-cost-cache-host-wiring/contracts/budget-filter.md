# Contract — Budget Filter (selected gates → executed set)

**Cores consumed (verbatim):** `CostBudget.Budget.budgetFor`, `CostBudget.Budget.decide`.
**Insertion point:** each host's `executionPlan`/`tryExecute` (`Loop.fs`), after `CacheEligibility.evaluate`.

## Inputs
- `profile : Profile` — `verify`/`ship` `--profile` (default `Standard`).
- `mode : RunMode` — `verify` fixed `Verify`; `ship` `--mode` (default `Gate`).
- selected gates, each with `Cost`, its computed `CacheEligibilityVerdict`, and an `AgentReviewMark`.

## Transformation
```
budget   = budgetFor profile mode                       // Standard ⇒ Ceiling = Medium
candidates = selectedGates |> map (fun g -> { Gate=g.Id; Cost=g.Cost; Verdict=verdictOf g; Review=reviewOf g })
report   = decide budget mode candidates                // GateId-ordinal sorted, order-independent
```
Demotion of the classification produced by `executionPlan`:

| `decide` outcome | gate's execution disposition |
|---|---|
| `Reuse ref` | `ToReuse` — reused, **charges nothing** |
| `Recompute cause` | `ToExecute` — runs, charged |
| `OverBudget { Class = Deferred }` (boundary modes `Verify`/`Gate`/`Release`) | **demoted out of `ToExecute`** — deferred, named reason |
| `OverBudget { Class = Skipped }` (inner modes `Sandbox`/`Inner`/`Focused`) | **demoted out of `ToExecute`** — skipped, named reason |

## Guarantees (asserted by tests)
- An `OverBudget` gate is **absent from `ExecuteGates`** and **never** added to `applyExecution`'s passed set ⇒
  **never reported as passed** (FR-001, SC-002).
- Each `OverBudget` gate carries a `BudgetReason` naming the gate, its `Cost`, the exceeded `Ceiling`, and the
  `DeferralClass` (FR-001).
- A `Reuse` gate charges nothing; a `Recompute` gate is charged only when it runs (FR-003).
- Reordering candidates changes no decision (the report is `GateId`-ordinal sorted) (FR-006).
- `verify` uses `budgetFor profile Verify`; `ship` uses `budgetFor profile mode` at the merge boundary (FR-002).
