module FS.GG.Governance.GateRun.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.GateRun.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, the GateExecution precedent),
// now via the shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the
// library. The check inspects the PRODUCTION assembly, not the test assembly — the test project's
// EvidenceCapture reference is deliberately excluded.

let private gateRun = typeof<GateOutcome>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "GateRun" "FS.GG.Governance.GateRun" gateRun

          SurfaceDrift.referencesOnly
              "GateRun"
              (fun n ->
                  n = "FS.GG.Governance.GateExecution"
                  || n = "FS.GG.Governance.ExecutionRecord"
                  || n = "FS.GG.Governance.CommandRecord"
                  || n = "FS.GG.Governance.EvidenceReuse"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.Gates")
              gateRun

          test "no capture/host/enforcement symbol leaked into the production library (scope hygiene)" {
              let banned =
                  [ "FS.GG.Governance.EvidenceCapture"
                    "FS.GG.Governance.EvidenceReuseStore"
                    "FS.GG.Governance.FreshnessSensing"
                    "FS.GG.Governance.CacheEligibility"
                    "FS.GG.Governance.RouteJson"
                    "FS.GG.Governance.AuditJson"
                    "FS.GG.Governance.Enforcement"
                    "FS.GG.Governance.Ship"
                    "FS.GG.Governance.RouteCommand"
                    "FS.GG.Governance.ShipCommand"
                    "FS.GG.Governance.Snapshot"
                    "FS.GG.Governance.Host"
                    "FS.GG.Governance.Cli" ]

              let referenced =
                  gateRun.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)

              for b in banned do
                  Expect.isFalse
                      (referenced |> Array.exists (fun n -> n.Contains b))
                      (sprintf "GateRun must not reference %s (pure helper layer, scope hygiene)" b)
          } ]
