module FS.GG.Governance.Adapters.Spi.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Kernel
open FS.GG.Governance.Adapters.Spi

// Reflective API surface-drift + dependency-hygiene checks for the SPI (FR-016, V72, SC-008, Principle II),
// now via the shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper, never in the SPI.

let private spi = typeof<Composed<int, int>>.Assembly
let private kernel = typeof<FactId>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "V72 Spi" "FS.GG.Governance.Adapters.Spi" spi

          SurfaceDrift.referencesOnly "V72 Spi" (fun n -> n = "FS.GG.Governance.Kernel") spi

          SurfaceDrift.noInboundReferences "V72 Spi" [ kernel ] spi ]
