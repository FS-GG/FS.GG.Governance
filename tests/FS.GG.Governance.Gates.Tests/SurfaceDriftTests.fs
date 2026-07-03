module FS.GG.Governance.Gates.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Gates.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, research D1), now via the
// shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library.

let private gates = typeof<GateId>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "Gates" "FS.GG.Governance.Gates" gates

          test "the public surface is exactly the two modules Model and Gates, nothing private" {
              let typeNames =
                  gates.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              let hasModel = typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.Gates.ModelModule")
              let hasGates = typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.Gates.GatesModule")
              Expect.isTrue hasModel "Model module is public"
              Expect.isTrue hasGates "Gates module is public"
          }

          SurfaceDrift.referencesOnly "Gates" (fun n -> n = "FS.GG.Governance.Config") gates ]
