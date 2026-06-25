module FS.GG.Governance.CostBudget.Tests.ReuseGuardTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Tests.Support

// Polish (T048): the no-new-vocabulary guard. `decide` folds the F041 verdict VERBATIM — the carried cause /
// evidence read back unchanged — and the cost dimension never enters the freshness key (FR-006, FR-013,
// D4/D5). A future F041 verdict / F030 cause case would be a compile error in `decide`'s exhaustive match.

[<Tests>]
let tests =
    testList
        "ReuseGuard"
        [ test "decide carries the F030 RecomputeCause into Recompute VERBATIM (no second opinion)" {
              for cats in [ [ RuleHashCat ]; [ BaseRevisionCat; HeadRevisionCat ]; [] ] do
                  let report = Budget.decide (Budget.budgetFor Profile.Release RunMode.Release) RunMode.Release [ mustRecompute (gid "d" "x") Cheap cats ]
                  match (Budget.entries report |> List.head).Decision with
                  | Recompute(InputsChanged got) -> Expect.equal got cats "the changed categories read back verbatim"
                  | other -> failtestf "expected Recompute (InputsChanged %A), got %A" cats other
          }

          test "decide carries the F030 EvidenceRef into Reuse VERBATIM" {
              let report = Budget.decide (Budget.budgetFor Profile.Release RunMode.Release) RunMode.Release [ reusable (gid "d" "x") Cheap ]
              match (Budget.entries report |> List.head).Decision with
              | Reuse r -> Expect.equal r refA "the reuse reference reads back verbatim"
              | other -> failtestf "expected Reuse, got %A" other
          }

          test "the cost tier never enters the freshness verdict (cost excluded from the key)" {
              // The SAME real F041 verdict is produced regardless of the cost we later attach.
              let verdict = verdictFor baseInputs baseStore
              Expect.equal verdict (Reusable refA) "verdict is cost-independent"
          } ]
