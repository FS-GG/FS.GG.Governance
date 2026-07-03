module FS.GG.Governance.DesignChecks.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.DesignChecks.Model

let private library = typeof<DesignFacts>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "DesignChecks" "FS.GG.Governance.DesignChecks" library ]
