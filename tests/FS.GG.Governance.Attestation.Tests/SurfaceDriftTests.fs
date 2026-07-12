module FS.GG.Governance.Attestation.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Attestation

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).

let private asm = SurfaceDrift.assemblyNamed "FS.GG.Governance.Attestation"

[<Tests>]
let tests =
    testList "SurfaceDrift" [ SurfaceDrift.surfaceTest "Attestation" "FS.GG.Governance.Attestation" asm ]
