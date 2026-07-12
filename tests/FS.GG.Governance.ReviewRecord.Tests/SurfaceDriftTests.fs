module FS.GG.Governance.ReviewRecord.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.ReviewRecord

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1, SC-006), now via the
// shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library.

let private reviewRecordAsm =
    SurfaceDrift.assemblyNamed "FS.GG.Governance.ReviewRecord"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "ReviewRecord" "FS.GG.Governance.ReviewRecord" reviewRecordAsm

          test "the public surface is exactly the two modules (Model + ReviewRecord), nothing else" {
              let typeNames =
                  reviewRecordAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.ReviewRecord.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.ReviewRecord.ReviewRecordModule"))
                  "ReviewRecord operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n ->
                       let l = n.ToLowerInvariant()
                       l.Contains "helper" || l.Contains "internal"))
                  "no helper/internal module leaks into the public surface"
          }

          SurfaceDrift.referencesOnly
              "ReviewRecord"
              (fun n ->
                  n = "FS.GG.Governance.PromptIsolation"
                  || n = "FS.GG.Governance.SensedMetadata"
                  || n = "FS.GG.Governance.AgentReviewKey"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.CommandRecord"
                  || n = "FS.GG.Governance.Config")
              reviewRecordAsm ]
