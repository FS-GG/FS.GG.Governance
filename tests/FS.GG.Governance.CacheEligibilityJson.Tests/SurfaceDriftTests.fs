module FS.GG.Governance.CacheEligibilityJson.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.CacheEligibilityJson

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3). The "exactly one module" leak guard stays inline.

let private cacheEligibilityJson = SurfaceDrift.assemblyNamed "FS.GG.Governance.CacheEligibilityJson"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "CacheEligibilityJson" "FS.GG.Governance.CacheEligibilityJson" cacheEligibilityJson

          test "the public surface is exactly the CacheEligibilityJson module, nothing private" {
              let typeNames =
                  cacheEligibilityJson.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              // exactly one exported type: the CacheEligibilityJson module. No writer / token helper leak
              // (they are hidden by CacheEligibilityJson.fsi).
              Expect.equal typeNames.Length 1 "exactly one exported type (the CacheEligibilityJson module)"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.CacheEligibilityJson.CacheEligibilityJsonModule"))
                  "CacheEligibilityJson module is public"
          }

          // FR-014 scope guard: One-way dependency CacheEligibilityJson -> CacheEligibility ->
          // EvidenceReuse/Gates/FreshnessKey/Config, plus the 073 Json* leaves. No host/adapter/CLI/
          // sibling-projection edge and no new third-party package.
          SurfaceDrift.referencesOnly
              "CacheEligibilityJson"
              (fun n ->
                  n = "FS.GG.Governance.CacheEligibility"
                  || n = "FS.GG.Governance.EvidenceReuse"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.JsonText"
                  || n = "FS.GG.Governance.JsonWriters")
              cacheEligibilityJson ]
