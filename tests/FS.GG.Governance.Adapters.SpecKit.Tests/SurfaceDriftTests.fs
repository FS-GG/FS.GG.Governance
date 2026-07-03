module FS.GG.Governance.Adapters.SpecKit.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi
open FS.GG.Governance.Adapters.SpecKit

// Reflective API surface-drift + dependency-hygiene checks for the SpecKit adapter (Principle II), now via
// the shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the adapter.

let private specKit = typeof<ConstitutionDial>.Assembly
let private spi = typeof<Composed<int, int>>.Assembly
let private kernel = typeof<FactId>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "V8 SpecKit" "FS.GG.Governance.Adapters.SpecKit" specKit

          SurfaceDrift.referencesOnly
              "V8 SpecKit"
              (fun n -> n = "FS.GG.Governance.Adapters.Spi" || n = "FS.GG.Governance.Kernel")
              specKit

          SurfaceDrift.noInboundReferences "V8 SpecKit" [ kernel; spi ] specKit ]
