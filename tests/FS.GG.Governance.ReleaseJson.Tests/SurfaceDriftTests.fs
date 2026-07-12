module FS.GG.Governance.ReleaseJson.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.ReleaseJson

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).
// The public surface is exactly the `ReleaseJson` module (the one `.fsi` contract).

let private library = SurfaceDrift.assemblyNamed "FS.GG.Governance.ReleaseJson"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "ReleaseJson" "FS.GG.Governance.ReleaseJson" library ]
