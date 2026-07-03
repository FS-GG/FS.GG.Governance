module FS.GG.Governance.SurfaceChecks.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common

module SC = FS.GG.Governance.SurfaceChecks.Model

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "SurfaceChecks" "FS.GG.Governance.SurfaceChecks" typeof<SC.CheckDomain>.Assembly

          SurfaceDrift.surfaceTest
              "SurfaceChecks.Dispatch"
              "FS.GG.Governance.SurfaceChecks.Dispatch"
              typeof<FS.GG.Governance.SurfaceChecks.Dispatch.Composition.DomainFactBundle>.Assembly ]
