module FS.GG.Governance.Route.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Route.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, research D1), now via the
// shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library.

let private route = typeof<RouteResult>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "Route" "FS.GG.Governance.Route" route

          test "the public surface is exactly the two modules Model and Route, nothing private" {
              let typeNames =
                  route.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              let hasModel = typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.Route.ModelModule")
              let hasRoute = typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.Route.RouteModule")
              Expect.isTrue hasModel "Model module is public"
              Expect.isTrue hasRoute "Route module is public"
          }

          SurfaceDrift.referencesOnly
              "Route"
              (fun n ->
                  n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.Routing"
                  || n = "FS.GG.Governance.Findings")
              route ]
