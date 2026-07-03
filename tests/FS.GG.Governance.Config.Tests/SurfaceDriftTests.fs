module FS.GG.Governance.Config.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Config.Model

// Reflective API surface-drift + dependency-hygiene checks (Principle II, research D1), now via the shared
// SurfaceDrift helper (101/M-CI-3). FS.GG.Contracts is the ONE sanctioned cross-repo dependency
// (FS.GG.Governance#14): the org-shared, BCL-only typed source of truth for the `.fsgg` schema version
// constants — it carries no kernel/host/capability surface, so the isolation the guard protects is preserved.

let private config = typeof<DiagnosticId>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "Config" "FS.GG.Governance.Config" config

          SurfaceDrift.referencesOnly "Config" (fun n -> n = "YamlDotNet" || n = "FS.GG.Contracts") config ]
