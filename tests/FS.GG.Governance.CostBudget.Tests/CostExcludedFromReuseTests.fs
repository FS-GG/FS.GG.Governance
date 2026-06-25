module FS.GG.Governance.CostBudget.Tests.CostExcludedFromReuseTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Tests.Support

// US2 (T023): a candidate whose ONLY change vs recorded evidence is its Cost tier (every freshness dimension
// unchanged) is still Reusable ⇒ Reuse; only the budget accounting reflects the new cost. Cost is deliberately
// excluded from the freshness key (FR-006).

[<Tests>]
let tests =
    testList
        "CostExcludedFromReuse"
        [ test "the same freshness inputs at any cost tier are still Reusable ⇒ Reuse" {
              // The verdict is the SAME real Reusable for the exact-match inputs regardless of the cost we
              // attach to the candidate — cost never enters `CacheEligibility.evaluateGate`.
              let verdict = verdictFor baseInputs baseStore
              Expect.equal verdict (Reusable refA) "exact freshness match ⇒ Reusable, independent of cost"

              for cost in costs do
                  let report = Budget.decide (Budget.budgetFor Profile.Release RunMode.Release) RunMode.Release [ cc (gid "build" "tests") cost verdict ]
                  match (Budget.entries report |> List.head).Decision with
                  | Reuse _ -> ()
                  | other -> failtestf "cost %A should still Reuse, got %A" cost other
          }

          test "a reused gate is in reuseGates (not recomputeGates) at every cost tier" {
              let verdict = verdictFor baseInputs baseStore
              for cost in costs do
                  let report = Budget.decide (Budget.budgetFor Light Inner) Inner [ cc (gid "build" "tests") cost verdict ]
                  Expect.equal (Budget.reuseGates report) [ gid "build" "tests" ] (sprintf "reused at cost %A" cost)
                  Expect.equal (Budget.recomputeGates report) [] "reuse charges nothing even when the cost is huge"
          } ]
