module FS.GG.Governance.GeneratedViewsJson.Tests.SurfaceBaselineTests

open Expecto
open FS.GG.Governance.Tests.Common

// Reflective API surface-drift + dependency/scope-hygiene checks for the JSON-4 GeneratedViewsJson leaf
// (Principle II), via the shared SurfaceDrift helper (101/M-CI-3). The public surface is exactly the one
// GeneratedViewsJson.fsi contract; the referencesOnly allow-list pins its layer — ABOVE
// RefreshJson/CurrencyEnforcement (whose vocabularies it writes) but below the projections that consume it.
// Unlike the JsonWriters leaf (a deny-list that forbids every `*Json` edge), this leaf MAY reference the
// RefreshJson leaf on purpose — that edge is exactly why it cannot fold into JsonWriters.

let private asm =
    SurfaceDrift.assemblyNamed "FS.GG.Governance.GeneratedViewsJson"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "GeneratedViewsJson" "FS.GG.Governance.GeneratedViewsJson" asm

          SurfaceDrift.referencesOnly
              "GeneratedViewsJson"
              (fun n ->
                  n = "FS.GG.Governance.CurrencyEnforcement"
                  || n = "FS.GG.Governance.Enforcement"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.RefreshJson"
                  || n = "FS.GG.Governance.JsonTokens"
                  || n = "FS.GG.Governance.Config")
              asm ]
