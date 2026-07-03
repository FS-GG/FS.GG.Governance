module FS.GG.Governance.CurrencyEnforcement.Tests.SurfaceDriftTests

// The leaf's public surface-drift baseline (Constitution Principle II), now via the shared SurfaceDrift
// helper (101/M-CI-3). BLESS_SURFACE=1 regenerates it.

open Expecto
open FS.GG.Governance.Tests.Common

module CE = FS.GG.Governance.CurrencyEnforcement.CurrencyEnforcement

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest
              "CurrencyEnforcement"
              "FS.GG.Governance.CurrencyEnforcement"
              typeof<CE.CurrencyFinding>.Assembly ]
