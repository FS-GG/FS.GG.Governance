module FS.GG.Governance.Adapters.SddHandoff.Tests.SurfaceDriftTests

open Expecto
open FS.GG.Governance.Tests.Common
open FS.GG.Governance.Adapters.SddHandoff

// Reflective API surface-drift + dependency-hygiene checks for the SDD-handoff consumer (Principle II,
// SC-006), now via the shared SurfaceDrift helper (101/M-CI-3). Reflection lives in the helper. The
// "references no SDD assembly" guard is a bespoke deny-list, so it stays local.

let private sddHandoff = typeof<Model.Diagnostic>.Assembly

[<Tests>]
let tests =
    testList
        "SurfaceDrift"
        [ SurfaceDrift.surfaceTest "SddHandoff" "FS.GG.Governance.Adapters.SddHandoff" sddHandoff

          test "SddHandoff references no SDD assembly (SC-006 — consumer imports no SDD source)" {
              let offending =
                  sddHandoff.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)
                  |> Array.filter (fun n -> n.Contains "FS.GG.SDD" || n.Contains "Sdd" && n.StartsWith "FS.GG.SDD")

              Expect.isEmpty
                  offending
                  (sprintf "the consumer must reference no SDD assembly; found: %A" offending)
          }

          SurfaceDrift.referencesOnly
              "SddHandoff"
              (fun n ->
                  n = "FS.GG.Governance.Kernel"
                  || n = "FS.GG.Governance.Config"
                  || n = "FS.GG.Governance.Gates"
                  || n = "FS.GG.Governance.Route"
                  || n = "FS.GG.Governance.Routing"
                  || n = "FS.GG.Governance.Findings")
              sddHandoff ]
