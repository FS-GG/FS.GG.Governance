module FS.GG.Governance.DependencyFences.Tests.FsggOwnerTests

// 100 · Fence 3 (M-ARCH-3) — at most one project may claim ToolCommandName=fsgg.
// Contract: specs/100-dependency-fences/contracts/dependency-fences.md §Fence 3.
// After this change RouteCommand is the sole `fsgg` owner; EvidenceCommand → fsgg-evidence,
// CacheEligibilityCommand → fsgg-cache-eligibility (until the ADR-0003 multiplexer lands).
//
// ARCH-3 (2026-07-15 review) generalizes the fence: `fsggClaimants` only ever caught a second
// `fsgg` claimant, so two tools colliding on ANY OTHER command name (a duplicate `fsgg-evidence`,
// say) passed silently. `toolCommandCollisions` closes that class — no `ToolCommandName` may be
// claimed by more than one publishable project — while the original `fsgg`-specific case is kept.

open Expecto
open FS.GG.Governance.DependencyFences.Tests.ProjectGraph

[<Tests>]
let tests =
    testList
        "dependency-fences · fsgg-owner"
        [
          // Production assertion over the REAL project graph (Principle V).
          test "at most one project claims ToolCommandName=fsgg" {
              let claimants = fsggClaimants (load ())

              Expect.isTrue
                  (List.length claimants <= 1)
                  (sprintf
                      "at most one project may set ToolCommandName=fsgg; found %d: %s"
                      (List.length claimants)
                      (String.concat ", " claimants))
          }

          // Red-path (pure matcher over literal nodes): two claimants are caught.
          test "two fsgg claimants are flagged" {
              let node name (tcn: string option) =
                  { Name = name
                    Path = name + "/" + name + ".fsproj"
                    OutputType = "Exe"
                    PackAsTool = true
                    ToolCommandName = tcn
                    IsPackable = true
                    PackageReferences = Set.empty
                    ProjectReferences = Set.empty }

              let graph =
                  [ node "FS.GG.Governance.RouteCommand" (Some "fsgg")
                    node "FS.GG.Governance.EvidenceCommand" (Some "fsgg") // collision.
                    node "FS.GG.Governance.Cli" (Some "fsgg-governance") ]

              Expect.equal
                  (fsggClaimants graph)
                  [ "FS.GG.Governance.EvidenceCommand"; "FS.GG.Governance.RouteCommand" ]
                  "both fsgg claimants must be reported"
          }

          // Production assertion over the REAL project graph (Principle V): the generalized fence —
          // no ToolCommandName is shared by two publishable tools (closes ARCH-3, not just `fsgg`).
          test "no ToolCommandName is claimed by more than one publishable project" {
              let violations = toolCommandCollisions (load ())

              Expect.isEmpty
                  violations
                  (sprintf
                      "every publishable ToolCommandName must be unique; found: %s"
                      (violations |> List.map render |> String.concat "; "))
          }

          // Red-path (pure matcher over literal nodes): a NON-fsgg collision — the class the old
          // `fsgg`-only check let through — is now flagged, and the fsgg-unique baseline is clean.
          test "a non-fsgg ToolCommandName collision is flagged" {
              let node name (tcn: string option) =
                  { Name = name
                    Path = name + "/" + name + ".fsproj"
                    OutputType = "Exe"
                    PackAsTool = true
                    ToolCommandName = tcn
                    IsPackable = true
                    PackageReferences = Set.empty
                    ProjectReferences = Set.empty }

              let graph =
                  [ node "FS.GG.Governance.RouteCommand" (Some "fsgg") // sole fsgg owner — fine.
                    node "FS.GG.Governance.EvidenceCommand" (Some "fsgg-evidence")
                    node "FS.GG.Governance.EvidenceCommand.Alt" (Some "fsgg-evidence") // collision.
                    node "FS.GG.Governance.Cli" (Some "fsgg-governance") ]

              let violations = toolCommandCollisions graph

              Expect.equal (List.length violations) 1 "exactly the fsgg-evidence collision is reported"

              Expect.stringContains
                  (violations |> List.map render |> String.concat "; ")
                  "fsgg-evidence"
                  "the diagnostic must name the colliding command"

              // A node that is not published as a tool cannot collide, even sharing a name.
              let notPacked =
                  [ node "A" (Some "shared")
                    { node "B" (Some "shared") with
                        PackAsTool = false } ]

              Expect.isEmpty
                  (toolCommandCollisions notPacked)
                  "a non-PackAsTool project sharing a name is not a tool-install collision"
          } ]
