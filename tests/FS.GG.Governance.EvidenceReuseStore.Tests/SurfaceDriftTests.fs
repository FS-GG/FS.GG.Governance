module FS.GG.Governance.EvidenceReuseStore.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.EvidenceReuseStore

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3).

let private evidenceReuseStore = SurfaceDrift.assemblyNamed "FS.GG.Governance.EvidenceReuseStore"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "EvidenceReuseStore" "FS.GG.Governance.EvidenceReuseStore" evidenceReuseStore

          test "the public surface is exactly the EvidenceReuseStore module, nothing private" {
              let typeNames =
                  evidenceReuseStore.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              // exactly one exported type: the EvidenceReuseStore module. No writeToString / token-writer /
              // fold-helper leak (they are hidden by EvidenceReuseStore.fsi).
              Expect.equal typeNames.Length 1 "exactly one exported type (the EvidenceReuseStore module)"

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.EvidenceReuseStore.EvidenceReuseStoreModule"))
                  "EvidenceReuseStore module is public"
          }

          SurfaceDrift.referencesOnly
              "EvidenceReuseStore"
              (fun n ->
                  n = "FS.GG.Governance.EvidenceReuse"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.Config"
                  // 073: the dependency-free JsonText leaf (the shared deterministic-emit helper writeToString).
                  || n = "FS.GG.Governance.JsonText")
              evidenceReuseStore ]
