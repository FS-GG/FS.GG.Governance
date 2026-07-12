module FS.GG.Governance.PackEvidence.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.PackEvidence

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).

let private asm = SurfaceDrift.assemblyNamed "FS.GG.Governance.PackEvidence"

[<Tests>]
let tests =
    testList "SurfaceDrift" [ SurfaceDrift.surfaceTest "PackEvidence" "FS.GG.Governance.PackEvidence" asm ]
