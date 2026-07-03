module FS.GG.Governance.Ship.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Ship.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here, never in the library.

// Touch a member to force the library assembly to load, then locate it by name among the loaded
// assemblies.
let private shipAsm =
    (Pass: Verdict) |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.Ship"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "Ship" "FS.GG.Governance.Ship" shipAsm

          test "the hidden mappings, item-identity builder, and sort key never leak into the public surface" {
              let surfaceText = SurfaceDrift.renderSurface shipAsm

              for hidden in [ "gateToInput"; "findingToInput"; "itemSortKey" ] do
                  Expect.isFalse (surfaceText.Contains hidden) (sprintf "%s must be hidden (absent from Ship.fsi)" hidden)
          }

          test "the surface exposes no audit-doc/exit-code-number/cache/freshness/policy/IO/CLI member (FR-012/SC-007)" {
              let surfaceText = (SurfaceDrift.renderSurface shipAsm).ToLowerInvariant()

              for forbidden in [ "audit"; "writeall"; "filestream"; "httpclient"; "policy"; "freshness"; "cache"; "exitcode get_" ] do
                  Expect.isFalse (surfaceText.Contains forbidden) (sprintf "no %s member (pure decision only)" forbidden)
          }

          SurfaceDrift.referencesOnly
              "Ship"
              (fun n ->
                  n = "FS.GG.Governance.Enforcement"
                  || n = "FS.GG.Governance.Route"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.Findings"
                  || n = "FS.GG.Governance.Config")
              shipAsm ]
