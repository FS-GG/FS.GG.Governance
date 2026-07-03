module FS.GG.Governance.CacheEligibility.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.EvidenceReuse
open FS.GG.Governance.CacheEligibility

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, SC-008), now via the shared
// SurfaceDrift helper (101/M-CI-3).

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private cacheEligibilityAsm =
    CacheEligibility.entries (CacheEligibility.evaluate [] EvidenceReuse.empty) |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.CacheEligibility"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "CacheEligibility" "FS.GG.Governance.CacheEligibility" cacheEligibilityAsm

          test "the public surface is exactly the two modules (Model + CacheEligibility), nothing else" {
              let typeNames =
                  cacheEligibilityAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.CacheEligibility.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.CacheEligibility.CacheEligibilityModule"))
                  "CacheEligibility operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n ->
                       let l = n.ToLowerInvariant()
                       l.Contains "helper" || l.Contains "internal" || l.Contains "comparator"))
                  "no helper/internal/comparator module leaks into the public surface"
          }

          SurfaceDrift.referencesOnly
              "CacheEligibility"
              (fun n ->
                  n = "FS.GG.Governance.EvidenceReuse"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.Config")
              cacheEligibilityAsm ]
