module FS.GG.Governance.PackageChecks.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.PackageChecks.Model

// Reflective API surface-drift + dependency-scope guard (Principle II), now via the shared SurfaceDrift
// helper (101/M-CI-3). Reflection lives in the helper, never in the library.

let private library = typeof<PackageFacts>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "PackageChecks" "FS.GG.Governance.PackageChecks" library

          SurfaceDrift.referencesOnly "PackageChecks" (fun n -> n.StartsWith "FS.GG.Governance.") library ]
