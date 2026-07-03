module FS.GG.Governance.GateExecution.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.GateExecution.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, the Snapshot/F050 precedent),
// now via the shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the
// library. The check inspects the PRODUCTION assembly, not the test assembly — the test project's
// F049/F030/F029 references are deliberately excluded.

let private gateExec = typeof<ExecutionOutcome>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "GateExecution" "FS.GG.Governance.GateExecution" gateExec

          SurfaceDrift.referencesOnly
              "GateExecution"
              (fun n ->
                  n = "FS.GG.Governance.ExecutionRecord"
                  || n = "FS.GG.Governance.CommandRecord"
                  || n = "FS.GG.Governance.Config")
              gateExec

          test "no later-phase / host / network symbol leaked into the production library (SC-007)" {
              let banned =
                  [ "FS.GG.Governance.EvidenceCapture"
                    "FS.GG.Governance.EvidenceReuse"
                    "FS.GG.Governance.FreshnessKey"
                    "FS.GG.Governance.FreshnessSensing"
                    "FS.GG.Governance.EvidenceReuseStore"
                    "FS.GG.Governance.CacheEligibility"
                    "FS.GG.Governance.RouteJson"
                    "FS.GG.Governance.AuditJson"
                    "FS.GG.Governance.Enforcement"
                    "FS.GG.Governance.Ship"
                    "FS.GG.Governance.Snapshot"
                    "FS.GG.Governance.Routing"
                    "FS.GG.Governance.Host"
                    "FS.GG.Governance.Cli"
                    "System.Net.Http"
                    "System.Net.Sockets"
                    "Octokit"
                    "LibGit2Sharp" ]

              let referenced =
                  gateExec.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)

              for b in banned do
                  Expect.isFalse
                      (referenced |> Array.exists (fun n -> n.Contains b))
                      (sprintf "GateExecution must not reference %s (additive, no host/network leak, SC-007)" b)
          } ]
