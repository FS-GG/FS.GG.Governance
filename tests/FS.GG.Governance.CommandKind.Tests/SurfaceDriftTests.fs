module FS.GG.Governance.CommandKind.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.CommandKind

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, SC-008), now via the
// shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper and here.

let private commandKindAsm =
    Audit.kindToken FS.GG.Governance.CommandKind.Model.Build |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.CommandKind"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "CommandKind" "FS.GG.Governance.CommandKind" commandKindAsm

          SurfaceDrift.referencesOnly
              "CommandKind"
              (fun n ->
                  n = "FS.GG.Governance.CommandRecord"
                  || n = "FS.GG.Governance.Provenance"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.Config")
              commandKindAsm ]
