module FS.GG.Governance.RefreshJson.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).
// The public surface is the `RefreshModel` + `RefreshJson` modules.

let private library = typeof<FS.GG.Governance.RefreshJson.RefreshModel.GenerationManifest>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "RefreshJson" "FS.GG.Governance.RefreshJson" library ]
