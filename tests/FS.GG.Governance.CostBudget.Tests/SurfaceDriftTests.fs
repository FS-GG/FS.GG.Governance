module FS.GG.Governance.CostBudget.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.CostBudget

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, SC-008), now via the shared
// SurfaceDrift helper (101/M-CI-3).

let private costBudgetAsm = SurfaceDrift.assemblyNamed "FS.GG.Governance.CostBudget"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "CostBudget" "FS.GG.Governance.CostBudget" costBudgetAsm

          SurfaceDrift.referencesOnly
              "CostBudget"
              (fun n ->
                  n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.Enforcement"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.EvidenceReuse"
                  || n = "FS.GG.Governance.CacheEligibility"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.AgentReviewKey")
              costBudgetAsm ]
