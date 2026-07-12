module FS.GG.Governance.FreshnessSensing.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.FreshnessSensing

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, FR-014), now via the shared
// SurfaceDrift helper (101/M-CI-3). The Tier-1 baseline for the shared sensing edge's public surface.

let private freshnessSensingAsm =
    SurfaceDrift.assemblyNamed "FS.GG.Governance.FreshnessSensing"

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
