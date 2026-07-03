module FS.GG.Governance.DependencyFences.Tests.FsggOwnerTests

// 100 · Fence 3 (M-ARCH-3) — at most one project may claim ToolCommandName=fsgg.
// Contract: specs/100-dependency-fences/contracts/dependency-fences.md §Fence 3.
// After this change RouteCommand is the sole `fsgg` owner; EvidenceCommand → fsgg-evidence,
// CacheEligibilityCommand → fsgg-cache-eligibility (until the ADR-0003 multiplexer lands).

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
          } ]
