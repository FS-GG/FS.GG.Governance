module FS.GG.Governance.ReleaseDeclaration.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.ReleaseDeclaration

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).
// The public surface is exactly the `Declaration` module (its one `.fsi` contract).

let private library = typeof<Declaration.ReleaseDeclaration>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "ReleaseDeclaration" "FS.GG.Governance.ReleaseDeclaration" library ]
