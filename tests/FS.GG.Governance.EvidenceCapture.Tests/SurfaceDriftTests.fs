module FS.GG.Governance.EvidenceCapture.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.EvidenceCapture

// Reflective API surface-drift + dependency/scope-hygiene checks (Principle II), now via the shared
// SurfaceDrift helper (101/M-CI-3).

let private evidenceCapture = SurfaceDrift.assemblyNamed "FS.GG.Governance.EvidenceCapture"

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "EvidenceCapture" "FS.GG.Governance.EvidenceCapture" evidenceCapture

          test "the public surface is exactly the EvidenceCapture module, nothing private" {
              let typeNames =
                  evidenceCapture.GetExportedTypes()
                  |> Array.choose (fun t -> Option.ofObj t.FullName)
                  |> Array.sort

              // exactly one exported type: the EvidenceCapture module. No helper leak (there are no private
              // helpers — both bodies are one-line compositions).
              Expect.equal typeNames.Length 1 "exactly one exported type (the EvidenceCapture module)"

              Expect.isTrue
                  (typeNames |> Array.exists (fun n -> n.Contains "FS.GG.Governance.EvidenceCapture.EvidenceCaptureModule"))
                  "EvidenceCapture module is public"
          }

          SurfaceDrift.referencesOnly
              "EvidenceCapture"
              (fun n ->
                  n = "FS.GG.Governance.EvidenceReuse"
                  || n = "FS.GG.Governance.CommandRecord"
                  || n = "FS.GG.Governance.FreshnessKey"
                  || n = "FS.GG.Governance.Config")
              evidenceCapture ]
