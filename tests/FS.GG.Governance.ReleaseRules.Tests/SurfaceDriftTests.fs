module FS.GG.Governance.ReleaseRules.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3).

let private releaseRulesAsm = SurfaceDrift.assemblyNamed "FS.GG.Governance.ReleaseRules"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "ReleaseRules" "FS.GG.Governance.ReleaseRules" releaseRulesAsm

          test "the per-rule classifier, EnforcementInput builder, and partition helper never leak" {
              let surfaceText = (SurfaceDrift.renderSurface releaseRulesAsm).ToLowerInvariant()

              for hidden in [ "classify"; "enforcementinput"; "partition"; "toinput"; "reasonfor" ] do
                  Expect.isFalse (surfaceText.Contains hidden) (sprintf "%s must be hidden (absent from Release.fsi)" hidden)
          }

          test "the surface exposes no sensing/process/document/IO member (FR-007/FR-008)" {
              let surfaceText = (SurfaceDrift.renderSurface releaseRulesAsm).ToLowerInvariant()

              for forbidden in [ "writeall"; "filestream"; "httpclient"; "process"; "json"; "release.json" ] do
                  Expect.isFalse (surfaceText.Contains forbidden) (sprintf "no %s member (pure decision only)" forbidden)
          }

          SurfaceDrift.referencesOnly
              "ReleaseRules"
              (fun n ->
                  // Ship pulls Route/Gates/Findings transitively.
                  n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.Enforcement"
                  || n = "FS.GG.Governance.Ship"
                  || n = "FS.GG.Governance.Route"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.Findings")
              releaseRulesAsm ]
