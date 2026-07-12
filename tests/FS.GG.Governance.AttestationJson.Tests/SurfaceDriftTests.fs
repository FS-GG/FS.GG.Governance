module FS.GG.Governance.AttestationJson.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.AttestationJson

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).

let private asm = SurfaceDrift.assemblyNamed "FS.GG.Governance.AttestationJson"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "AttestationJson" "FS.GG.Governance.AttestationJson" asm ]
