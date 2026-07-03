module FS.GG.Governance.Routing.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Routing.Model

// Reflective API surface-drift + dependency-hygiene checks (Principle II, research D1), now via the shared
// SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library.

let private routing = typeof<RoutingDiagnosticId>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "Routing" "FS.GG.Governance.Routing" routing

          SurfaceDrift.referencesOnly "Routing" (fun n -> n = "FS.GG.Governance.Config") routing ]
