module FS.GG.Governance.CostBudgetJson.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.CostBudgetJson

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).

let private asm = SurfaceDrift.assemblyNamed "FS.GG.Governance.CostBudgetJson"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "CostBudgetJson" "FS.GG.Governance.CostBudgetJson" asm ]
