module FS.GG.Governance.Scaffold.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper, never in the library.

let private scaffold =
    // The library exports public types, so locate its assembly directly via one of them.
    typeof<FS.GG.Governance.Scaffold.Model.ProviderId>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "Scaffold" "FS.GG.Governance.Scaffold" scaffold

          SurfaceDrift.referencesOnly "Scaffold" (fun n -> n = "FS.GG.Governance.Kernel") scaffold ]
