module FS.GG.Governance.CostBudget.Tests.SmokeTests

open Expecto
open FS.GG.Governance.Config.Model
open FS.GG.Governance.Enforcement.Enforcement
open FS.GG.Governance.CostBudget.Model
open FS.GG.Governance.CostBudget
open FS.GG.Governance.CostBudget.Findings
open FS.GG.Governance.CostBudget.Tests.Support

// The Constitution-I smoke test: the public surface (`Budget.budgetFor`/`decide`, `Findings.cacheFindings`/
// `enforce`/`kindToken`) composes and is reachable through the loaded library.

[<Tests>]
let tests =
    testList
        "Smoke"
        [ test "the CostBudget public surface composes" {
              let budget = Budget.budgetFor Strict Verify
              let report = Budget.decide budget Verify [ reusable (gid "build" "tests") Cheap ]
              Expect.equal (List.length (Budget.entries report)) 1 "one entry"

              let findings = Findings.cacheFindings report allReal
              Expect.equal findings [] "a clean reuse is silent"

              let token = Findings.kindToken NoEvidence
              Expect.equal token "noEvidence" "kindToken is total"

              let decision = Findings.enforce Verify Strict { Gate = gid "build" "tests"; Kind = NoEvidence; BaseSeverity = Advisory; Message = "m" }
              Expect.equal decision.EffectiveSeverity Advisory "a base-advisory finding never blocks"
          } ]
