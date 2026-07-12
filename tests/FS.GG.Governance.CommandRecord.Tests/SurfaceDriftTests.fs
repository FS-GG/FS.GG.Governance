module FS.GG.Governance.CommandRecord.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.CommandRecord
open FS.GG.Governance.CommandRecord.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1), now via the
// shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library.

let private commandRecordAsm = SurfaceDrift.assemblyNamed "FS.GG.Governance.CommandRecord"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "CommandRecord" "FS.GG.Governance.CommandRecord" commandRecordAsm

          test "the public surface is exactly the two modules (Model + CommandRecord), nothing else" {
              let typeNames =
                  commandRecordAsm.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.CommandRecord.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.CommandRecord.CommandRecordModule"))
                  "CommandRecord operations module is public"
              Expect.isFalse
                  (typeNames |> Array.exists (fun n -> n.ToLowerInvariant().Contains "encode" || n.ToLowerInvariant().Contains "segment"))
                  "no encoder/segment helper module leaks into the public surface"
          }

          SurfaceDrift.referencesOnly "CommandRecord" (fun n -> n = "FS.GG.Governance.Config") commandRecordAsm ]
