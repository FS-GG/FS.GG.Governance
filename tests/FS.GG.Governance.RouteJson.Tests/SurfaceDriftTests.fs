module FS.GG.Governance.RouteJson.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.RouteJson

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3). The "exactly one module" leak guard stays inline.

// RouteJson exports only the module (no public types). Touch a member to force the library assembly to
// load, then locate it by name among the loaded assemblies.
let private routeJson =
    RouteJson.schemaVersion |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.RouteJson"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "RouteJson" "FS.GG.Governance.RouteJson" routeJson

          test "the public surface is exactly the RouteJson module, nothing private" {
              let typeNames =
                  routeJson.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              // exactly one exported type: the RouteJson module. No token helpers, sub-object writers,
              // or buffer plumbing leak (they are hidden by RouteJson.fsi).
              Expect.equal typeNames.Length 1 "exactly one exported type (the RouteJson module)"
              Expect.isTrue (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.RouteJson.RouteJsonModule")) "RouteJson module is public"
          }

          // FR-015 scope guard: the one-way graph is RouteJson -> Route -> {Gates, Routing, Findings} ->
          // Config, plus the F045 CacheEligibility embed, the F23 ProductSurfaces embed, the F052 GateRun
          // embed, the 068 RuleIdentity leaf, and the 073 Json* leaves. No kernel/host/adapter/Snapshot/
          // CLI dependency, no new third-party package.
          SurfaceDrift.referencesOnly
              "RouteJson"
              (fun n ->
                  n = "FS.GG.Governance.Route"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.Routing"
                  || n = "FS.GG.Governance.Findings"
                  || n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.CacheEligibility"
                  || n = "FS.GG.Governance.EvidenceReuse"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.ProductSurfaces"
                  || n = "FS.GG.Governance.GateRun"
                  || n = "FS.GG.Governance.GateExecution"
                  || n = "FS.GG.Governance.ExecutionRecord"
                  || n = "FS.GG.Governance.CommandRecord"
                  || n = "FS.GG.Governance.RuleIdentity"
                  || n = "FS.GG.Governance.JsonText"
                  || n = "FS.GG.Governance.JsonWriters"
                  || n = "FS.GG.Governance.JsonTokens")
              routeJson ]
