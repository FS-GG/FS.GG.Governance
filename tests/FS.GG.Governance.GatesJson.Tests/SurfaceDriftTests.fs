module FS.GG.Governance.GatesJson.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.GatesJson

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3). The "exactly one module" leak guard stays inline.

let private gatesJson = SurfaceDrift.assemblyNamed "FS.GG.Governance.GatesJson"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "GatesJson" "FS.GG.Governance.GatesJson" gatesJson

          test "the public surface is exactly the GatesJson module, nothing private" {
              let typeNames =
                  gatesJson.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              // exactly one exported type: the GatesJson module. No token helpers, sub-object writers,
              // or buffer plumbing leak (they are hidden by GatesJson.fsi).
              Expect.equal typeNames.Length 1 "exactly one exported type (the GatesJson module)"
              Expect.isTrue (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.GatesJson.GatesJsonModule")) "GatesJson module is public"
          }

          // FR-015 scope guard: the one-way graph is GatesJson -> Gates -> Config, plus the 073 Json*
          // leaves. No kernel/host/adapter/Snapshot/Route/RouteJson/CLI dependency, no new third-party
          // package.
          SurfaceDrift.referencesOnly
              "GatesJson"
              (fun n ->
                  n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.JsonText"
                  || n = "FS.GG.Governance.JsonTokens"
                  || n = "FS.GG.Governance.JsonWriters") // 111/A4: shared freshnessKey/prerequisite writers
              gatesJson ]
