module FS.GG.Governance.HumanText.Tests.DependencyBoundaryTests

open Expecto
open FS.GG.Governance.HumanText
open FS.GG.Governance.Tests.Common

// T047 [SC-007]: the pure HumanText library must NOT reference Spectre.Console (or any presentation
// package). Spectre lives ONLY in FS.GG.Governance.HumanRender (FR-013, SC-007).

let private humanTextAssembly = SurfaceDrift.assemblyNamed "FS.GG.Governance.HumanText"

[<Tests>]
let tests =
    testList
        "DependencyBoundary"
        [ test "HumanText does not reference Spectre.Console or any presentation package" {
              let referenced =
                  humanTextAssembly.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)

              let offending =
                  referenced
                  |> Array.filter (fun n -> n.StartsWith "Spectre" || n = "Terminal.Gui")

              Expect.isEmpty offending (sprintf "HumanText must not reference a presentation package; found: %A" offending)
          }

          test "HumanText references only report cores + BCL + FSharp.Core" {
              let referenced =
                  humanTextAssembly.GetReferencedAssemblies()
                  |> Array.choose (fun a -> Option.ofObj a.Name)

              let forbidden =
                  referenced
                  |> Array.filter (fun n ->
                      n = "FS.GG.Governance.Kernel"
                      || n = "FS.GG.Governance.Host"
                      || n = "FS.GG.Governance.HumanRender"
                      || n.StartsWith "Spectre")

              Expect.isEmpty forbidden (sprintf "HumanText must not reference host/render/Spectre; found: %A" forbidden)
          } ]
