module FS.GG.Governance.HumanText.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.HumanText

// T012: reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper
// (101/M-CI-3). Reflection lives in the helper. The HumanText assembly is resolved from the loaded
// app-domain after forcing a RenderMode load (preserved from the original test).

let private humanText = SurfaceDrift.assemblyNamed "FS.GG.Governance.HumanText"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "HumanText" "FS.GG.Governance.HumanText" humanText ]
