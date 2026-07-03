module FS.GG.Governance.DependencyFences.Tests.YamlFenceTests

// 100 · Fence 1 (M-ARCH-1) — the direct-YamlDotNet owner set must equal the documented allowlist.
// Contract: specs/100-dependency-fences/contracts/dependency-fences.md §Fence 1.
// The allowlist below is the AUTHORITATIVE documented state; the README table must agree with it.

open Expecto
open FS.GG.Governance.DependencyFences.Tests.ProjectGraph

// The four genuine YAML owners (T004 audit removed ReleaseCommand's dead reference). Keep this list,
// the README (README.md §capability platform + §.fsgg configuration model), and the contract in sync.
let documentedYamlOwners : Set<string> =
    Set.ofList
        [ "FS.GG.Governance.Config"
          "FS.GG.Governance.CurrencySensing"
          "FS.GG.Governance.RefreshCommand"
          "FS.GG.Governance.ReleaseDeclaration" ]

[<Tests>]
let tests =
    testList
        "dependency-fences · yaml-owner"
        [
          // Production assertion over the REAL project graph (Principle V).
          test "the direct-YamlDotNet owner set equals the documented allowlist" {
              let violations = yamlOwnerViolations documentedYamlOwners (load ())

              let offenders =
                  violations |> List.map render |> String.concat System.Environment.NewLine

              Expect.isEmpty
                  violations
                  (sprintf
                      "the set of projects with a direct YamlDotNet reference must equal the documented YAML-owner allowlist; found:%s%s"
                      System.Environment.NewLine
                      offenders)
          }

          // Red-path (pure matcher over literal nodes): an undocumented owner is caught.
          test "an undocumented YamlDotNet owner is flagged" {
              let node name refs =
                  { Name = name
                    Path = name + "/" + name + ".fsproj"
                    OutputType = "Library"
                    PackAsTool = false
                    ToolCommandName = None
                    IsPackable = true
                    PackageReferences = Set.ofList refs
                    ProjectReferences = Set.empty }

              let graph =
                  [ node "FS.GG.Governance.Config" [ "YamlDotNet" ]
                    node "FS.GG.Governance.Findings" [ "YamlDotNet" ] ] // Findings must NOT parse YAML.

              let allowed = Set.ofList [ "FS.GG.Governance.Config" ]
              let violations = yamlOwnerViolations allowed graph

              Expect.isNonEmpty violations "an undocumented YamlDotNet owner must be caught"

              Expect.isTrue
                  (violations |> List.exists (fun v -> v.Project = "FS.GG.Governance.Findings"))
                  "the diagnostic must name the offending project (Findings)"
          }

          // Red-path: a documented owner that dropped YamlDotNet is also caught (keeps the list honest).
          test "a documented owner that dropped YamlDotNet is flagged" {
              let node name refs =
                  { Name = name
                    Path = name + "/" + name + ".fsproj"
                    OutputType = "Library"
                    PackAsTool = false
                    ToolCommandName = None
                    IsPackable = true
                    PackageReferences = Set.ofList refs
                    ProjectReferences = Set.empty }

              let graph = [ node "FS.GG.Governance.Config" [] ] // owner lost its ref.
              let allowed = Set.ofList [ "FS.GG.Governance.Config" ]

              Expect.isNonEmpty
                  (yamlOwnerViolations allowed graph)
                  "a documented owner missing its YamlDotNet reference must be caught"
          } ]
