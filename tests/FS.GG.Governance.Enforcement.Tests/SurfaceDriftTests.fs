module FS.GG.Governance.Enforcement.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Enforcement.Enforcement

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library.

// Touch a member to force the library assembly to load, then locate it by name among the loaded
// assemblies.
let private enforcementAsm =
    runModeOrdinal Sandbox |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.Enforcement"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "Enforcement" "FS.GG.Governance.Enforcement" enforcementAsm

          test "the hidden floor/tighten maps and reason builders never leak into the public surface" {
              let surfaceText = SurfaceDrift.renderSurface enforcementAsm

              for hidden in [ "maturityFloor"; "profileTighten"; "withholdReason"; "blockingReason"; "relaxedReason"; "baseAdvisoryReason" ] do
                  Expect.isFalse (surfaceText.Contains hidden) (sprintf "%s must be hidden (absent from Enforcement.fsi)" hidden)
          }

          test "the surface exposes no rollup/ship-verdict/blockers/exit-code/IO/CLI member (FR-013/FR-014)" {
              let surfaceText = (SurfaceDrift.renderSurface enforcementAsm).ToLowerInvariant()

              for forbidden in [ "verdict"; "blockers"; "exitcode"; "rollup"; "writeall"; "filestream"; "httpclient"; "policy" ] do
                  Expect.isFalse (surfaceText.Contains forbidden) (sprintf "no %s member (pure decision only)" forbidden)
          }

          SurfaceDrift.referencesOnly "Enforcement" (fun n -> n = "FS.GG.Governance.Config") enforcementAsm ]
