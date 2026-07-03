module FS.GG.Governance.DocsChecks.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.DocsChecks.Model

let private library = typeof<DocsFacts>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "DocsChecks" "FS.GG.Governance.DocsChecks" library

          SurfaceDrift.referencesOnly "DocsChecks" (fun n -> n.StartsWith "FS.GG.Governance.") library ]
