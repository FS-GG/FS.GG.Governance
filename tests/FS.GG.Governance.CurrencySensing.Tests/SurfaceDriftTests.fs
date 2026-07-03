module FS.GG.Governance.CurrencySensing.Tests.SurfaceDriftTests

// The CurrencySensing core's public surface-drift baseline (Constitution Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3). BLESS_SURFACE=1 regenerates.

open System.Reflection
open Expecto
open FS.GG.Governance.Tests.Common

module CS = FS.GG.Governance.CurrencySensing.CurrencySensing

// Touch the module so the assembly is loaded, then resolve it by name (the module is a static
// class with no exported type to `typeof`, so we load by assembly name).
let private currencySensing =
    CS.parseManifest [] |> ignore
    Assembly.Load "FS.GG.Governance.CurrencySensing"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "CurrencySensing" "FS.GG.Governance.CurrencySensing" currencySensing ]
