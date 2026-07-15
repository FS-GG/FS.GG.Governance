module FS.GG.Governance.AuditJson.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.AuditJson

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3). The "exactly one module" leak guard stays inline.

let private auditJson = SurfaceDrift.assemblyNamed "FS.GG.Governance.AuditJson"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "AuditJson" "FS.GG.Governance.AuditJson" auditJson

          test "the public surface is exactly the AuditJson module, nothing private" {
              let typeNames =
                  auditJson.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              // exactly one exported type: the AuditJson module. No token helpers, enforcement/item
              // writers, or buffer plumbing leak (they are hidden by AuditJson.fsi).
              Expect.equal typeNames.Length 1 "exactly one exported type (the AuditJson module)"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.AuditJson.AuditJsonModule"))
                  "AuditJson module is public"
          }

          // FR-014 scope guard: One-way dependency AuditJson -> Ship -> Enforcement/Route/Config/Gates/
          // Findings/Kernel, plus the F045 CacheEligibility embed, the F052 GateRun embed, the 068
          // RuleIdentity leaf, the F070 CurrencyEnforcement edge + the JSON-4 GeneratedViewsJson writer leaf
          // (RefreshJson arrives transitively through it), and the 073 Json* leaves.
          // No host/adapter/CLI edge and no new third-party package.
          SurfaceDrift.referencesOnly
              "AuditJson"
              (fun n ->
                  n = "FS.GG.Governance.Ship"
                  || n = "FS.GG.Governance.Enforcement"
                  || n = "FS.GG.Governance.Route"
                  || n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.Findings"
                  || n = "FS.GG.Governance.Kernel"
                  || n = "FS.GG.Governance.CacheEligibility"
                  || n = "FS.GG.Governance.EvidenceReuse"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.GateRun"
                  || n = "FS.GG.Governance.GateExecution"
                  || n = "FS.GG.Governance.ExecutionRecord"
                  || n = "FS.GG.Governance.CommandRecord"
                  || n = "FS.GG.Governance.RuleIdentity"
                  || n = "FS.GG.Governance.CurrencyEnforcement"
                  || n = "FS.GG.Governance.RefreshJson"
                  || n = "FS.GG.Governance.GeneratedViewsJson"
                  || n = "FS.GG.Governance.JsonText"
                  || n = "FS.GG.Governance.JsonWriters"
                  || n = "FS.GG.Governance.JsonTokens")
              auditJson ]
