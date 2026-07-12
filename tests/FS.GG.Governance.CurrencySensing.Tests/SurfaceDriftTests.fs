module FS.GG.Governance.CurrencySensing.Tests.SurfaceDriftTests

// The CurrencySensing core's public surface-drift baseline (Constitution Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3). BLESS_SURFACE=1 regenerates.

open Expecto
open FS.GG.Governance.Tests.Common

let private currencySensing =
    SurfaceDrift.assemblyNamed "FS.GG.Governance.CurrencySensing"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "CurrencySensing" "FS.GG.Governance.CurrencySensing" currencySensing ]
