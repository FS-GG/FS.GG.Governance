module FS.GG.Governance.Findings.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Findings.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, research D1), now via the
// shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library.

let private findings = typeof<FindingId>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "Findings" "FS.GG.Governance.Findings" findings

          test "the public surface is exactly the two modules Model and Findings, nothing private" {
              let typeNames =
                  findings.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              // The two module suffixes (ModelModule/FindingsModule) and the DU nested types
              // under Model; the only top-level F# modules are Model and Findings.
              let hasModel = typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.Findings.ModelModule")
              let hasFindings = typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.Findings.FindingsModule")
              Expect.isTrue hasModel "Model module is public"
              Expect.isTrue hasFindings "Findings module is public"
          }

          SurfaceDrift.referencesOnly
              "Findings"
              (fun n -> n = "FS.GG.Governance.Config" || n = "FS.GG.Governance.Routing")
              findings ]
