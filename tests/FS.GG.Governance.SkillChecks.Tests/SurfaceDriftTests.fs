module FS.GG.Governance.SkillChecks.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.SkillChecks.Model

let private library = typeof<SkillFacts>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "SkillChecks" "FS.GG.Governance.SkillChecks" library

          SurfaceDrift.referencesOnly "SkillChecks" (fun n -> n.StartsWith "FS.GG.Governance.") library ]
