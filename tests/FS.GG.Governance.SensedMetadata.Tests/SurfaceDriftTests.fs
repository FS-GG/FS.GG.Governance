module FS.GG.Governance.SensedMetadata.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.SensedMetadata
open FS.GG.Governance.SensedMetadata.Model

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II, plan D1). The surface-equality
// check now runs via the shared SurfaceDrift helper (101/M-CI-3); the exports-shape and scope/forbidden-set
// guards stay inline. Reflection lives in the helper and in these guards, never in the library.

// Touch a member of each public module to force the library assembly to load, then locate it by name.
let private sensedAsm =
    SensedMetadata.renderingValue (SensedRendering "load") |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.SensedMetadata"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "SensedMetadata" "FS.GG.Governance.SensedMetadata" sensedAsm

          test "the public surface is exactly the two modules (Model + SensedMetadata), nothing else" {
              let typeNames =
                  sensedAsm.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.SensedMetadata.ModelModule"))
                  "Model module is public"
              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.EndsWith "FS.GG.Governance.SensedMetadata.SensedMetadataModule"))
                  "SensedMetadata operations module is public"
              Expect.isFalse
                  (typeNames |> Array.exists (fun n -> n.ToLowerInvariant().Contains "encode" || n.ToLowerInvariant().Contains "prefix" || n.ToLowerInvariant().Contains "segment"))
                  "no encoder/prefix/segment helper module leaks into the public surface"
          }

          test "SensedMetadata references only CommandRecord + Config + BCL + FSharp.Core (plan D1 scope guard)" {
              // Single sibling reference (D1): SensedMetadata -> CommandRecord (owns F032 `SensedDuration`),
              // a pure vocab core. Config arrives transitively via CommandRecord (it may legitimately be
              // absent from the DIRECT refs — this is an ALLOW-SET whitelist, not a presence assertion).
              // (Contrast F033's three-core reference: FreshnessKey + CommandRecord + Config.)
              let allowed (name: string) =
                  name = "FSharp.Core"
                  || name = "FS.GG.Governance.CommandRecord"
                  || name = "FS.GG.Governance.Config"
                  || name = "System.Private.CoreLib"
                  || name = "netstandard"
                  || name = "mscorlib"
                  || name.StartsWith "System."

              let offending =
                  sensedAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (allowed >> not)

              Expect.isEmpty
                  offending
                  (sprintf "SensedMetadata must depend on CommandRecord/Config/BCL/FSharp.Core only; found: %A" offending)

              // Specifically: NOT FreshnessKey/Provenance/Gates/Snapshot/Route/Routing/Findings/EvidenceReuse/
              // RouteExplain/RouteJson/GatesJson/AuditJson/Enforcement/Ship/host/adapters/CLI or any other
              // identity-computing or edge assembly (FR-006 identity-neutrality is structural, D5).
              let forbidden =
                  sensedAsm.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.FreshnessKey"
                      || n = "FS.GG.Governance.Provenance"
                      || n = "FS.GG.Governance.Gates"
                      || n = "FS.GG.Governance.Snapshot"
                      || n = "FS.GG.Governance.Route"
                      || n = "FS.GG.Governance.Routing"
                      || n = "FS.GG.Governance.Findings"
                      || n = "FS.GG.Governance.EvidenceReuse"
                      || n = "FS.GG.Governance.RouteExplain"
                      || n = "FS.GG.Governance.RouteJson"
                      || n = "FS.GG.Governance.GatesJson"
                      || n = "FS.GG.Governance.AuditJson"
                      || n = "FS.GG.Governance.Enforcement"
                      || n = "FS.GG.Governance.Ship"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.Cli"
                      || n.StartsWith "FS.GG.Governance.Adapters")

              Expect.isEmpty
                  forbidden
                  (sprintf "SensedMetadata must not reference FreshnessKey/Provenance/Gates/Snapshot/Route/Routing/Findings/EvidenceReuse/RouteExplain/RouteJson/GatesJson/AuditJson/Enforcement/Ship/host/adapters/CLI; found: %A" forbidden)
          } ]
