module FS.GG.Governance.ProductSurfaces.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.ProductSurfaces.Model

// Reflective API surface-drift check (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3).

let private surfaceAsm = typeof<ClassificationReason>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "ProductSurfaces" "FS.GG.Governance.ProductSurfaces" surfaceAsm ]
