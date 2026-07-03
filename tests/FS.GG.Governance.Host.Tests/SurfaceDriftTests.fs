module FS.GG.Governance.Host.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Host

// Reflective API surface-drift + dependency-hygiene checks for the Host (FR-018, V13/V14, Principle II),
// now via the shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper, never in the Host.

let private host = typeof<Effect>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "V13 Host" "FS.GG.Governance.Host" host

          SurfaceDrift.referencesOnly "V13 Host" (fun n -> n = "FS.GG.Governance.Kernel") host ]
