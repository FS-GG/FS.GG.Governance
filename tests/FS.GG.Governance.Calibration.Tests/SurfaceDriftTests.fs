module FS.GG.Governance.Calibration.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Calibration

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1/D3, SC-007), now via
// the shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library.

let private calibrationAsm = SurfaceDrift.assemblyNamed "FS.GG.Governance.Calibration"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "Calibration" "FS.GG.Governance.Calibration" calibrationAsm

          test "the public surface is exactly the two modules (Model + Calibration), nothing else" {
              let typeNames =
                  calibrationAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.Calibration.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.Calibration.CalibrationModule"))
                  "Calibration operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n ->
                       let l = n.ToLowerInvariant()
                       l.Contains "helper" || l.Contains "internal"))
                  "no helper/internal module leaks into the public surface"
          }

          SurfaceDrift.referencesOnly
              "Calibration"
              (fun n ->
                  n = "FS.GG.Governance.AgentReviewKey"
                  || n = "FS.GG.Governance.ReviewRecord"
                  || n = "FS.GG.Governance.PromptIsolation"
                  || n = "FS.GG.Governance.SensedMetadata"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.Config")
              calibrationAsm ]
