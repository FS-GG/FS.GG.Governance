module FS.GG.Governance.VerdictReuse.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.AgentReviewKey.Model
open FS.GG.Governance.VerdictReuse
open FS.GG.Governance.VerdictReuse.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1), now via the shared
// SurfaceDrift helper (101/M-CI-3).

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private verdictReuseAsm =
    Model.inputGroup ModelIdInput |> ignore
    VerdictReuse.referenceValue (VerdictRef "load") |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.VerdictReuse"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "VerdictReuse" "FS.GG.Governance.VerdictReuse" verdictReuseAsm

          test "the public surface is exactly the two modules (Model + VerdictReuse), nothing else" {
              let typeNames =
                  verdictReuseAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.VerdictReuse.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.VerdictReuse.VerdictReuseModule"))
                  "VerdictReuse operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n ->
                       let l = n.ToLowerInvariant()
                       l.Contains "helper" || l.Contains "internal"))
                  "no helper/internal module leaks into the public surface"
          }

          SurfaceDrift.referencesOnly
              "VerdictReuse"
              (fun n ->
                  n = "FS.GG.Governance.AgentReviewKey"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.Config")
              verdictReuseAsm ]
