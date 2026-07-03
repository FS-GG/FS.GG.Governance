module FS.GG.Governance.AgentReviewKey.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.AgentReviewKey
open FS.GG.Governance.AgentReviewKey.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1), now via the shared
// SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library.

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private agentReviewKeyAsm =
    Model.inputToken ModelIdInput |> ignore
    AgentReviewKey.value (CacheKey "load") |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.AgentReviewKey"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "AgentReviewKey" "FS.GG.Governance.AgentReviewKey" agentReviewKeyAsm

          test "the public surface is exactly the two modules (Model + AgentReviewKey), nothing else" {
              let typeNames =
                  agentReviewKeyAsm.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)

              // The two operation/type modules plus the DU/record/newtype types they declare are exported,
              // but no token/encoder/buffer HELPER module leaks (those are hidden by the .fsi files).
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.AgentReviewKey.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.AgentReviewKey.AgentReviewKeyModule"))
                  "AgentReviewKey operations module is public"
              Expect.isFalse
                  (typeNames |> Array.exists (fun n -> n.ToLowerInvariant().Contains "encode" || n.ToLowerInvariant().Contains "segment"))
                  "no encoder/segment helper module leaks into the public surface"
          }

          SurfaceDrift.referencesOnly
              "AgentReviewKey"
              (fun n -> n = "FS.GG.Governance.FreshnessKey" || n = "FS.GG.Governance.Config")
              agentReviewKeyAsm ]
