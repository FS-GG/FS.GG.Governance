module FS.GG.Governance.CostBudget.Tests.SkipDeferReportTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Tests.Support

// US1 (T019): an over-budget gate is reported OverBudget (distinguishably Skipped vs Deferred), never absent
// and never a Reuse/Recompute; `overBudget`/`recomputeGates`/`reuseGates` partition the report; the
// budget-zero/disabled (Cheap ceiling) edge skips every Medium+ MustRecompute while cheap recompute and reuse
// proceed (FR-003).

[<Tests>]
let tests =
    testList
        "SkipDeferReport"
        [ test "an over-budget gate is present and OverBudget — never absent, never Reuse/Recompute" {
              let g = gid "build" "tests"
              // Inner ⇒ Cheap ceiling; a High-cost MustRecompute is over budget.
              let report = Budget.decide (Budget.budgetFor Light Inner) Inner [ mustRecompute g High [ RuleHashCat ] ]
              Expect.equal (List.length (Budget.entries report)) 1 "the gate is still present"

              match (Budget.entries report |> List.head).Decision with
              | OverBudget _ -> ()
              | other -> failtestf "expected OverBudget, got %A" other

              Expect.equal (Budget.overBudget report |> List.map fst) [ g ] "overBudget names the gate"
              Expect.equal (Budget.recomputeGates report) [] "not recomputed"
              Expect.equal (Budget.reuseGates report) [] "not reused"
          }

          test "budget zero/disabled (Cheap ceiling): every Medium+ MustRecompute is OverBudget; cheap recompute + reuse proceed" {
              let cheapMust = mustRecompute (gid "d" "cheap") Cheap [ RuleHashCat ]
              let medMust = mustRecompute (gid "d" "medium") Medium [ RuleHashCat ]
              let highMust = mustRecompute (gid "d" "high") High [ RuleHashCat ]
              let exhMust = mustRecompute (gid "d" "exhaustive") Exhaustive [ RuleHashCat ]
              let reuseGate = reusable (gid "d" "reuse") Exhaustive

              let report =
                  Budget.decide (Budget.budgetFor Light Inner) Inner [ cheapMust; medMust; highMust; exhMust; reuseGate ]

              Expect.equal (Budget.recomputeGates report) [ gid "d" "cheap" ] "only the cheap recompute proceeds"
              Expect.equal (Budget.reuseGates report) [ gid "d" "reuse" ] "all reuse proceeds (free)"
              Expect.equal
                  (Budget.overBudget report |> List.map fst |> List.sort)
                  [ gid "d" "exhaustive"; gid "d" "high"; gid "d" "medium" ]
                  "every Medium+ must-recompute is over budget"
          }

          test "every over-budget reason carries gate, cost, ceiling, and a class" {
              let report = Budget.decide (Budget.budgetFor Light Inner) Inner [ mustRecompute (gid "d" "x") Exhaustive [ RuleHashCat ] ]
              match Budget.overBudget report with
              | [ (g, r) ] ->
                  Expect.equal g (gid "d" "x") "gate"
                  Expect.equal r.Cost Exhaustive "cost"
                  Expect.equal r.Ceiling Cheap "ceiling"
                  Expect.equal r.Class Skipped "class"
              | other -> failtestf "expected exactly one over-budget reason, got %A" other
          } ]
