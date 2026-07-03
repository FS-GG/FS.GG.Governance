module FS.GG.Governance.FreshnessSensing.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.FreshnessSensing

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, FR-014), now via the shared
// SurfaceDrift helper (101/M-CI-3). The Tier-1 baseline for the shared sensing edge's public surface.

// Touch a member to force the library assembly to load, then locate it by name.
let private freshnessSensingAsm =
    FreshnessSensing.realStoreReader |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.FreshnessSensing"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "FreshnessSensing" "FS.GG.Governance.FreshnessSensing" freshnessSensingAsm

          SurfaceDrift.referencesOnly
              "FreshnessSensing"
              (fun n ->
                  n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.FreshnessResolution"
                  || n = "FS.GG.Governance.EvidenceReuse"
                  || n = "FS.GG.Governance.CacheEligibility"
                  || n = "FS.GG.Governance.Kernel")
              freshnessSensingAsm ]
