module FS.GG.Governance.RouteExplain.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.RouteExplain

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1), now via the shared
// SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library.

// Touch a member of a public module to force the library assembly to load, then locate it by name.
let private routeExplainAsm =
    RouteExplain.highCostThreshold |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.RouteExplain"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "RouteExplain" "FS.GG.Governance.RouteExplain" routeExplainAsm

          test "the public surface is exactly the two modules (Model + RouteExplain), nothing else" {
              let typeNames =
                  routeExplainAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.RouteExplain.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.RouteExplain.RouteExplainModule"))
                  "RouteExplain operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n ->
                       n.ToLowerInvariant().Contains "helper" || n.ToLowerInvariant().Contains "internal"))
                  "no helper/internal module leaks into the public surface"
          }

          SurfaceDrift.referencesOnly
              "RouteExplain"
              (fun n ->
                  n = "FS.GG.Governance.Route"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.Routing"
                  || n = "FS.GG.Governance.Findings"
                  || n = "FS.GG.Governance.Config")
              routeExplainAsm ]
