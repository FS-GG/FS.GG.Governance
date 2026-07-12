module FS.GG.Governance.ReleaseReport.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.ReleaseReport

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).

let private asm =
    SurfaceDrift.assemblyNamed "FS.GG.Governance.ReleaseReport"

[<Tests>]
let tests =
    testList "SurfaceDrift" [ SurfaceDrift.surfaceTest "ReleaseReport" "FS.GG.Governance.ReleaseReport" asm ]
