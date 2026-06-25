module FS.GG.Governance.CostBudget.Tests.DecideBudgetMatrixTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Tests.Support

// US1 (T018): `decide` with all-MustRecompute candidates spanning the four tiers across the grid —
// cost <= ceiling ⇒ Recompute; cost > ceiling ⇒ OverBudget (Deferred in boundary modes, Skipped in
// inner-loop modes), each reason naming gate/cost/ceiling; the cost == ceiling boundary is inclusive
// (FR-002, FR-003, SC-001, SC-003).

let private decisionFor (cost: Cost) (mode: RunMode) (profile: Profile) : CacheDecision =
    let budget = Budget.budgetFor profile mode
    let g = gid "build" "tests"
    let report = Budget.decide budget mode [ mustRecompute g cost [ RuleHashCat ] ]
    (Budget.entries report |> List.head).Decision

[<Tests>]
let tests =
    testList
        "DecideBudgetMatrix"
        [ test "a MustRecompute gate with cost <= ceiling is Recompute (naming its cause)" {
              for p in profiles do
                  for m in modes do
                      let ceiling = (Budget.budgetFor p m).Ceiling
                      for cost in costs do
                          if cost <= ceiling then
                              match decisionFor cost m p with
                              | Recompute(InputsChanged [ RuleHashCat ]) -> ()
                              | other -> failtestf "expected Recompute for %A/%A cost %A, got %A" p m cost other
          }

          test "a MustRecompute gate with cost > ceiling is OverBudget — Deferred in boundary modes" {
              for p in profiles do
                  for m in boundaryModes do
                      let ceiling = (Budget.budgetFor p m).Ceiling
                      for cost in costs do
                          if cost > ceiling then
                              match decisionFor cost m p with
                              | OverBudget r ->
                                  Expect.equal r.Class Deferred (sprintf "%A/%A cost %A defers" p m cost)
                                  Expect.equal r.Cost cost "reason names the cost"
                                  Expect.equal r.Ceiling ceiling "reason names the exceeded ceiling"
                                  Expect.equal r.Gate (gid "build" "tests") "reason names the gate"
                              | other -> failtestf "expected OverBudget for %A/%A cost %A, got %A" p m cost other
          }

          test "a MustRecompute gate with cost > ceiling is OverBudget — Skipped in inner-loop modes" {
              for p in profiles do
                  for m in innerModes do
                      let ceiling = (Budget.budgetFor p m).Ceiling
                      for cost in costs do
                          if cost > ceiling then
                              match decisionFor cost m p with
                              | OverBudget r -> Expect.equal r.Class Skipped (sprintf "%A/%A cost %A skips" p m cost)
                              | other -> failtestf "expected OverBudget for %A/%A cost %A, got %A" p m cost other
          }

          test "a cost == ceiling gate is Recompute (inclusive boundary)" {
              // Strict/Verify ceiling = High; a High-cost MustRecompute fits exactly.
              match decisionFor High Verify Strict with
              | Recompute _ -> ()
              | other -> failtestf "expected Recompute at the exact boundary, got %A" other
          } ]
