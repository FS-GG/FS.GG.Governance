module FS.GG.Governance.ReviewRecord.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.ReviewRecord
open FS.GG.Governance.ReviewRecord.Tests.Support

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1, SC-006), now via the
// shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library.

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private reviewRecordAsm =
    let loadRecord =
        buildOf baseRequest (modelId "m") (modelVersion "v") (promptHash "p") [] (responseDigest "r") (recordedVerdict "x") []

    ReviewRecord.identityValue (ReviewRecord.canonicalId loadRecord) |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.ReviewRecord"
        | None -> false)

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
