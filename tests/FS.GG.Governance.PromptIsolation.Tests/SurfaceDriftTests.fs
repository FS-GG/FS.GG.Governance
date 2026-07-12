module FS.GG.Governance.PromptIsolation.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.PromptIsolation
open FS.GG.Governance.PromptIsolation.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1), now via the shared
// SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library.

let private promptIsolationAsm = SurfaceDrift.assemblyNamed "FS.GG.Governance.PromptIsolation"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "PromptIsolation" "FS.GG.Governance.PromptIsolation" promptIsolationAsm

          test "the public surface is exactly the two modules (Model + PromptIsolation), nothing else" {
              let typeNames =
                  promptIsolationAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.PromptIsolation.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.PromptIsolation.PromptIsolationModule"))
                  "PromptIsolation operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n ->
                       let l = n.ToLowerInvariant()
                       l.Contains "helper" || l.Contains "internal"))
                  "no helper/internal module leaks into the public surface"
          }

          SurfaceDrift.referencesOnly
              "PromptIsolation"
              (fun n ->
                  n = "FS.GG.Governance.AgentReviewKey"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.Config")
              promptIsolationAsm ]
