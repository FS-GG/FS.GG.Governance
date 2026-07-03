module FS.GG.Governance.Kernel.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Kernel

// Reflective API surface-drift + dependency-hygiene checks (D6, FR-010/FR-011, Principle II), now via
// the shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper, never in the kernel.

let private kernel = typeof<FactId>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "V11" "FS.GG.Governance.Kernel" kernel

          SurfaceDrift.referencesOnly "V11" (fun _ -> false) kernel ]
