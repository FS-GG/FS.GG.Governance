module FS.GG.Governance.EvidenceJson.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.EvidenceJson

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3).

let private evidenceJson =
    EvidenceJson.schemaVersion |> ignore

    System.AppDomain.CurrentDomain.GetAssemblies()
    |> Array.find (fun a ->
        match Option.ofObj (a.GetName().Name) with
        | Some n -> n = "FS.GG.Governance.EvidenceJson"
        | None -> false)

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "EvidenceJson" "FS.GG.Governance.EvidenceJson" evidenceJson

          // Leaf scope guard (D7): EvidenceJson -> Kernel + the freshness-cause graph (EvidenceReuse/
          // FreshnessResolution/FreshnessKey/Config/Gates), plus the 073 Json* leaves. No host/command/
          // Cli/adapter edge.
          SurfaceDrift.referencesOnly
              "EvidenceJson"
              (fun n ->
                  n = "FS.GG.Governance.Kernel"
                  || n = "FS.GG.Governance.EvidenceReuse"
                  || n = "FS.GG.Governance.FreshnessResolution"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.JsonText"
                  || n = "FS.GG.Governance.JsonWriters")
              evidenceJson ]
