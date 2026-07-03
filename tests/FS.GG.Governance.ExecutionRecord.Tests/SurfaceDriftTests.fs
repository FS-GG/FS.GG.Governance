module FS.GG.Governance.ExecutionRecord.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.ExecutionRecord
open FS.GG.Governance.ExecutionRecord.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3).

// ExecutionRecord exports only the module (no public types). Touch a member to force the library assembly to
// load, then locate it by name among the loaded assemblies.
let private executionRecord =
    ExecutionRecord.digestOf bytesA |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.ExecutionRecord"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "ExecutionRecord" "FS.GG.Governance.ExecutionRecord" executionRecord

          test "the public surface is exactly the ExecutionRecord module, nothing private" {
              let typeNames =
                  executionRecord.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              // exactly one exported type: the ExecutionRecord module. No helper leak (there are no private
              // helpers — `digestOf` is a four-step BCL pipeline, `recordOf` is one expression) and no new type.
              Expect.equal typeNames.Length 1 "exactly one exported type (the ExecutionRecord module)"

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.ExecutionRecord.ExecutionRecordModule"))
                  "ExecutionRecord module is public"
          }

          SurfaceDrift.referencesOnly
              "ExecutionRecord"
              (fun n -> n = "FS.GG.Governance.CommandRecord" || n = "FS.GG.Governance.Config")
              executionRecord ]
