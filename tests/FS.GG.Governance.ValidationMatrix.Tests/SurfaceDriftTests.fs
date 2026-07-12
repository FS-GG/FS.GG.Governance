module FS.GG.Governance.ValidationMatrix.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.ValidationMatrix

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).

let private asm = SurfaceDrift.assemblyNamed "FS.GG.Governance.ValidationMatrix"

[<Tests>]
let tests =
    testList "SurfaceDrift" [ SurfaceDrift.surfaceTest "ValidationMatrix" "FS.GG.Governance.ValidationMatrix" asm ]
