module FS.GG.Governance.FreshnessResolution.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.FreshnessResolution
open FS.GG.Governance.FreshnessResolution.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, FR-014), now via the shared
// SurfaceDrift helper (101/M-CI-3).

let private noSensed: SensedFacts =
    { RuleHash = None
      GeneratorVersion = None
      Base = None
      Head = None
      CoveredArtifacts = Map.empty
      CommandVersions = Map.empty }

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private freshnessResolutionAsm =
    FreshnessResolution.entries (FreshnessResolution.resolve [] noSensed) |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.FreshnessResolution"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "FreshnessResolution" "FS.GG.Governance.FreshnessResolution" freshnessResolutionAsm

          test "the public surface is exactly the two modules (Model + FreshnessResolution), nothing else" {
              let typeNames =
                  freshnessResolutionAsm.GetExportedTypes() |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.FreshnessResolution.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.FreshnessResolution.FreshnessResolutionModule"))
                  "FreshnessResolution operations module is public"
              Expect.isFalse
                  (typeNames
                   |> Array.exists (fun n ->
                       let l = n.ToLowerInvariant()
                       l.Contains "helper" || l.Contains "internal" || l.Contains "comparator"))
                  "no helper/internal/comparator module leaks into the public surface"
          }

          SurfaceDrift.referencesOnly
              "FreshnessResolution"
              (fun n ->
                  n = "FS.GG.Governance.CacheEligibility"
                  || n = "FS.GG.Governance.EvidenceReuse"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.Kernel")
              freshnessResolutionAsm ]
