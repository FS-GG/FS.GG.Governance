module FS.GG.Governance.JsonWriters.Tests.SurfaceBaselineTests

open System.Text.Json
open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.JsonWriters

// Reflective API surface-drift + dependency/scope-hygiene checks for the 073 JsonWriters leaf
// (Principle II), now via the shared SurfaceDrift helper (101/M-CI-3). The bespoke forbidden-edge scope
// guard stays inline (it is a deny-list, not an allow-list).

let private jsonWritersAsm =
    SurfaceDrift.assemblyNamed "FS.GG.Governance.JsonWriters"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "JsonWriters" "FS.GG.Governance.JsonWriters" jsonWritersAsm

          test "JsonWriters takes no kernel/host/projection edge (scope guard — pure writer leaf)" {
              // The leaf references the JsonTokens leaf + the domain owners of the values it walks. It must
              // NOT reach the kernel/host capability the pure projections exclude, NOT any *Json projection,
              // and NOT the sibling JsonText leaf — it sits ABOVE the domain owners and BELOW the projections.
              let forbidden (n: string) =
                  n = "FS.GG.Governance.Kernel"
                  || n = "FS.GG.Governance.Host"
                  || n = "FS.GG.Governance.Cli"
                  || n = "FS.GG.Governance.Snapshot"
                  || n = "FS.GG.Governance.JsonText"
                  || n.StartsWith "FS.GG.Governance.Adapters"
                  || (n.StartsWith "FS.GG.Governance" && n.EndsWith "Json")

              let offending =
                  jsonWritersAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter forbidden

              Expect.isEmpty
                  offending
                  (sprintf "JsonWriters must not reference kernel/host/projection/JsonText; found: %A" offending)
          } ]
