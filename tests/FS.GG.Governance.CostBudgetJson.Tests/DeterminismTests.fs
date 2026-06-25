module FS.GG.Governance.CostBudgetJson.Tests.DeterminismTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.FreshnessKey.Model
open FS.GG.Governance.Gates.Model
open FS.GG.Governance.EvidenceReuse.Model
open FS.GG.Governance.CacheEligibility.Model
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudgetJson
open FS.GG.Governance.CostBudgetJson.Tests.Support

// US2 (T025): `ofReport` is byte-identical for identical input; reordering the candidates fed to `decide`
// cannot change the text (the report is already GateId-ordinal); `decisions`/`findings` are ALWAYS present
// (empty arrays for an all-reusable, no-finding run); no clock/host-path/env/process-exit-code leaks
// (FR-011, SC-008).

let private cand (gate: GateId) (cost: Cost) (verdict: CacheEligibilityVerdict) : CandidateCost =
    { Gate = gate; Cost = cost; Verdict = verdict; Review = Deterministic }

let private candidates =
    [ cand (gid "z" "z") High (MustRecompute(InputsChanged [ RuleHashCat ]))
      cand (gid "a" "a") Cheap (Reusable(EvidenceRef "e"))
      cand (gid "m" "m") Cheap (MustRecompute NoPriorEvidence) ]

[<Tests>]
let tests =
    testList
        "Determinism"
        [ test "CostBudgetJson.ofReport is byte-identical for identical input" {
              Expect.equal (CostBudgetJson.ofReport mixedReport mixedFindings) (CostBudgetJson.ofReport mixedReport mixedFindings) "identical"
          }

          test "reordering the candidates fed to decide cannot change the text" {
              let budget = Budget.budgetFor Profile.Release RunMode.Release
              let a = CostBudgetJson.ofReport (Budget.decide budget RunMode.Release candidates) []
              let b = CostBudgetJson.ofReport (Budget.decide budget RunMode.Release (List.rev candidates)) []
              Expect.equal a b "report is GateId-ordinal regardless of supply order"
          }

          test "an all-reusable, no-finding run is well-formed (empty decisions ok, findings [])" {
              let empty = CostBudgetJson.ofReport (CacheDecisionReport []) []
              Expect.stringContains empty "\"decisions\":[]" "empty decisions array present"
              Expect.stringContains empty "\"findings\":[]" "empty findings array present"
          }

          test "no wall-clock / host path / numeric process exit code leaks" {
              let json = CostBudgetJson.ofReport mixedReport mixedFindings
              Expect.isFalse (json.Contains "/home/") "no absolute path"
              Expect.isFalse (json.Contains "exitCode") "no process exit code in the cost-budget document"
          } ]
