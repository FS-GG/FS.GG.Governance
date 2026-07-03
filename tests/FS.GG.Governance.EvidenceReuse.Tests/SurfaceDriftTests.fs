module FS.GG.Governance.EvidenceReuse.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.EvidenceReuse.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1), now via the shared
// SurfaceDrift helper (101/M-CI-3).

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private evidenceReuseAsm =
    EvidenceReuse.empty |> ignore
    EvidenceReuse.referenceValue (EvidenceRef "load") |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.EvidenceReuse"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "EvidenceReuse" "FS.GG.Governance.EvidenceReuse" evidenceReuseAsm

          test "the public surface is exactly the two modules (Model + EvidenceReuse), nothing else" {
              let typeNames =
                  evidenceReuseAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.EvidenceReuse.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.EvidenceReuse.EvidenceReuseModule"))
                  "EvidenceReuse operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n -> n.ToLowerInvariant().Contains "helper" || n.ToLowerInvariant().Contains "internal"))
                  "no helper/internal module leaks into the public surface"
          }

          SurfaceDrift.referencesOnly
              "EvidenceReuse"
              (fun n -> n = "FS.GG.Governance.FreshnessKey" || n = "FS.GG.Governance.Config")
              evidenceReuseAsm ]
