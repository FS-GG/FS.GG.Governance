module FS.GG.Governance.DependencyFences.Tests.ExeLeafTests

// 100 · Fence 2 (M-ARCH-2) — no executable may reference another executable, directly or transitively.
// Contract: specs/100-dependency-fences/contracts/dependency-fences.md §Fence 2.
// After the RoutePipeline + ProjectSensing extractions, all eight executables are leaves:
// the former Cli→RouteCommand and EvidenceCommand→Cli edges now go through internal libraries.

open Expecto
open FS.GG.Governance.DependencyFences.Tests.ProjectGraph

[<Tests>]
let tests =
    testList
        "dependency-fences · exe-leaf"
        [
          // Production assertion over the REAL project graph (Principle V).
          test "no executable references another executable (directly or transitively)" {
              let violations = exeExeEdges (load ())

              let offenders =
                  violations |> List.map render |> String.concat System.Environment.NewLine

              Expect.isEmpty
                  violations
                  (sprintf
                      "every executable must be a leaf — no Exe may reach another Exe via ProjectReferences; found:%s%s"
                      System.Environment.NewLine
                      offenders)
          }

          // Red-path (pure matcher over literal nodes): a direct exe→exe edge is caught.
          test "a direct exe→exe edge is flagged" {
              let node name outputType refs =
                  { Name = name
                    Path = name + "/" + name + ".fsproj"
                    OutputType = outputType
                    PackAsTool = outputType = "Exe"
                    ToolCommandName = None
                    IsPackable = true
                    PackageReferences = Set.empty
                    ProjectReferences = Set.ofList refs }

              let graph =
                  [ node "FS.GG.Governance.Cli" "Exe" [ "FS.GG.Governance.RouteCommand" ]
                    node "FS.GG.Governance.RouteCommand" "Exe" []
                    node "FS.GG.Governance.RoutePipeline" "Library" [] ]

              let violations = exeExeEdges graph
              Expect.isNonEmpty violations "a direct exe→exe edge must be caught"

              Expect.isTrue
                  (violations |> List.exists (fun v -> v.Detail.Contains "FS.GG.Governance.Cli" && v.Detail.Contains "FS.GG.Governance.RouteCommand"))
                  "the diagnostic names the offending Exe → Exe edge"
          }

          // Red-path: a TRANSITIVE exe→exe edge (A → lib → B) is also caught.
          test "a transitive exe→exe edge through a library is flagged" {
              let node name outputType refs =
                  { Name = name
                    Path = name + "/" + name + ".fsproj"
                    OutputType = outputType
                    PackAsTool = outputType = "Exe"
                    ToolCommandName = None
                    IsPackable = true
                    PackageReferences = Set.empty
                    ProjectReferences = Set.ofList refs }

              // EvidenceCommand → SomeLib → Cli (Cli is an Exe) — a transitive exe→exe reach.
              let graph =
                  [ node "FS.GG.Governance.EvidenceCommand" "Exe" [ "FS.GG.Governance.SomeLib" ]
                    node "FS.GG.Governance.SomeLib" "Library" [ "FS.GG.Governance.Cli" ]
                    node "FS.GG.Governance.Cli" "Exe" [] ]

              Expect.isNonEmpty
                  (exeExeEdges graph)
                  "a transitive exe→exe reach through a library must be caught"
          } ]
