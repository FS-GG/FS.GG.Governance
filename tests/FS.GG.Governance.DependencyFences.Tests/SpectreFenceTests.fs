module FS.GG.Governance.DependencyFences.Tests.SpectreFenceTests

// ARCH-2 (2026-07-15 review) — the direct-Spectre.Console owner set must equal the documented allowlist.
// Mirrors YamlFenceTests: the README (README.md §capability platform + §key dependencies) asserts
// Spectre.Console is confined to a single project, but unlike YamlDotNet nothing fenced it — the
// confinement could drift silently. This closes that gap with the same bidirectional owner fence.
// The allowlist below is the AUTHORITATIVE documented state; the README must agree with it.

open Expecto
open FS.GG.Governance.DependencyFences.Tests.ProjectGraph

// The sole Spectre.Console owner: FS.GG.Governance.HumanRender is the CLI-host presentation library and
// the ONLY project that may reference Spectre.Console (FR-013, SC-007). Keep this list, the README, and
// HumanRender.fsproj's "ONLY project" comment in sync.
let documentedSpectreOwners : Set<string> =
    Set.ofList [ "FS.GG.Governance.HumanRender" ]

[<Tests>]
let tests =
    testList
        "dependency-fences · spectre-owner"
        [
          // Production assertion over the REAL project graph (Principle V).
          test "the direct-Spectre.Console owner set equals the documented allowlist" {
              let violations = spectreOwnerViolations documentedSpectreOwners (load ())

              let offenders =
                  violations |> List.map render |> String.concat System.Environment.NewLine

              Expect.isEmpty
                  violations
                  (sprintf
                      "the set of projects with a direct Spectre.Console reference must equal the documented Spectre-owner allowlist; found:%s%s"
                      System.Environment.NewLine
                      offenders)
          }

          // Red-path (pure matcher over literal nodes): an undocumented owner is caught.
          test "an undocumented Spectre.Console owner is flagged" {
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
                  [ node "FS.GG.Governance.HumanRender" [ "Spectre.Console" ]
                    node "FS.GG.Governance.Cli" [ "Spectre.Console" ] ] // the Cli host must NOT reference Spectre directly.

              let allowed = Set.ofList [ "FS.GG.Governance.HumanRender" ]
              let violations = spectreOwnerViolations allowed graph

              Expect.isNonEmpty violations "an undocumented Spectre.Console owner must be caught"

              Expect.isTrue
                  (violations |> List.exists (fun v -> v.Project = "FS.GG.Governance.Cli"))
                  "the diagnostic must name the offending project (Cli)"
          }

          // Red-path: a documented owner that dropped Spectre.Console is also caught (keeps the list honest).
          test "a documented owner that dropped Spectre.Console is flagged" {
              let node name refs =
                  { Name = name
                    Path = name + "/" + name + ".fsproj"
                    OutputType = "Library"
                    PackAsTool = false
                    ToolCommandName = None
                    IsPackable = true
                    PackageReferences = Set.ofList refs
                    ProjectReferences = Set.empty }

              let graph = [ node "FS.GG.Governance.HumanRender" [] ] // owner lost its ref.
              let allowed = Set.ofList [ "FS.GG.Governance.HumanRender" ]

              Expect.isNonEmpty
                  (spectreOwnerViolations allowed graph)
                  "a documented owner missing its Spectre.Console reference must be caught"
          } ]
